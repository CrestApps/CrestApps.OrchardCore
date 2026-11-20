using System.Text;
using CrestApps.OrchardCore.Attachments.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Attachments;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging;

/// <summary>
/// A file is kept, and served back, only as a format its channel carries, and wherever the format has a signature,
/// only as the format its own bytes declare. A file that merely claims to be a picture, such as an SVG or HTML page
/// renamed to .png, must never be stored and shown inline as one.
/// </summary>
public sealed class FileFormatsTests
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
        Assert.Equal(expected, FileFormats.Detect(content, fileName, null, FileFormats.Images)?.ContentType);
    }

    [Fact]
    public void Detect_GoesByTheBytes_NotTheName()
    {
        // A PNG named .jpg is a PNG; a picture with no name at all is still recognised.
        Assert.Equal(FileFormats.Png, FileFormats.Detect(TestImages.Png, "photo.jpg", "image/jpeg", FileFormats.Images));
        Assert.Equal(FileFormats.Png, FileFormats.Detect(TestImages.Png, null, null, FileFormats.Images));
    }

    [Theory]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>", "cat.png")]
    [InlineData("<!DOCTYPE html><html><body>hi</body></html>", "cat.png")]
    [InlineData("BEGIN:VCARD", "card.vcf")]
    public void Detect_RefusesAFileThatIsNotAPicture_OnAChannelThatCarriesOnlyPictures(string content, string fileName)
    {
        Assert.Null(FileFormats.Detect(Encoding.UTF8.GetBytes(content), fileName, "image/png", FileFormats.Images));
    }

    [Fact]
    public void Detect_RefusesAFormatTheChannelDoesNotCarry_EvenWhenItIsGenuine()
    {
        // SMS carries pictures only, so a real PDF is refused there; a channel that lists documents takes it.
        var pdf = "%PDF-1.7\n"u8.ToArray();

        Assert.Null(FileFormats.Detect(pdf, "form.pdf", "application/pdf", FileFormats.Images));
        Assert.Equal(FileFormats.Pdf, FileFormats.Detect(pdf, "form.pdf", "application/pdf", FileFormats.All));
    }

    [Fact]
    public void Detect_TellsTheOfficeFormatsApartByExtension()
    {
        // Every Office document is a ZIP file, so the bytes alone cannot say which one it is.
        var zip = new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x14, 0x00 };

        Assert.Equal(FileFormats.Word, FileFormats.Detect(zip, "report.docx", null, FileFormats.All));
        Assert.Equal(FileFormats.Excel, FileFormats.Detect(zip, "sheet.xlsx", null, FileFormats.All));
        Assert.Null(FileFormats.Detect(zip, "archive.zip", null, FileFormats.All));
    }

    [Fact]
    public void Detect_RecognisesAFormatWithNoSignatureByItsExtension_ButNeverAsAPictureInDisguise()
    {
        Assert.Equal(FileFormats.Text, FileFormats.Detect("hello"u8, "notes.txt", null, FileFormats.Documents));

        // A picture renamed .txt carries its picture signature, so it is not accepted as text.
        Assert.Null(FileFormats.Detect(TestImages.Png, "notes.txt", "text/plain", FileFormats.Documents));
    }

    [Fact]
    public void Detect_WithoutAName_UsesTheDeclaredTypeOnlyForAFormatWithNoSignature()
    {
        Assert.Equal(FileFormats.VCard, FileFormats.Detect("BEGIN:VCARD"u8, null, "text/vcard; charset=utf-8", FileFormats.All));
    }

    [Fact]
    public void Detect_RefusesAnythingWhenTheChannelCarriesNoFiles()
    {
        Assert.Null(FileFormats.Detect(TestImages.Png, "a.png", "image/png", []));
    }

    [Fact]
    public void OnlyPicturesAreShownInline_AndOnlyStillPicturesAreShrunk()
    {
        Assert.All(FileFormats.Images, format => Assert.True(format.IsImage));
        Assert.All(FileFormats.Documents, format => Assert.False(format.IsImage));
        Assert.False(FileFormats.Gif.CanShrink);
        Assert.True(FileFormats.Jpeg.CanShrink);
    }
}
