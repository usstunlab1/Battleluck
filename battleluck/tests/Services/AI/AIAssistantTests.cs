using BattleLuck.Core;
using FluentAssertions;

namespace BattleLuck.Tests.Services.AI;

public class AIAssistantTests
{
    [Fact]
    public void FormatInGameResponse_RemovesModelRichTextTagsBeforeApplyingColor()
    {
        var assistant = new AIAssistant();

        var result = assistant.FormatInGameResponse(
            "help",
            "<color=#FF0000><b>Injected</b></color> response");

        result.Should().NotContain("#FF0000");
        result.Should().NotContain("<b>");
        result.Should().Contain("Injected");
    }
}
