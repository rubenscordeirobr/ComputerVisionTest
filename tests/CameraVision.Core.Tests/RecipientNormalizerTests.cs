using CameraVision.Core.Alerts;
using CameraVision.Core.Entities;

namespace CameraVision.Core.Tests;

public class RecipientNormalizerTests
{
    [Theory]
    [InlineData(" A@X.com ", "a@x.com")]
    [InlineData("Rubens.Cordeiro@Example.COM", "rubens.cordeiro@example.com")]
    public void Email_is_trimmed_and_lower_cased(string raw, string expected) =>
        Assert.Equal(expected, RecipientNormalizer.NormalizeEmail(raw));

    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a b@x.com")]
    [InlineData("@x.com")]
    public void Invalid_email_is_rejected(string raw) =>
        Assert.Null(RecipientNormalizer.NormalizeEmail(raw));

    [Theory]
    [InlineData("+55 (49) 99999-9999", "+5549999999999")]
    [InlineData("5549999999999", "+5549999999999")]
    [InlineData("+5542998373996", "+5542998373996")]
    public void Phone_keeps_digits_only_with_a_plus_prefix(string raw, string expected) =>
        Assert.Equal(expected, RecipientNormalizer.NormalizePhone(raw));

    [Theory]
    [InlineData("12345")]
    [InlineData("")]
    [InlineData("+1234567890123456")]
    public void Invalid_phone_is_rejected(string raw) =>
        Assert.Null(RecipientNormalizer.NormalizePhone(raw));

    [Fact]
    public void Normalize_dispatches_by_channel()
    {
        Assert.Equal("a@x.com", RecipientNormalizer.Normalize(AlertChannel.Email, "A@X.com"));
        Assert.Equal("+5549999999999", RecipientNormalizer.Normalize(AlertChannel.WhatsApp, "55 49 99999 9999"));
        Assert.Null(RecipientNormalizer.Normalize(AlertChannel.Email, null));
        Assert.Null(RecipientNormalizer.Normalize(AlertChannel.WhatsApp, "  "));
    }
}
