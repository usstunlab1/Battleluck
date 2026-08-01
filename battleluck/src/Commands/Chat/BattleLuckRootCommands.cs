using BattleLuck.Commands;
using BattleLuck.Services.AI;
using BattleLuck.Services.Assistant;
using BattleLuck.Services.Practice;
using BattleLuck.Services.Chat;
using VampireCommandFramework;

namespace BattleLuck.Commands.Chat;

/// <summary>
/// The complete public BattleLuck command surface. All former .bl routes are
/// intentionally retired; requests enter through one private .ai command.
/// </summary>
public static class BattleLuckRootCommands
{
    static readonly AiLiteKnowledgeService AiLite = new();
    static readonly ZuiPacketPresenter Zui = new(
        (steamId, packet) => BattleLuckPlugin.NotifyPlayerBySteamIdRawOnMainThread(steamId, packet));

    internal static void ResetPresentationState() => Zui.Clear();

    /// <summary>
    /// VCF registration surface used for command discovery and the short usage
    /// response. The Harmony chat prefix handles complete multi-word requests
    /// before VCF tokenizes them, then routes them to <see cref="Request"/>.
    /// </summary>
    [Command("ai", usage: ".ai <text>", description: "Ask BattleLuck or describe a catalog action")]
    public static void Ai(ChatCommandContext ctx, string text = "")
    {
        text = NormalizeRequest(text);
        if (string.IsNullOrWhiteSpace(text))
        {
            ctx.Reply("Usage: .ai <question or action description>");
            return;
        }

        if (!ctx.TryGetSenderIdentity(out var character, out var steamId))
        {
            ctx.Reply("BattleLuck could not resolve your player identity.");
            return;
        }

        BattleLuckCommandDispatcher.TryDispatch(
            $".ai {text}",
            character,
            steamId,
            ctx.Event.User.IsAdmin,
            isConsole: false);
    }

    [BattleLuckCommand("ai", description: "Ask BattleLuck or describe a catalog action")]
    public static async Task Request(BattleLuckCommandContext ctx, string request = "")
    {
        request = NormalizeRequest(request);
        if (!AiRequestPolicy.TryValidate(ctx.SenderCharacterEntity, request, out var validationError))
        {
            ctx.Reply(request.Length == 0
                ? "Usage: .ai <question or action description>"
                : validationError);
            return;
        }

        if (request.Equals("end", StringComparison.OrdinalIgnoreCase))
        {
            var ended = GameChatAiBridge.EndSession(ctx.SenderSteamId);
            ctx.Reply(ended ? "AI conversation ended." : "No AI conversation is active.");
            return;
        }

        if (TryHandleSessionLifecycle(ctx, request))
            return;

        if (TryHandleZui(ctx, request))
            return;

        if (TryHandleSoloPractice(ctx, request))
            return;

        GameChatAiBridge.BeginSession(ctx.SenderSteamId);
        try
        {
            if (IntentActionRouter.TryHandle(ctx.SenderSteamId, request) ||
                NaturalLanguageActionRouter.TryHandle(ctx.SenderSteamId, request))
                return;
        }
        catch (Exception ex)
        {
            BattleLuckPlugin.LogWarning($"[AI Command] Request routing failed: {ex.Message}");
        }

        var assistant = BattleLuckPlugin.AIAssistant;
        if (assistant == null)
        {
            ctx.Reply(AiLite.Answer(request));
            GameChatAiBridge.RecordReply(ctx.SenderSteamId);
            return;
        }

        try
        {
            var reply = await assistant.HandleDirectQuery(
                ctx.SenderSteamId,
                request,
                source: "ai_request",
                broadcastToInGameChat: false).ConfigureAwait(false);

            var playerReply = string.IsNullOrWhiteSpace(reply)
                ? AiLite.Answer(request)
                : assistant.FormatInGameResponse(request, reply);
            BattleLuckPlugin.NotifyPlayerBySteamIdOnMainThread(ctx.SenderSteamId, playerReply);
            GameChatAiBridge.RecordReply(ctx.SenderSteamId);
        }
        catch (Exception ex)
        {
            BattleLuckPlugin.LogWarning($"[AI Command] Request failed: {ex.Message}");
            BattleLuckPlugin.NotifyPlayerBySteamIdOnMainThread(ctx.SenderSteamId, AiLite.Answer(request));
            GameChatAiBridge.RecordReply(ctx.SenderSteamId);
        }
    }

    /// <summary>
    /// Explicit, admin-only live-session controls.  These intentionally do
    /// not share the declarative event-file deployment rollback vocabulary:
    /// destroy/rollback here always target a currently active session and
    /// archive only BattleLuck's terminal evidence.
    /// </summary>
    static bool TryHandleSessionLifecycle(BattleLuckCommandContext ctx, string request)
    {
        var parts = request.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2 || !parts[0].Equals("event", StringComparison.OrdinalIgnoreCase))
            return false;

        var command = parts[1].ToLowerInvariant();
        if (command is not ("destroy" or "rollback" or "delete" or "archives"))
            return false;

        if (!ctx.IsAdmin)
        {
            ctx.Reply("This event lifecycle command requires administrator permission.");
            return true;
        }

        var session = BattleLuckPlugin.Session;
        if (session == null)
        {
            ctx.Reply("BattleLuck session runtime is unavailable.");
            return true;
        }

        if (command == "archives")
        {
            var records = session.ListSessionArchives();
            ctx.Reply(records.Count == 0
                ? "No BattleLuck session archives are stored."
                : "Session archives: " + string.Join(", ", records.Take(10).Select(record =>
                    $"{record.ArchiveId} [{record.ModeId}/{record.Reason}]")));
            return true;
        }

        if (command == "delete")
        {
            if (parts.Length < 4 || !parts[^1].Equals("confirm", StringComparison.OrdinalIgnoreCase))
            {
                ctx.Reply("Usage: .ai event delete <archiveId> confirm. This deletes only the named BattleLuck session archive, never a V Rising world save.");
                return true;
            }

            var result = session.DeleteSessionArchive(parts[2], confirmed: true);
            ctx.Reply(result.Success
                ? $"Deleted BattleLuck session archive '{parts[2]}'."
                : result.UserMessage);
            return true;
        }

        if (parts.Length < 3)
        {
            ctx.Reply($"Usage: .ai event {command} <eventId>. This targets an active session only.");
            return true;
        }

        var operation = command == "destroy"
            ? session.DestroyByModeId(parts[2])
            : session.RollbackByModeId(parts[2]);
        if (!operation.Success)
        {
            ctx.Reply(operation.UserMessage);
            return true;
        }

        ctx.Reply(command == "destroy"
            ? $"Destroyed active BattleLuck session(s) for '{parts[2]}'; only session-owned objects were cleaned and terminal evidence was archived."
            : $"Rolled back active BattleLuck session(s) for '{parts[2]}' to the safe pre-session boundary; no deleted geometry or V Rising world save was recreated.");
        return true;
    }

    static bool TryHandleZui(BattleLuckCommandContext ctx, string request)
    {
        var parts = request.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0 || !parts[0].Equals("ui", StringComparison.OrdinalIgnoreCase))
            return false;

        var requested = parts.Length > 1 ? parts[1] : string.Empty;
        if (requested.Equals("disable", StringComparison.OrdinalIgnoreCase) ||
            requested.Equals("native", StringComparison.OrdinalIgnoreCase))
        {
            Zui.Disable(ctx.SenderSteamId);
            ctx.Reply(BattleLuckZuiDashboard.BuildNativeText());
            return true;
        }

        var zuiRequested = requested.Equals("zui", StringComparison.OrdinalIgnoreCase) ||
                           requested.Equals("enable", StringComparison.OrdinalIgnoreCase);
        var zuiAllowed = ConfigLoader.LoadBattleLuckConfig().Chat.ZuiOptIn;
        if (zuiRequested)
        {
            if (!zuiAllowed)
            {
                ctx.Reply("Optional ZUI transport is disabled by server configuration.");
                return true;
            }
            Zui.Enable(ctx.SenderSteamId);
            requested = string.Empty;
        }

        if (!zuiAllowed && Zui.IsEnabled(ctx.SenderSteamId))
            Zui.Disable(ctx.SenderSteamId);

        if (!Zui.IsEnabled(ctx.SenderSteamId))
        {
            ctx.Reply(BattleLuckZuiDashboard.BuildNativeText(requested));
            return true;
        }

        var window = requested.Length == 0 || requested.Equals("home", StringComparison.OrdinalIgnoreCase)
            ? BattleLuckZuiDashboard.BuildHome()
            : BattleLuckZuiDashboard.BuildSection(requested);

        if (!Zui.TrySend(ctx.SenderSteamId, window))
            ctx.Reply("BattleLuck could not send the ZUI window.");
        return true;
    }

    internal static string NormalizeRequest(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.Equals("r", StringComparison.OrdinalIgnoreCase) ||
            text.Equals("request", StringComparison.OrdinalIgnoreCase))
            return string.Empty;

        if (text.StartsWith("r ", StringComparison.OrdinalIgnoreCase))
            return text["r ".Length..].Trim();
        if (text.StartsWith("request ", StringComparison.OrdinalIgnoreCase))
            return text["request ".Length..].Trim();

        var tokens = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length >= 2 && tokens[0].Equals("pr", StringComparison.OrdinalIgnoreCase))
        {
            var practiceMode = tokens[1].ToLowerInvariant() switch
            {
                "mir" => "mirror",
                "fol" => "follow",
                "fig" => "fight",
                "st" => "status",
                "stop" => "stop",
                _ => tokens[1]
            };
            return $"practice {practiceMode}";
        }

        return text;
    }

    static bool TryHandleSoloPractice(BattleLuckCommandContext ctx, string request)
    {
        var parts = request.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0 || !parts[0].Equals("practice", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!ctx.IsAdmin)
        {
            ctx.Reply("Solo practice is an admin-only command.");
            return true;
        }

        var mode = parts.Length > 1 ? parts[1] : "";
        if (mode.Equals("status", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Reply(SoloPracticeService.Instance.Status(ctx.SenderSteamId));
            return true;
        }
        var result = mode.Equals("stop", StringComparison.OrdinalIgnoreCase)
            ? SoloPracticeService.Instance.Stop(ctx.SenderSteamId)
            : SoloPracticeService.Instance.Start(ctx.SenderCharacterEntity, ctx.SenderSteamId, mode);
        ctx.Reply(result.Success
            ? (mode.Equals("stop", StringComparison.OrdinalIgnoreCase)
                ? "Solo practice NPC removed."
                : $"Solo practice AI is starting in {mode} mode.")
            : result.UserMessage);
        return true;
    }
}
