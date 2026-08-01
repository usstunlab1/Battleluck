using System.Text;
using System.Text.Json;
using BattleLuck.Services.Chat;

namespace BattleLuck.Tests.Services;

public sealed class ZuiPacketPresenterTests
{
    [Fact]
    public void TrySend_RequiresOptIn()
    {
        var packets = new List<string>();
        var presenter = new ZuiPacketPresenter((_, packet) => packets.Add(packet));

        var sent = presenter.TrySend(7, BattleLuckZuiDashboard.BuildHome());

        Assert.False(sent);
        Assert.Empty(packets);
    }

    [Fact]
    public void TrySend_EmitsAValidBoundedWindowPacket()
    {
        var packets = new List<string>();
        var presenter = new ZuiPacketPresenter((_, packet) => packets.Add(packet));
        presenter.Enable(7);

        var sent = presenter.TrySend(7, BattleLuckZuiDashboard.BuildHome());

        Assert.True(sent);
        var packet = Assert.Single(packets);
        Assert.StartsWith("[[ZUI]]", packet);
        Assert.InRange(Encoding.UTF8.GetByteCount(packet), 1, NotificationHelper.MaxSystemMessageUtf8Bytes);
        using var payload = JsonDocument.Parse(packet["[[ZUI]]".Length..]);
        Assert.Equal("battleluck.home", payload.RootElement.GetProperty("id").GetString());
        Assert.Equal(5, payload.RootElement.GetProperty("buttons").GetArrayLength());
    }

    [Fact]
    public void BuildSection_UsesOnlyServerCommands()
    {
        var window = BattleLuckZuiDashboard.BuildSection("audit");

        Assert.All(window.Buttons, button => Assert.True(ZuiPacketPresenter.IsViewOnlyCommand(button.Command)));
    }

    [Theory]
    [InlineData(".ai")]
    [InlineData(".ai join bloodbath")]
    [InlineData(".ai execute loot.grant:prefab=Item_Weapon_Sword_T08_Sanguine")]
    [InlineData(".ai yes")]
    [InlineData(".ai start")]
    [InlineData(".ai ui unknown")]
    public void ViewOnlyPolicy_RejectsAnythingOutsideClosedNavigation(string command)
    {
        Assert.False(ZuiPacketPresenter.IsViewOnlyCommand(command));
    }

    [Theory]
    [InlineData(".ai ui")]
    [InlineData(".ai ui events")]
    [InlineData(".ai ui loot")]
    [InlineData(".ai ui signs")]
    [InlineData(".ai ui weapons")]
    public void ViewOnlyPolicy_AcceptsClosedNavigation(string command)
    {
        Assert.True(ZuiPacketPresenter.IsViewOnlyCommand(command));
    }

    [Fact]
    public void PresenterState_IsInstanceOwned()
    {
        var first = new ZuiPacketPresenter((_, _) => { });
        var second = new ZuiPacketPresenter((_, _) => { });
        first.Enable(7);

        Assert.True(first.IsEnabled(7));
        Assert.False(second.IsEnabled(7));
    }

    [Fact]
    public void TrySend_StripsMutationBearingButtons()
    {
        var packets = new List<string>();
        var presenter = new ZuiPacketPresenter((_, packet) => packets.Add(packet));
        presenter.Enable(7);
        var window = new ZuiWindow(
            "battleluck.test",
            "Test",
            new[] { "Read only" },
            new[]
            {
                new ZuiButton("Safe", ".ai ui"),
                new ZuiButton("Unsafe", ".ai execute loot.roll:rewardId=test")
            });

        Assert.True(presenter.TrySend(7, window));

        using var payload = JsonDocument.Parse(Assert.Single(packets)["[[ZUI]]".Length..]);
        var buttons = payload.RootElement.GetProperty("buttons").EnumerateArray().ToArray();
        Assert.Single(buttons);
        Assert.Equal(".ai ui", buttons[0].GetProperty("command").GetString());
    }

    [Fact]
    public void UnknownSection_FallsBackToHome()
    {
        var window = BattleLuckZuiDashboard.BuildSection("not-a-route");

        Assert.Equal("battleluck.home", window.Id);
        Assert.All(window.Buttons, button =>
            Assert.True(ZuiPacketPresenter.IsViewOnlyCommand(button.Command)));
    }
}
