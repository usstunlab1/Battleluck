using System.Reflection;
using BattleLuck.Services.AI;
using FluentAssertions;
using Xunit;

namespace BattleLuck.Tests.Services.AI;

public class AiActionPlannerTests
{
    static string? Extract(string text)
    {
        var mi = typeof(AiActionPlanner).GetMethod("ExtractJsonArray", BindingFlags.NonPublic | BindingFlags.Static);
        mi.Should().NotBeNull();
        return (string?)mi!.Invoke(null, new object[] { text });
    }

    [Theory]
    [InlineData("Here is your plan:```json\n[ {\\\"action\\\":\\\"foo\\\"} ]\n```Thanks!", "[ {\"action\":\"foo\"} ]")] 
    [InlineData("prefix [1,2,3] suffix", "[1,2,3]")]
    [InlineData("[ {\"action\":\"a\"}, {\"action\":\"b\"} ] trailing text", "[ {\"action\":\"a\"}, {\"action\":\"b\"} ]")]
    public void ExtractJsonArray_ResilientlyExtractsBracketArray(string input, string expected)
    {
        var actual = Extract(input);
        // Normalize possible escaped quotes differences in test literals
        actual!.Replace("\\\"", "\"").Should().Be(expected.Replace("\\\"", "\""));
    }

    [Fact]
    /* Implement this function */
    public void UnitTest1()
    {
        // Arrange
        string input = "Some text with [ {\"action\":\"unit_test\"} ] inside";

        // Act
        var result = Extract(input);

        // Assert
        result.Should().NotBeNull("because the text contains a bracketed JSON array");
        result!.Replace("\\\"", "\"").Should().Be("[ {\"action\":\"unit_test\"} ]");
    }
}
