using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Attachments;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging;

/// <summary>
/// A picture is kept, and served back, only as the format its own bytes declare. A file that merely claims to be a
/// picture, such as an SVG or an HTML page renamed to .png, must never be stored and served as one.
/// </summary>
public sealed class MessagingImageFormatTests
{
    public static TheoryData<byte[], string> Pictures => new()
    {
        { TestImages.Jpeg, MessagingImageFormat.Jpeg },
        { TestImages.Png, MessagingImageFormat.Png },
        { TestImages.Gif, MessagingImageFormat.Gif },
        { TestImages.WebP, MessagingImageFormat.WebP },
    };

    [Theory]
    [MemberData(nameof(Pictures))]
    public void TryDetect_RecognisesEachSupportedFormat(byte[] content, string expected)
    {
        Assert.True(MessagingImageFormat.TryDetect(content, out var contentType));
        Assert.Equal(expected, contentType);
    }

    [Theory]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>")]
    [InlineData("<!DOCTYPE html><html><body>hi</body></html>")]
    [InlineData("BEGIN:VCARD")]
    [InlineData("")]
    public void TryDetect_RefusesContentThatIsNotAPicture(string content)
    {
        Assert.False(MessagingImageFormat.TryDetect(System.Text.Encoding.UTF8.GetBytes(content), out var contentType));
        Assert.Null(contentType);
    }

    [Fact]
    public void TryDetect_RefusesARiffFileThatIsNotWebP()
    {
        Assert.False(MessagingImageFormat.TryDetect("RIFF$\u0000\u0000\u0000WAVEfmt "u8, out _));
    }
}
