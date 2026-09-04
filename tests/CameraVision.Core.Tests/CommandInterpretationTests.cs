using CameraVision.Core.Commands;

namespace CameraVision.Core.Tests;

public class CommandInterpretationTests
{
    [Fact]
    public void Unsupported_cleans_the_request()
    {
        var result = CommandInterpretation.Unsupported("  \"ver as\n capturas   de ontem.\" ", "llm");
        Assert.Equal(CommandIntent.Unsupported, result.Intent);
        Assert.Equal("ver as capturas de ontem", result.Request);
        Assert.Equal("llm", result.Source);
        Assert.Null(result.Fallback);
    }

    [Fact]
    public void Unsupported_caps_the_request()
    {
        var result = CommandInterpretation.Unsupported(new string('a', 300), "llm");
        Assert.NotNull(result.Request);
        Assert.Equal(CommandInterpretation.MaxRequestLength, result.Request.Length);
        Assert.EndsWith("…", result.Request);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  \"\" ")]
    public void Unsupported_without_a_request_is_unknown(string? request)
    {
        var result = CommandInterpretation.Unsupported(request, "llm", new CommandInterpretation(CommandIntent.CameraStatus));
        Assert.Equal(CommandIntent.Unknown, result.Intent);
        Assert.Equal("llm", result.Source);
        Assert.Null(result.Fallback);
    }

    [Fact]
    public void Only_runnable_fallbacks_are_kept()
    {
        var runnable = CommandInterpretation.Unsupported("abrir o portão", "llm",
            new CommandInterpretation(CommandIntent.ListCaptures, Count: 3, ObjectClass: "person", Request: "x"));
        Assert.NotNull(runnable.Fallback);
        Assert.Equal(3, runnable.Fallback.Count);
        Assert.Null(runnable.Fallback.Request);

        var notRunnable = CommandInterpretation.Unsupported("abrir o portão", "llm",
            new CommandInterpretation(CommandIntent.SetDuration, Until: DateTime.Now.AddHours(1)));
        Assert.Equal(CommandIntent.Unsupported, notRunnable.Intent);
        Assert.Null(notRunnable.Fallback);
    }

    [Fact]
    public void Json_round_trip_keeps_the_offer()
    {
        var offer = new CommandInterpretation(CommandIntent.ListCaptures, Source: "llm", Count: 5, ObjectClass: "person");
        Assert.Equal(offer, CommandInterpretation.TryFromJson(offer.ToJson()));

        var enable = new CommandInterpretation(CommandIntent.EnableAlerts, new DateTime(2026, 9, 7, 22, 0, 0), Source: "llm");
        Assert.Equal(enable, CommandInterpretation.TryFromJson(enable.ToJson()));

        var untilDisabled = new CommandInterpretation(CommandIntent.EnableAlerts, UntilDisabled: true);
        Assert.Equal(untilDisabled, CommandInterpretation.TryFromJson(untilDisabled.ToJson()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[1,2]")]
    public void Bad_json_is_null(string? json) => Assert.Null(CommandInterpretation.TryFromJson(json));
}
