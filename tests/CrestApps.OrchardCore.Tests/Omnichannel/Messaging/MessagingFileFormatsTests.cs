using System.Text;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Attachments;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging;

/// <summary>
/// A file is kept, and served back, only as a format its channel carries, and wherever the format has a signature,
/// only as the format its own bytes declare. A file that merely claims to be a picture, such as an SVG or HTML page
/// renamed to .png, must never be stored and shown inline as one.
/// </summary>
public sealed class MessagingFileFormatsTests
{
    public static TheoryData<byte[], string, string> Pictures => new()
    {
        { TestImages.Jpeg, "a.jpg", "image/jpeg" },
        { TestImages.Png, "a.png", "image/png" },
        { TestImages.Gif, "a.gif", "image/gif" },
        { TestImages.WebP, "a.webp", "image/webp" },
    };

    [Theory]
    [MemberData(nameof(Pictures))]
    public void Detect_RecognisesEachPictureByItsBytes(byte[] content, string fileName, string expected)
    {
        Assert.Equal(expected, MessagingFileFormats.Detect(content, fileName, null, MessagingFileFormats.Images)?.ContentType);
    }

    [Fact]
    public void Detect_GoesByTheBytes_NotTheName()
    {
        // A PNG named .jpg is a PNG; a picture with no name at all is still recognised.
        Assert.Equal(MessagingFileFormats.Png, MessagingFileFormats.Detect(TestImages.Png, "photo.jpg", "image/jpeg", MessagingFileFormats.Images));
        Assert.Equal(MessagingFileFormats.Png, MessagingFileFormats.Detect(TestImages.Png, null, null, MessagingFileFormats.Images));
    }

    [Theory]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>", "cat.png")]
    [InlineData("<!DOCTYPE html><html><body>hi</body></html>", "cat.png")]
    [InlineData("BEGIN:VCARD", "card.vcf")]
    public void Detect_RefusesAFileThatIsNotAPicture_OnAChannelThatCarriesOnlyPictures(string content, string fileName)
    {
        Assert.Null(MessagingFileFormats.Detect(Encoding.UTF8.GetBytes(content), fileName, "image/png", MessagingFileFormats.Images));
    }

    [Fact]
    public void Detect_RefusesAFormatTheChannelDoesNotCarry_EvenWhenItIsGenuine()
    {
        // SMS carries pictures only, so a real PDF is refused there; a channel that lists documents takes it.
        var pdf = "%PDF-1.7\n"u8.ToArray();

        Assert.Null(MessagingFileFormats.Detect(pdf, "form.pdf", "application/pdf", MessagingFileFormats.Images));
        Assert.Equal(MessagingFileFormats.Pdf, MessagingFileFormats.Detect(pdf, "form.pdf", "application/pdf", MessagingFileFormats.All));
    }

    [Fact]
    public void Detect_TellsTheOfficeFormatsApartByExtension()
    {
        // Every Office document is a ZIP file, so the bytes alone cannot say which one it is.
        var zip = new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x14, 0x00 };

        Assert.Equal(MessagingFileFormats.Word, MessagingFileFormats.Detect(zip, "report.docx", null, MessagingFileFormats.All));
        Assert.Equal(MessagingFileFormats.Excel, MessagingFileFormats.Detect(zip, "sheet.xlsx", null, MessagingFileFormats.All));
        Assert.Null(MessagingFileFormats.Detect(zip, "archive.zip", null, MessagingFileFormats.All));
    }

    [Fact]
    public void Detect_RecognisesAFormatWithNoSignatureByItsExtension_ButNeverAsAPictureInDisguise()
    {
        Assert.Equal(MessagingFileFormats.Text, MessagingFileFormats.Detect("hello"u8, "notes.txt", null, MessagingFileFormats.Documents));

        // A picture renamed .txt carries its picture signature, so it is not accepted as text.
        Assert.Null(MessagingFileFormats.Detect(TestImages.Png, "notes.txt", "text/plain", MessagingFileFormats.Documents));
    }

    [Fact]
    public void Detect_WithoutAName_UsesTheDeclaredTypeOnlyForAFormatWithNoSignature()
    {
        Assert.Equal(MessagingFileFormats.VCard, MessagingFileFormats.Detect("BEGIN:VCARD"u8, null, "text/vcard; charset=utf-8", MessagingFileFormats.All));
    }

    [Fact]
    public void Detect_RefusesAnythingWhenTheChannelCarriesNoFiles()
    {
        Assert.Null(MessagingFileFormats.Detect(TestImages.Png, "a.png", "image/png", []));
    }

    [Fact]
    public void OnlyPicturesAreShownInline_AndOnlyStillPicturesAreShrunk()
    {
        Assert.All(MessagingFileFormats.Images, format => Assert.True(format.IsImage));
        Assert.All(MessagingFileFormats.Documents, format => Assert.False(format.IsImage));
        Assert.False(MessagingFileFormats.Gif.CanShrink);
        Assert.True(MessagingFileFormats.Jpeg.CanShrink);
    }
}
