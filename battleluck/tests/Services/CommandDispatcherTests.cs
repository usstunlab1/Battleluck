using BattleLuck.Commands;
using BattleLuck.Commands.Chat;
using System.Reflection;

namespace BattleLuck.Tests.Services;

public sealed class CommandDispatcherTests
{
    [Fact]
    public void Static_command_containers_are_not_mistaken_for_abstract_bases()
    {
        Assert.True(CommandDiscoveryRules.IsCommandContainer(typeof(StaticCommands)));
        Assert.False(CommandDiscoveryRules.IsCommandContainer(typeof(AbstractCommands)));
        Assert.False(CommandDiscoveryRules.IsCommandContainer(typeof(GenericCommands<>)));
    }

    [Fact]
    public void Public_command_surface_contains_only_ai()
    {
        var root = FindRepositoryRoot();
        var dispatcherSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Commands",
            "BattleLuckCommandDispatcher.cs"));

        Assert.Contains(
            "if (!attr.Name.Equals(\"ai\", StringComparison.OrdinalIgnoreCase))",
            dispatcherSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "attr.Name.Equals(\"bl\"",
            dispatcherSource,
            StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("spawn a boss at a random position", "spawn a boss at a random position")]
    [InlineData("request spawn a boss at a random position", "spawn a boss at a random position")]
    [InlineData("  request   show results  ", "show results")]
    public void Ai_text_does_not_require_request_keyword(string input, string expected)
    {
        Assert.Equal(expected, BattleLuckRootCommands.NormalizeRequest(input));
    }

    [Fact]
    public void Ai_root_is_registered_for_vcf_help_and_routing()
    {
        var commandAttributes = typeof(BattleLuckRootCommands)
            .GetMethod(nameof(BattleLuckRootCommands.Ai))!
            .GetCustomAttributesData()
            .Where(attribute => attribute.AttributeType.FullName ==
                                "VampireCommandFramework.CommandAttribute")
            .ToArray();

        var attribute = Assert.Single(commandAttributes);
        Assert.Equal("ai", attribute.ConstructorArguments[0].Value);
    }

    [Fact]
    public void Dispatcher_source_defines_per_player_rate_limiting()
    {
        var root = FindRepositoryRoot();
        var dispatcherSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Commands",
            "BattleLuckCommandDispatcher.cs"));

        Assert.Contains("MaxCommandsPerWindow", dispatcherSource, StringComparison.Ordinal);
        Assert.Contains("RateLimitWindow", dispatcherSource, StringComparison.Ordinal);
        Assert.Contains("TryAcquireRateLimit", dispatcherSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Dispatcher_source_defines_input_length_guard()
    {
        var root = FindRepositoryRoot();
        var dispatcherSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Commands",
            "BattleLuckCommandDispatcher.cs"));

        Assert.Contains("MaxRawInputLength", dispatcherSource, StringComparison.Ordinal);
        Assert.Contains("Command too long", dispatcherSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Dispatcher_source_exempts_admins_from_rate_limiting()
    {
        var root = FindRepositoryRoot();
        var dispatcherSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Commands",
            "BattleLuckCommandDispatcher.cs"));

        Assert.Contains("!isAdmin && !isConsole && !TryAcquireRateLimit", dispatcherSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Dispatcher_source_evicts_stale_rate_limit_entries()
    {
        var root = FindRepositoryRoot();
        var dispatcherSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Commands",
            "BattleLuckCommandDispatcher.cs"));

        Assert.Contains("_rateLimits.Count > 1024", dispatcherSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Declared_BattleLuck_commands_have_dispatchable_handlers_and_unique_names()
    {
        var declarations = GetLoadableTypes(typeof(BattleLuckPlugin).Assembly)
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .Select(method => new
                {
                    Type = type,
                    Method = method,
                    Attribute = method.GetCustomAttribute<BattleLuckCommandAttribute>()
                }))
            .Where(item => item.Attribute != null)
            .ToArray();

        Assert.NotEmpty(declarations);
        Assert.All(declarations, declaration =>
        {
            Assert.False(string.IsNullOrWhiteSpace(declaration.Attribute!.Name));
            var parameters = declaration.Method.GetParameters();
            Assert.NotEmpty(parameters);
            Assert.Equal(typeof(BattleLuckCommandContext), parameters[0].ParameterType);
            Assert.True(
                declaration.Method.ReturnType == typeof(void) ||
                declaration.Method.ReturnType == typeof(OperationResult) ||
                typeof(Task).IsAssignableFrom(declaration.Method.ReturnType),
                $"{declaration.Type.FullName}.{declaration.Method.Name} has unsupported return type {declaration.Method.ReturnType}.");
        });

        var duplicateNames = declarations
            .GroupBy(item => item.Attribute!.Name, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        Assert.Empty(duplicateNames);
    }

    static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.OfType<Type>();
        }
    }

    static class StaticCommands { }
    abstract class AbstractCommands { }
    sealed class GenericCommands<T> { }

    static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "BattleLuck.csproj")))
                return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
