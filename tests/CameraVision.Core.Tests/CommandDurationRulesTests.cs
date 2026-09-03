using CameraVision.Core.Commands;

namespace CameraVision.Core.Tests;

public class CommandDurationRulesTests
{
    // Monday 2026-09-07 14:00.
    private static readonly DateTime Now = new(2026, 9, 7, 14, 0, 0);

    private static CommandDuration Parse(string text) =>
        CommandDurationRules.TryParse(CommandTextRules.Fold(text), Now)
        ?? throw new Xunit.Sdk.XunitException($"\"{text}\" did not parse.");

    [Theory]
    [InlineData("por 2 horas", 2)]
    [InlineData("2h", 2)]
    [InlineData("3 hrs", 3)]
    [InlineData("por uma hora", 1)]
    [InlineData("duas horas", 2)]
    public void Relative_hours(string text, int hours) =>
        Assert.Equal(Now.AddHours(hours), Parse(text).Until);

    [Fact]
    public void Half_an_hour() => Assert.Equal(Now.AddMinutes(30), Parse("meia hora").Until);

    [Fact]
    public void Minutes() => Assert.Equal(Now.AddMinutes(45), Parse("45 minutos").Until);

    [Theory]
    [InlineData("até as 22h", 22, 0)]
    [InlineData("até às 22:30", 22, 30)]
    [InlineData("até 22h", 22, 0)]
    [InlineData("até 22h15", 22, 15)]
    [InlineData("ate 18:00", 18, 0)]
    public void Clock_time_later_today(string text, int hour, int minute) =>
        Assert.Equal(Now.Date.AddHours(hour).AddMinutes(minute), Parse(text).Until);

    [Fact]
    public void Clock_time_already_past_means_tomorrow() =>
        Assert.Equal(Now.Date.AddDays(1).AddHours(8), Parse("até as 8h").Until);

    [Fact]
    public void Until_two_hours_is_a_duration_not_a_clock_time() =>
        Assert.Equal(Now.AddHours(2), Parse("até 2 horas").Until);

    [Fact]
    public void Tomorrow_defaults_to_eight() =>
        Assert.Equal(Now.Date.AddDays(1).AddHours(8), Parse("até amanhã").Until);

    [Fact]
    public void Tomorrow_with_hour() =>
        Assert.Equal(Now.Date.AddDays(1).AddHours(6), Parse("até amanhã às 6h").Until);

    [Theory]
    [InlineData("hoje")]
    [InlineData("até o fim do dia")]
    [InlineData("até meia-noite")]
    public void End_of_day(string text) => Assert.Equal(Now.Date.AddDays(1), Parse(text).Until);

    [Theory]
    [InlineData("até eu desativar")]
    [InlineData("até desativar")]
    [InlineData("sem prazo")]
    [InlineData("até segunda ordem")]
    public void Open_ended(string text)
    {
        var duration = Parse(text);
        Assert.True(duration.UntilDisabled);
        Assert.Null(duration.Until);
    }

    [Theory]
    [InlineData("ativar alertas")]
    [InlineData("bom dia")]
    [InlineData("até as 25h")]
    public void No_duration(string text) =>
        Assert.Null(CommandDurationRules.TryParse(CommandTextRules.Fold(text), Now));
}
