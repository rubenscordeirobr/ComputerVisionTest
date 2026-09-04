using CameraVision.Core;
using CameraVision.Core.Entities;

namespace CameraVision.Core.Tests;

public class AnnotationColorTests
{
    [Theory]
    [InlineData("#ff3838", "#FF3838")]
    [InlineData("ff3838", "#FF3838")]
    [InlineData("  #ABC ", "#AABBCC")]
    [InlineData("0018ec", "#0018EC")]
    public void TryNormalize_accepts_hex_forms(string input, string expected)
    {
        Assert.True(AnnotationColor.TryNormalize(input, out var hex));
        Assert.Equal(expected, hex);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("#GG0000")]
    [InlineData("#12345")]
    [InlineData("red")]
    [InlineData("#FF3838FF")]
    public void TryNormalize_rejects_invalid(string? input)
    {
        Assert.False(AnnotationColor.TryNormalize(input, out var hex));
        Assert.Null(hex);
    }

    [Fact]
    public void Sanitize_drops_invalid_entries_and_normalizes_valid_ones()
    {
        var result = AnnotationColor.Sanitize(new Dictionary<string, string?>
        {
            ["person"] = "ff3838",
            ["car"] = "",
            ["dog"] = "not a color",
            [""] = "#123456",
        });

        Assert.Equal(new Dictionary<string, string> { ["person"] = "#FF3838" }, result);
        Assert.True(result.ContainsKey("PERSON")); // case-insensitive keys, like COCO names in rules
    }

    [Fact]
    public void CaptureRule_ColorFor_returns_null_without_a_configured_color()
    {
        var rule = new CaptureRule { Classes = ["person", "car"], ClassColors = { ["person"] = "#FF3838" } };

        Assert.Equal("#FF3838", rule.ColorFor("person"));
        Assert.Equal("#FF3838", rule.ColorFor("Person"));
        Assert.Null(rule.ColorFor("car"));
    }
}
