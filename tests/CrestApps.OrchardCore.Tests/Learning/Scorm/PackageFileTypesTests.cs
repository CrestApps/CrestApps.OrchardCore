using CrestApps.OrchardCore.Learning.Packages.Core.Services;

namespace CrestApps.OrchardCore.Tests.Learning.Scorm;

/// <summary>
/// The kinds of file a package may hold, and the content type each is served with.
/// </summary>
public sealed class PackageFileTypesTests
{
    [Theory]
    // Cursors and icons (old Articulate players ship .cur files).
    [InlineData("story_content/cursor.cur")]
    [InlineData("favicon.ico")]

    // Office documents and PDF (Rise attachments).
    [InlineData("scormcontent/assets/Trainer Biography.docx")]
    [InlineData("assets/plan.doc")]
    [InlineData("assets/data.xlsx")]
    [InlineData("assets/data.xls")]
    [InlineData("assets/slides.pptx")]
    [InlineData("assets/slides.ppt")]
    [InlineData("assets/guide.pdf")]

    // Captions, fonts, media, images and data.
    [InlineData("captions/en.vtt")]
    [InlineData("captions/en.srt")]
    [InlineData("fonts/a.woff")]
    [InlineData("fonts/a.woff2")]
    [InlineData("fonts/a.ttf")]
    [InlineData("fonts/a.otf")]
    [InlineData("fonts/a.eot")]
    [InlineData("media/a.mp4")]
    [InlineData("media/a.webm")]
    [InlineData("media/a.mp3")]
    [InlineData("media/a.m4a")]
    [InlineData("media/a.ogg")]
    [InlineData("media/a.wav")]
    [InlineData("images/a.svg")]
    [InlineData("images/a.webp")]
    [InlineData("data/a.json")]
    [InlineData("data/a.xml")]
    [InlineData("a.xsd")]
    [InlineData("a.dtd")]
    [InlineData("a.txt")]
    [InlineData("a.csv")]
    [InlineData("INDEX.HTML")]
    public void IsAllowed_TakesTheFilesRealPackagesCarry(string path)
    {
        Assert.True(PackageFileTypes.IsAllowed(path));
    }

    [Theory]
    [InlineData("Views/Index.cshtml")]
    [InlineData("default.aspx")]
    [InlineData("handler.ashx")]
    [InlineData("service.asmx")]
    [InlineData("page.asp")]
    [InlineData("index.php")]
    [InlineData("page.jsp")]
    [InlineData("setup.exe")]
    [InlineData("bin/Lesson.dll")]
    [InlineData("run.bat")]
    [InlineData("run.cmd")]
    [InlineData("run.ps1")]
    [InlineData("run.sh")]
    [InlineData("run.vbs")]
    [InlineData("install.msi")]
    [InlineData("web.config")]
    [InlineData(".htaccess")]
    [InlineData("macro.docm")]
    [InlineData("macro.xlsm")]
    [InlineData("noextension")]
    [InlineData("")]
    [InlineData(null)]
    public void IsAllowed_RefusesServerAndProgramFiles(string path)
    {
        Assert.False(PackageFileTypes.IsAllowed(path));
    }

    [Theory]
    [InlineData("a.html", "text/html")]
    [InlineData("a.js", "text/javascript")]
    [InlineData("a.css", "text/css")]
    [InlineData("a.cur", "image/x-icon")]
    [InlineData("a.ico", "image/x-icon")]
    [InlineData("a.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [InlineData("a.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [InlineData("a.pptx", "application/vnd.openxmlformats-officedocument.presentationml.presentation")]
    [InlineData("a.doc", "application/msword")]
    [InlineData("a.xls", "application/vnd.ms-excel")]
    [InlineData("a.ppt", "application/vnd.ms-powerpoint")]
    [InlineData("a.pdf", "application/pdf")]
    [InlineData("a.vtt", "text/vtt")]
    [InlineData("a.srt", "text/plain")]
    [InlineData("a.woff", "font/woff")]
    [InlineData("a.woff2", "font/woff2")]
    [InlineData("a.ttf", "font/ttf")]
    [InlineData("a.otf", "font/otf")]
    [InlineData("a.eot", "application/vnd.ms-fontobject")]
    [InlineData("a.mp4", "video/mp4")]
    [InlineData("a.webm", "video/webm")]
    [InlineData("a.mp3", "audio/mpeg")]
    [InlineData("a.m4a", "audio/mp4")]
    [InlineData("a.ogg", "audio/ogg")]
    [InlineData("a.wav", "audio/wav")]
    [InlineData("a.svg", "image/svg+xml")]
    [InlineData("a.webp", "image/webp")]
    [InlineData("a.json", "application/json")]
    [InlineData("a.xml", "text/xml")]
    [InlineData("a.txt", "text/plain")]
    [InlineData("a.csv", "text/csv")]
    public void ContentTypeOf_GivesEachKindItsType(string path, string expected)
    {
        Assert.Equal(expected, PackageFileTypes.ContentTypeOf(path));
    }

    [Fact]
    public void ContentTypeOf_EveryAllowedKind_HasAType()
    {
        // A .bin file (the buffers of a 3D model) is plain bytes.
        foreach (var extension in PackageFileTypes.Extensions.Where(extension => extension != ".bin"))
        {
            Assert.NotEqual("application/octet-stream", PackageFileTypes.ContentTypeOf("file" + extension));
        }
    }
}
