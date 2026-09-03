using CameraVision.Core.WhatsApp;

namespace CameraVision.Core.Tests;

public class WhatsAppNumberMatcherTests
{
    [Fact]
    public void Brazilian_number_without_ninth_digit_also_matches_with_it() =>
        Assert.Equal(["+554988887777", "+5549988887777"], WhatsAppNumberMatcher.Candidates("554988887777"));

    [Fact]
    public void Brazilian_number_with_ninth_digit_also_matches_without_it() =>
        Assert.Equal(["+5549988887777", "+554988887777"], WhatsAppNumberMatcher.Candidates("5549988887777"));

    [Fact]
    public void Foreign_number_has_one_candidate() =>
        Assert.Equal(["+14155552671"], WhatsAppNumberMatcher.Candidates("14155552671"));

    [Fact]
    public void Invalid_number_has_none() => Assert.Empty(WhatsAppNumberMatcher.Candidates("123"));
}
