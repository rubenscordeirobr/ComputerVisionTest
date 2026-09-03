using CameraVision.Core.Commands;

namespace CameraVision.Core.Tests;

public class DetectableClassResolverTests
{
    [Theory]
    [InlineData("pessoas", "person")]
    [InlineData("pessoa", "person")]
    [InlineData("gente", "person")]
    [InlineData("Pessoas", "person")]
    [InlineData("gatos", "cat")]
    [InlineData("cães", "dog")]
    [InlineData("cachorros", "dog")]
    [InlineData("carros", "car")]
    [InlineData("veículos", "car")]
    [InlineData("motos", "motorcycle")]
    [InlineData("caminhões", "truck")]
    [InlineData("ônibus", "bus")]
    [InlineData("pássaros", "bird")]
    [InlineData("bicicletas", "bicycle")]
    [InlineData("celulares", "cell phone")]
    [InlineData("person", "person")]
    [InlineData("dogs", "dog")]
    public void Resolves_pt_br_words_and_english_names(string word, string expected) =>
        Assert.Equal(expected, DetectableClassResolver.TryResolve(word));

    [Theory]
    [InlineData("dinossauros")]
    [InlineData("hoje")]
    [InlineData("")]
    [InlineData(" ")]
    public void Unknown_words_return_null(string word) =>
        Assert.Null(DetectableClassResolver.TryResolve(word));

    [Theory]
    [InlineData("caes", "cao")]
    [InlineData("caminhoes", "caminhao")]
    [InlineData("animais", "animal")]
    [InlineData("homens", "homem")]
    [InlineData("carros", "carro")]
    [InlineData("gas", "gas")]
    public void Singular_forms(string plural, string singular) =>
        Assert.Equal(singular, DetectableClassResolver.Singular(plural));
}
