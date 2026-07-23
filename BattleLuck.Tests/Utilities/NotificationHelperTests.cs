using System.Text;
using BattleLuck.Utilities;
using FluentAssertions;

namespace BattleLuck.Tests.Utilities;

public class NotificationHelperTests
{
    [Fact]
    public void ClampForSystemMessage_LeavesTextAtOrBelowByteBudgetUntouched()
    {
        var text = new string('x', NotificationHelper.MaxSystemMessageUtf8Bytes);

        var result = NotificationHelper.ClampForSystemMessage(text);

        result.Should().Be(text);
        Encoding.UTF8.GetByteCount(result).Should().Be(NotificationHelper.MaxSystemMessageUtf8Bytes);
    }

    [Fact]
    public void ClampForSystemMessage_UsesUtf8BytesForEmojiAndCjk()
    {
        var text = string.Concat(Enumerable.Repeat("😀界", 400));

        var result = NotificationHelper.ClampForSystemMessage(text);

        Encoding.UTF8.GetByteCount(result).Should().BeLessOrEqualTo(NotificationHelper.MaxSystemMessageUtf8Bytes);
        result.Should().EndWith("…");
        result.Should().NotContain("\uFFFD");
    }

    [Fact]
    public void ClampForSystemMessage_PreservesRichTextAsTextWithinBudget()
    {
        var text = "<color=#5CC8FF>[BattleLuck] • " + new string('a', 1000) + "</color>";

        var result = NotificationHelper.ClampForSystemMessage(text);

        Encoding.UTF8.GetByteCount(result).Should().BeLessOrEqualTo(NotificationHelper.MaxSystemMessageUtf8Bytes);
        result.Should().StartWith("<color=#5CC8FF>[BattleLuck]");
        result.Should().EndWith("…");
    }

    [Fact]
    public void ClampForSystemMessage_DoesNotLeaveAnUnpairedSurrogate()
    {
        var text = new string('a', 498) + "😀";

        var result = NotificationHelper.ClampForSystemMessage(text);

        result.Should().EndWith("…");
        for (var i = 0; i < result.Length; i++)
        {
            if (char.IsHighSurrogate(result[i]))
                (i + 1 < result.Length && char.IsLowSurrogate(result[i + 1])).Should().BeTrue();
            if (char.IsLowSurrogate(result[i]))
                (i > 0 && char.IsHighSurrogate(result[i - 1])).Should().BeTrue();
        }
    }
}
