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

    [Theory]
    [InlineData("status")]
    [InlineData("Status das câmeras")]
    [InlineData("você poderia me informar a saúde das câmeras?")]
    [InlineData("voce poderia me informar a sudade da camaras")]
    [InlineData("como estão as câmeras?")]
    [InlineData("o processador está funcionando?")]
    [InlineData("as câmeras estão ligadas?")]
    [InlineData("a câmera da garagem caiu?")]
    public void Status_phrases(string text)
    {
        var result = CommandTextRules.TryMatch(text, Now);
        Assert.NotNull(result);
        Assert.Equal(CommandIntent.CameraStatus, result.Intent);
    }

    [Fact]
    public void Captures_with_count_and_class()
    {
        var result = CommandTextRules.TryMatch("vc pode me enviar a lista das últimas 3 capturas de pessoas", Now);
        Assert.NotNull(result);
        Assert.Equal(CommandIntent.ListCaptures, result.Intent);
        Assert.Equal(3, result.Count);
        Assert.Equal("person", result.ObjectClass);
        Assert.Null(result.UnknownClass);
    }

    [Theory]
    [InlineData("últimas capturas")]
    [InlineData("quero ver as capturas de hoje")]
    [InlineData("me manda os últimos vídeos")]
    public void Captures_without_count_or_class(string text)
    {
        var result = CommandTextRules.TryMatch(text, Now);
        Assert.NotNull(result);
        Assert.Equal(CommandIntent.ListCaptures, result.Intent);
        Assert.Null(result.Count);
        Assert.Null(result.ObjectClass);
        Assert.Null(result.UnknownClass);
    }

    [Fact]
    public void Captures_keep_the_raw_count_and_resolve_plurals()
    {
        var result = CommandTextRules.TryMatch("últimas 50 capturas de gatos", Now);
        Assert.NotNull(result);
        Assert.Equal(50, result.Count);
        Assert.Equal("cat", result.ObjectClass);

        var words = CommandTextRules.TryMatch("mostra as duas últimas capturas de cães de hoje", Now);
        Assert.NotNull(words);
        Assert.Equal(2, words.Count);
        Assert.Equal("dog", words.ObjectClass);
    }

    [Fact]
    public void Captures_of_an_unknown_object_report_the_word()
    {
        var result = CommandTextRules.TryMatch("últimas capturas de dinossauros", Now);
        Assert.NotNull(result);
        Assert.Equal(CommandIntent.ListCaptures, result.Intent);
        Assert.Null(result.ObjectClass);
        Assert.Equal("dinossauros", result.UnknownClass);
        Assert.True(result.Tentative);
    }

    [Theory]
    [InlineData("últimas 5 capturas de pessoas de camisa amarela", "person", 5)]
    [InlineData("mostra as capturas de carros na garagem", "car", null)]
    [InlineData("quero ver as capturas de pessoas e cachorros", "person", null)]
    public void Captures_with_unread_words_are_tentative(string text, string objectClass, int? count)
    {
        var result = CommandTextRules.TryMatch(text, Now);
        Assert.NotNull(result);
        Assert.Equal(CommandIntent.ListCaptures, result.Intent);
        Assert.Equal(objectClass, result.ObjectClass);
        Assert.Equal(count, result.Count);
        Assert.True(result.Tentative);
    }

    [Theory]
    [InlineData("últimas 5 capturas de pessoas")]
    [InlineData("quero ver as capturas de gatos de hoje")]
    [InlineData("mostra as duas últimas capturas de cães de hoje")]
    [InlineData("últimas capturas")]
    [InlineData("status")]
    [InlineData("ativar alertas")]
    public void Fully_read_messages_are_not_tentative(string text)
    {
        var result = CommandTextRules.TryMatch(text, Now);
        Assert.NotNull(result);
        Assert.False(result.Tentative);
    }

    [Fact]
    public void Read_only_intents_are_flagged()
    {
        Assert.True(CommandTextRules.TryMatch("status", Now)!.IsReadOnly);
        Assert.False(CommandTextRules.TryMatch("ativar alertas", Now)!.IsReadOnly);
    }

    [Fact]
    public void Fold_strips_accents_and_punctuation() =>
        Assert.Equal("ate as 22:30 nao", CommandTextRules.Fold("  Até às 22:30, NÃO!  "));

    [Theory]
    [InlineData("sim")]
    [InlineData("Sim!")]
    [InlineData("pode mandar")]
    [InlineData("ok, quero")]
    [InlineData("manda aí 👍")]
    [InlineData("👍")]
    [InlineData("por favor")]
    public void Confirmation_yes(string text) =>
        Assert.True(CommandTextRules.TryMatchConfirmation(text));

    [Theory]
    [InlineData("não")]
    [InlineData("Não, obrigado")]
    [InlineData("deixa pra lá")]
    [InlineData("agora não")]
    [InlineData("n")]
    public void Confirmation_no(string text) =>
        Assert.False(CommandTextRules.TryMatchConfirmation(text));

    [Theory]
    [InlineData("status")]
    [InlineData("manda as capturas de carros")]
    [InlineData("não quero mais alertas")]
    [InlineData("sim, mas só as de hoje e as de ontem")]
    [InlineData("bom dia")]
    [InlineData("")]
    public void Confirmation_undecided(string text) =>
        Assert.Null(CommandTextRules.TryMatchConfirmation(text));
}
