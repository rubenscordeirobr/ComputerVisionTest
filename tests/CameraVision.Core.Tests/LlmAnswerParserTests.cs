using CameraVision.Core.Commands;

namespace CameraVision.Core.Tests;

public class LlmAnswerParserTests
{
    private static readonly DateTime Now = new(2026, 9, 7, 14, 0, 0);

    [Fact]
    public void Unsupported_with_request_and_fallback()
    {
        const string json = """
            {"intent": "unsupported", "until": null, "until_disabled": false, "count": null, "object_class": null,
             "request": "ver as últimas 5 capturas de pessoas de camisa amarela",
             "fallback": {"intent": "list_captures", "count": 5, "object_class": "person"}}
            """;
        var result = LlmAnswerParser.Parse(json, Now);
        Assert.NotNull(result);
        Assert.Equal(CommandIntent.Unsupported, result.Intent);
        Assert.Equal("llm", result.Source);
        Assert.Equal("ver as últimas 5 capturas de pessoas de camisa amarela", result.Request);
        Assert.NotNull(result.Fallback);
        Assert.Equal(CommandIntent.ListCaptures, result.Fallback.Intent);
        Assert.Equal(5, result.Fallback.Count);
        Assert.Equal("person", result.Fallback.ObjectClass);
        Assert.True(result.Fallback.IsExecutable);
    }

    [Fact]
    public void Fallback_class_goes_through_the_resolver()
    {
        var result = LlmAnswerParser.Parse(
            """{"intent":"unsupported","request":"ver capturas de cachorros de coleira","fallback":{"intent":"list_captures","object_class":"cachorros"}}""",
            Now);
        Assert.NotNull(result?.Fallback);
        Assert.Equal("dog", result.Fallback.ObjectClass);
        Assert.Null(result.Fallback.Count);
    }

    [Fact]
    public void Unsupported_without_request_is_unknown()
    {
        var result = LlmAnswerParser.Parse(
            """{"intent":"unsupported","request":"  ","fallback":{"intent":"camera_status"}}""", Now);
        Assert.NotNull(result);
        Assert.Equal(CommandIntent.Unknown, result.Intent);
        Assert.Equal("llm", result.Source);
        Assert.Null(result.Fallback);
    }

    [Theory]
    [InlineData("set_duration")]
    [InlineData("unknown")]
    [InlineData("unsupported")]
    [InlineData("confirm")]
    public void Fallback_must_be_a_runnable_command(string intent)
    {
        var result = LlmAnswerParser.Parse(
            $$$"""{"intent":"unsupported","request":"abrir o portão","fallback":{"intent":"{{{intent}}}","until":"2026-09-07T16:00"}}""",
            Now);
        Assert.NotNull(result);
        Assert.Equal(CommandIntent.Unsupported, result.Intent);
        Assert.Equal("abrir o portão", result.Request);
        Assert.Null(result.Fallback);
    }

    [Fact]
    public void Fallback_enable_keeps_its_validity()
    {
        var result = LlmAnswerParser.Parse(
            """{"intent":"unsupported","request":"receber os alertas só da garagem por 2 horas","fallback":{"intent":"enable","until":"2026-09-07T16:00","until_disabled":false}}""",
            Now);
        Assert.NotNull(result?.Fallback);
        Assert.Equal(CommandIntent.EnableAlerts, result.Fallback.Intent);
        Assert.Equal(Now.AddHours(2), result.Fallback.Until);
    }

    [Theory]
    [InlineData("confirm", CommandIntent.Confirm)]
    [InlineData("decline", CommandIntent.Decline)]
    [InlineData("camera_status", CommandIntent.CameraStatus)]
    [InlineData("whatever", CommandIntent.Unknown)]
    public void Simple_intents(string intent, CommandIntent expected)
    {
        var result = LlmAnswerParser.Parse($$"""{"intent": "{{intent}}"}""", Now);
        Assert.NotNull(result);
        Assert.Equal(expected, result.Intent);
        Assert.Equal("llm", result.Source);
    }

    [Fact]
    public void Code_fences_and_extra_fields_are_tolerated()
    {
        var result = LlmAnswerParser.Parse(
            "```json\n{\"intent\": \"enable\", \"until\": \"2026-09-07T22:00\", \"until_disabled\": false, \"note\": \"x\"}\n```",
            Now);
        Assert.NotNull(result);
        Assert.Equal(CommandIntent.EnableAlerts, result.Intent);
        Assert.Equal(new DateTime(2026, 9, 7, 22, 0, 0), result.Until);
    }

    [Fact]
    public void Past_until_is_dropped_and_bare_set_duration_is_unknown()
    {
        var enable = LlmAnswerParser.Parse("""{"intent":"enable","until":"2026-09-07T10:00"}""", Now);
        Assert.NotNull(enable);
        Assert.Equal(CommandIntent.EnableAlerts, enable.Intent);
        Assert.Null(enable.Until);

        var duration = LlmAnswerParser.Parse("""{"intent":"set_duration","until":null,"until_disabled":false}""", Now);
        Assert.NotNull(duration);
        Assert.Equal(CommandIntent.Unknown, duration.Intent);
    }

    [Fact]
    public void List_captures_with_unknown_class_reports_the_word()
    {
        var result = LlmAnswerParser.Parse("""{"intent":"list_captures","count":3,"object_class":"dinossauro"}""", Now);
        Assert.NotNull(result);
        Assert.Equal(CommandIntent.ListCaptures, result.Intent);
        Assert.Equal(3, result.Count);
        Assert.Null(result.ObjectClass);
        Assert.Equal("dinossauro", result.UnknownClass);
    }

    [Theory]
    [InlineData("no json here")]
    [InlineData("{not json}")]
    [InlineData("")]
    public void Garbage_is_null(string raw) => Assert.Null(LlmAnswerParser.Parse(raw, Now));
}
