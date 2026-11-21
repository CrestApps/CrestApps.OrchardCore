using System.Text;
using CrestApps.OrchardCore.Learning.Packages.Core.Services;

namespace CrestApps.OrchardCore.Tests.Learning.Scorm;

public sealed class PackageHtmlInjectorTests
{
    private const string Script = "<script>window.API={};</script>";

    [Theory]
    [InlineData("<html><head><title>T</title></head></html>", "<html><head><script>window.API={};</script><title>T</title></head></html>")]
    [InlineData("<!DOCTYPE html><HTML lang=\"en\"><HEAD data-x=\"1\"><title>T</title>", "<!DOCTYPE html><HTML lang=\"en\"><HEAD data-x=\"1\"><script>window.API={};</script><title>T</title>")]
    [InlineData("<html><body>No head</body></html>", "<html><script>window.API={};</script><body>No head</body></html>")]
    [InlineData("<!doctype html><p>Fragment</p>", "<!doctype html><script>window.API={};</script><p>Fragment</p>")]
    [InlineData("<p>Fragment</p>", "<script>window.API={};</script><p>Fragment</p>")]
    [InlineData("<html><header>Not the head</header><head></head>", "<html><header>Not the head</header><head><script>window.API={};</script></head>")]
    public void Script_IsInsertedFirstInTheHead(string html, string expected)
    {
        Assert.Equal(expected, Encoding.Latin1.GetString(PackageHtmlInjector.Inject(Encoding.Latin1.GetBytes(html), Script)));
    }

    [Fact]
    public void PageBytes_AreKept()
    {
        var page = Encoding.UTF8.GetBytes("<html><head><title>Élève – ✓</title></head></html>");
        var result = PackageHtmlInjector.Inject(page, Script);

        Assert.Equal("<html><head>" + Script + "<title>Élève – ✓</title></head></html>", Encoding.UTF8.GetString(result));
    }

    [Fact]
    public void ByteOrderMark_StaysFirst()
    {
        var page = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("<p>x</p>")).ToArray();
        var result = PackageHtmlInjector.Inject(page, Script);

        Assert.Equal(Encoding.UTF8.GetPreamble(), result[..3]);
        Assert.Equal(Script + "<p>x</p>", Encoding.UTF8.GetString(result[3..]));
    }
}
