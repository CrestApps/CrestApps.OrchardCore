using System.Text.Encodings.Web;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;
using Microsoft.AspNetCore.Http;
using Moq;
using Shortcodes;

namespace CrestApps.OrchardCore.Tests.Surveys.Authoring;

/// <summary>
/// R11-L1: the <c>[survey]</c> shortcode takes a collector's token, never a survey's name, so a content author with no
/// Surveys permission can't print the link of any open survey from its technical name.
/// </summary>
public sealed class SurveyShortcodeTests : IAsyncLifetime
{
    private const string Token = "tok-public-1";

    private ResultsSqliteStore _store;

    public async ValueTask InitializeAsync()
        => _store = await ResultsSqliteStore.CreateAsync();

    public async ValueTask DisposeAsync()
        => await _store.DisposeAsync();

    [Fact]
    public async Task Shortcode_WithASurveysTechnicalName_PrintsNoLink()
    {
        // Arrange
        await SaveSurveyAsync(SurveyCollectorKinds.WebLink);
        await using var session = _store.OpenSession();

        // Act
        var html = await Processor(session).EvaluateAsync("[survey name=\"employee-engagement\"]");
        var positional = await Processor(session).EvaluateAsync("[survey employee-engagement]");

        // Assert
        Assert.DoesNotContain(Token, html, StringComparison.Ordinal);
        Assert.DoesNotContain("/s/", html, StringComparison.Ordinal);
        Assert.DoesNotContain("/s/", positional, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Shortcode_WithACollectorsToken_PrintsItsLink()
    {
        // Arrange
        await SaveSurveyAsync(SurveyCollectorKinds.WebLink);
        await using var session = _store.OpenSession();

        // Act
        var html = await Processor(session).EvaluateAsync($"[survey token=\"{Token}\" display=\"link\"]Take it[/survey]");

        // Assert
        Assert.Contains("href=\"/s/" + Token + "\"", html, StringComparison.Ordinal);
        Assert.Contains(">Take it</a>", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(SurveyCollectorKinds.Invitation)]
    [InlineData(SurveyCollectorKinds.Kiosk)]
    public async Task Shortcode_WithTheTokenOfALinkThatIsNeverEmbedded_PrintsNoLink(string kind)
    {
        // Arrange
        await SaveSurveyAsync(kind);
        await using var session = _store.OpenSession();

        // Act
        var html = await Processor(session).EvaluateAsync($"[survey token=\"{Token}\"]");

        // Assert
        Assert.DoesNotContain("/s/", html, StringComparison.Ordinal);
    }

    private static ShortcodesProcessor Processor(global::YesSql.ISession session)
    {
        var versions = new Mock<ISurveyVersionCache>();
        versions
            .Setup(cache => cache.GetPublishedAsync(It.IsAny<Survey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SurveyVersion { ItemId = "v1", SurveyId = "s1", Number = 1, Definition = new SurveyDefinition { Title = "Engagement" } });
        var resolver = new SurveyEmbedResolver(new SurveyStore(session), new SurveyCollectorStore(session), versions.Object);
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };

        return new ShortcodesProcessor(new SurveyShortcodeProvider(resolver, HtmlEncoder.Default, accessor));
    }

    private async Task SaveSurveyAsync(string collectorKind)
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var session = _store.OpenSession();
        await session.SaveAsync(new Survey
        {
            ItemId = "s1",
            Name = "employee-engagement",
            Title = "Engagement",
            Status = SurveyStatus.Open,
            PublishedVersionId = "v1",
            PublishedVersionNumber = 1,
        }, false, SurveysConstants.CollectionName, cancellationToken);
        await session.SaveAsync(new SurveyCollector
        {
            ItemId = "c1",
            SurveyId = "s1",
            Name = "Default",
            Kind = collectorKind,
            Token = Token,
            Status = CollectorStatus.Open,
            IsDefault = true,
        }, false, SurveysConstants.CollectionName, cancellationToken);
        await session.SaveChangesAsync(cancellationToken);
    }
}
