using CameraVision.Core.Commands;

namespace CameraVision.Core.Tests;

public class CommandTextRulesTests
{
    private static readonly DateTime Now = new(2026, 9, 7, 14, 0, 0);

    [Theory]
    [InlineData("Você poderia ativar os alertas")]
    [InlineData("ativar alertas")]
    [InlineData("ATIVAR ALERTAS!!")]
    [InlineData("liga os avisos por favor")]
    [InlineData("habilite as notificações")]
    [InlineData("quero receber os alertas")]
    [InlineData("Ativa")]
    public void Enable_phrases(string text)
    {
        var result = CommandTextRules.TryMatch(text, Now);
        Assert.NotNull(result);
        Assert.Equal(CommandIntent.EnableAlerts, result.Intent);
        Assert.False(result.HasDuration);
    }

    [Theory]
    [InlineData("Desativar alertas")]
    [InlineData("desligue os avisos")]
    [InlineData("pode parar os alertas")]
    [InlineData("cancelar as notificações")]
    [InlineData("não quero mais receber alertas")]
    [InlineData("desativa")]
    public void Disable_phrases(string text)
    {
        var result = CommandTextRules.TryMatch(text, Now);
        Assert.NotNull(result);
        Assert.Equal(CommandIntent.DisableAlerts, result.Intent);
    }

    [Fact]
    public void Enable_with_hours_carries_the_end()
    {
        var result = CommandTextRules.TryMatch("ativar alertas por 2 horas", Now);
        Assert.NotNull(result);
        Assert.Equal(CommandIntent.EnableAlerts, result.Intent);
        Assert.Equal(Now.AddHours(2), result.Until);
    }

    [Fact]
    public void Enable_until_disabled()
    {
        var result = CommandTextRules.TryMatch("ativar os alertas até eu desativar", Now);
        Assert.NotNull(result);
        Assert.Equal(CommandIntent.EnableAlerts, result.Intent);
        Assert.True(result.UntilDisabled);
        Assert.Null(result.Until);
    }

    [Theory]
    [InlineData("bom dia")]
    [InlineData("não quero ativar os alertas")]
    [InlineData("ativar e desativar")]
    [InlineData("a câmera da garagem está ativa, mas o vídeo não abre")]
    [InlineData("")]
    public void Ambiguous_or_unrelated_is_null(string text) =>
        Assert.Null(CommandTextRules.TryMatch(text, Now));

    [Fact]
    public void Bare_duration_is_set_duration_only_when_expected()
    {
        Assert.Null(CommandTextRules.TryMatch("2 horas", Now));

        var result = CommandTextRules.TryMatch("2 horas", Now, expectingDuration: true);
        Assert.NotNull(result);
        Assert.Equal(CommandIntent.SetDuration, result.Intent);
        Assert.Equal(Now.AddHours(2), result.Until);
    }

    [Fact]
    public void Fold_strips_accents_and_punctuation() =>
        Assert.Equal("ate as 22:30 nao", CommandTextRules.Fold("  Até às 22:30, NÃO!  "));
}
