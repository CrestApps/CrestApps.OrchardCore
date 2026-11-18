using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.KnowledgeBase;
using CrestApps.OrchardCore.KnowledgeBase.Indexes;
using CrestApps.OrchardCore.KnowledgeBase.Migrations;
using CrestApps.OrchardCore.KnowledgeBase.Models;
using CrestApps.OrchardCore.KnowledgeBase.Services;
using CrestApps.OrchardCore.PersonalData;
using CrestApps.OrchardCore.Tests.Calendars;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.ContentManagement;
using YesSql;

namespace CrestApps.OrchardCore.Tests.KnowledgeBase;

/// <summary>
/// "Was this helpful?" only counted answers on the article, so nobody could see what readers thought or what was
/// missing: each answer is now a dated record in the Knowledge Base's collection, a No can say what was missing, and a
/// signed-in reader's answers take part in personal data requests.
/// </summary>
public sealed class KnowledgeBaseFeedbackTests
{
    [Fact]
    public async Task EachAnswer_IsADatedRecord_AndStillCountsOnTheArticle()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var article = Article("a1", "Reset your password", "accounts");

        // Act
        var ids = await harness.RunAsync(async services =>
        {
            var feedback = services.GetRequiredService<KnowledgeArticleFeedbackService>();

            return (
                Yes: await feedback.RecordAsync(article, helpful: true, CalendarTestHarness.User("dana")),
                No: await feedback.RecordAsync(article, helpful: false, new ClaimsPrincipal(new ClaimsIdentity())));
        });

        // Assert
        var part = article.GetOrCreate<KnowledgeArticlePart>();
        Assert.Equal((1, 1), (part.HelpfulCount, part.NotHelpfulCount));

        var answers = await ListAsync(harness);
        var yes = Assert.Single(answers, answer => answer.ItemId == ids.Yes);
        var no = Assert.Single(answers, answer => answer.ItemId == ids.No);
        Assert.True(yes.Helpful);
        Assert.Equal(("dana", "dana"), (yes.UserId, yes.UserName));
        Assert.False(no.Helpful);
        Assert.Null(no.UserId);
        Assert.All(answers, answer =>
        {
            Assert.Equal(("a1", "a1-v1", "Reset your password", "accounts"), (answer.ContentItemId, answer.ContentItemVersionId, answer.Title, answer.CategoryId));
            Assert.Equal(CalendarTestHarness.Now, answer.CreatedUtc);
        });
    }

    [Fact]
    public async Task WhatWasMissing_IsKeptOnce_OnlyOnTheReadersOwnNo()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var article = Article("a1", "Reset your password", null);
        var ids = await harness.RunAsync(async services =>
        {
            var feedback = services.GetRequiredService<KnowledgeArticleFeedbackService>();

            return (
                Yes: await feedback.RecordAsync(article, helpful: true, null),
                No: await feedback.RecordAsync(article, helpful: false, null));
        });

        // Act
        var kept = await harness.RunAsync(async services =>
        {
            var feedback = services.GetRequiredService<KnowledgeArticleFeedbackService>();
            var ct = TestContext.Current.CancellationToken;

            return new
            {
                OnYes = await feedback.AddCommentAsync("a1", ids.Yes, "Nothing", ct),
                OtherArticle = await feedback.AddCommentAsync("a2", ids.No, "Nothing", ct),
                Empty = await feedback.AddCommentAsync("a1", ids.No, "   ", ct),
                First = await feedback.AddCommentAsync("a1", ids.No, "  The reset link expired.  ", ct),
            };
        });
        var again = await harness.RunAsync(services => services.GetRequiredService<KnowledgeArticleFeedbackService>().AddCommentAsync("a1", ids.No, "Second try", TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal((false, false, false, true, false), (kept.OnYes, kept.OtherArticle, kept.Empty, kept.First, again));
        var answers = await ListAsync(harness);
        Assert.Equal("The reset link expired.", Assert.Single(answers, answer => answer.ItemId == ids.No).Comment);
        Assert.Null(Assert.Single(answers, answer => answer.ItemId == ids.Yes).Comment);
    }

    [Fact]
    public void ALongComment_IsCut()
        => Assert.Equal(KnowledgeArticleFeedbackIndex.MaxCommentLength, KnowledgeArticleFeedbackService.Clean(new string('x', 5000)).Length);

    [Fact]
    public async Task ASignedInReadersAnswers_AreExported_AndErasedWithoutNamingThem()
    {
        // Arrange
        await using var harness = await CreateAsync(services => services.AddScoped<IPersonalDataContributor, KnowledgeArticleFeedbackPersonalDataContributor>());
        var article = Article("a1", "Reset your password", null);
        var danaId = await harness.RunAsync(async services =>
        {
            var feedback = services.GetRequiredService<KnowledgeArticleFeedbackService>();
            var id = await feedback.RecordAsync(article, helpful: false, CalendarTestHarness.User("dana"));
            await feedback.RecordAsync(article, helpful: true, CalendarTestHarness.User("sam"));
            await services.GetRequiredService<ISession>().SaveChangesAsync(TestContext.Current.CancellationToken);
            await feedback.AddCommentAsync("a1", id, "Dana here: the link expired.", TestContext.Current.CancellationToken);

            return id;
        });

        var subject = new PersonalDataSubject(userId: "dana");
        subject.AddName("Dana");

        // Act
        var export = new PersonalDataExport();
        await harness.RunAsync(async services =>
        {
            var contributor = services.GetRequiredService<IPersonalDataContributor>();
            await contributor.ExportAsync(new PersonalDataExportContext(subject, export), TestContext.Current.CancellationToken);
            await contributor.AnonymizeAsync(new PersonalDataAnonymizeContext(subject, "[removed]", new PersonalDataAnonymizeResult()), TestContext.Current.CancellationToken);

            return true;
        });

        // Assert
        var record = Assert.Single(export.Records);
        Assert.Equal((KnowledgeArticleFeedbackPersonalDataContributor.FeedbackKind, danaId), (record.Kind, record.Id));
        Assert.Equal("Dana here: the link expired.", record.Fields[nameof(KnowledgeArticleFeedback.Comment)]);

        var answers = await ListAsync(harness);
        var erased = Assert.Single(answers, answer => answer.ItemId == danaId);
        Assert.Null(erased.UserId);
        Assert.Null(erased.UserName);
        Assert.DoesNotContain("Dana", erased.Comment, StringComparison.Ordinal);
        Assert.Equal("sam", Assert.Single(answers, answer => answer.ItemId != danaId).UserId);
    }

    private static async Task<List<KnowledgeArticleFeedback>> ListAsync(CalendarTestHarness harness)
        => await harness.RunAsync(async services => (await services.GetRequiredService<ISession>()
            .Query<KnowledgeArticleFeedback, KnowledgeArticleFeedbackIndex>(collection: KnowledgeBaseConstants.CollectionName)
            .ListAsync(TestContext.Current.CancellationToken)).ToList(), commit: false);

    private static ContentItem Article(string id, string title, string category)
    {
        var item = new ContentItem
        {
            ContentItemId = id,
            ContentItemVersionId = id + "-v1",
            ContentType = KnowledgeBaseConstants.ArticleType,
            DisplayText = title,
            Published = true,
            Latest = true,
        };
        item.Weld(new KnowledgeArticlePart());

        if (category is not null)
        {
            ((JsonObject)item.Content)[KnowledgeBaseConstants.ArticleType] = new JsonObject
            {
                [KnowledgeBaseConstants.CategoryField] = new JsonObject { ["TaxonomyContentItemId"] = "taxonomy", ["TermContentItemIds"] = new JsonArray(category) },
            };
        }

        return item;
    }

    private static Task<CalendarTestHarness> CreateAsync(Action<IServiceCollection> configure = null)
        => CalendarTestHarness.CreateAsync(
            services =>
            {
                services.AddSingleton(Mock.Of<IContentManager>());
                services.AddScoped<KnowledgeArticleFeedbackService>();
                configure?.Invoke(services);
            },
            KnowledgeBaseMigrations.CreateFeedbackIndexAsync,
            [new KnowledgeArticleFeedbackIndexProvider()],
            [KnowledgeBaseConstants.CollectionName]);
}
