using System.IO.Compression;
using System.Reflection;
using System.Text;
using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.ContentTransfer.BackgroundTasks;
using CrestApps.OrchardCore.ContentTransfer.Indexes;
using CrestApps.OrchardCore.ContentTransfer.Models;
using CrestApps.OrchardCore.Learning.Assessments;
using CrestApps.OrchardCore.Learning.Assessments.ContentTransfer;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Entities;
using OrchardCore.Lists.Indexes;
using OrchardCore.Media;
using OrchardCore.Title.Models;
using YesSql;

namespace CrestApps.OrchardCore.Learning.IntegrationTests;

/// <summary>
/// QTI import on a running site: a QTI 3.0 package imported through ContentTransfer's background import becomes
/// questions of several kinds in the chosen question bank, with its picture in the media library; an unsupported item is
/// a failed row; importing the package again updates the same questions.
/// </summary>
[Collection(LearningSiteCollection.Name)]
public sealed class QtiImportTests
{
    private const string Namespace30 = "http://www.imsglobal.org/xsd/imsqtiasi_v3p0";

    private readonly LearningSiteFixture _fixture;

    public QtiImportTests(LearningSiteFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task AQtiPackage_ImportsIntoTheChosenBank_WithItsPicture_ReportsTheItemItSkips_AndUpdatesOnTheNextImport()
    {
        // Arrange
        var bankId = await _fixture.InTenantAsync(async services =>
        {
            var contentManager = services.GetRequiredService<IContentManager>();
            var bank = await contentManager.NewAsync(AssessmentsConstants.ContentTypes.QuestionBank);
            bank.Alter<TitlePart>(part => part.Title = "QTI bank");
            await contentManager.CreateAsync(bank, VersionOptions.Published);

            return bank.ContentItemId;
        });

        var package = Package();

        // Act
        var first = await ImportAsync(package, bankId);
        var firstQuestions = await QuestionsAsync(bankId);
        var second = await ImportAsync(package, bankId);
        var secondQuestions = await QuestionsAsync(bankId);

        // Assert
        Assert.Equal(ContentTransferEntryStatus.CompletedWithErrors, first.Status);
        var stats = first.GetOrCreate<ImportFileProcessStatsPart>();
        Assert.Equal(3, stats.ImportedCount);
        var error = Assert.Single(stats.ErrorMessages);
        Assert.Contains("hotspot", error.Value, StringComparison.Ordinal);
        Assert.Contains("hotspotInteraction", error.Value, StringComparison.Ordinal);

        Assert.Equal(3, firstQuestions.Count);
        Assert.Equal(
            [AssessmentsConstants.ContentTypes.EssayQuestion, AssessmentsConstants.ContentTypes.OrderingQuestion, AssessmentsConstants.ContentTypes.SingleChoiceQuestion],
            firstQuestions.Select(question => question.ContentType).Order(StringComparer.Ordinal));

        var choice = firstQuestions.Single(question => question.ContentType == AssessmentsConstants.ContentTypes.SingleChoiceQuestion);
        var part = choice.Get<QuestionPart>(nameof(QuestionPart));
        Assert.Equal("choice", part.SourceIdentifier);
        Assert.Equal("bank.zip/items/choice.xml", part.SourceFile);
        var mediaPath = $"qti-imports/{first.EntryId}/items/images/sign.png";
        Assert.Contains(mediaPath, part.Prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("qti-media:", part.Prompt, StringComparison.Ordinal);
        Assert.Equal(["Stay with it", "Leave it"], choice.Get<ChoiceQuestionPart>(nameof(ChoiceQuestionPart)).Options.Select(option => option.Text));

        var ordering = firstQuestions.Single(question => question.ContentType == AssessmentsConstants.ContentTypes.OrderingQuestion);
        Assert.Equal(["Plan", "Do", "Check", "Act"], ordering.Get<OrderingQuestionPart>(nameof(OrderingQuestionPart)).Items.Select(item => item.Text));

        var stored = await _fixture.InTenantAsync(services => services.GetRequiredService<IMediaFileStore>().GetFileInfoAsync(mediaPath));
        Assert.NotNull(stored);

        Assert.Equal(3, second.GetOrCreate<ImportFileProcessStatsPart>().ImportedCount);
        Assert.Equal(firstQuestions.Select(question => question.ContentItemId).Order(StringComparer.Ordinal), secondQuestions.Select(question => question.ContentItemId).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Round9M9_ReImportMatchesOnlyTheSamePackageInTheSameChosenBank()
    {
        // Arrange: two banks' worth of imports of packages whose items have the same identifiers.
        var bankId = await _fixture.InTenantAsync(async services =>
        {
            var contentManager = services.GetRequiredService<IContentManager>();
            var bank = await contentManager.NewAsync(AssessmentsConstants.ContentTypes.QuestionBank);
            bank.Alter<TitlePart>(part => part.Title = "QTI bank of two instructors");
            await contentManager.CreateAsync(bank, VersionOptions.Published);

            return bank.ContentItemId;
        });

        var package = Package();

        // Act: instructor A imports a.zip; instructor B imports another package with the same item identifiers into
        // the same bank; then both import without a bank.
        await ImportAsync(package, bankId, "instructor-a.zip");
        var before = (await QuestionsAsync(bankId)).Select(question => question.ContentItemId).ToHashSet(StringComparer.Ordinal);
        await ImportAsync(package, bankId, "instructor-b.zip");
        var after = await QuestionsAsync(bankId);

        var firstBankless = await ImportAsync(package, null, "bankless-a.zip");
        var secondBankless = await ImportAsync(package, null, "bankless-b.zip");
        var bankless = await _fixture.InTenantAsync(async services => (await services.GetRequiredService<ISession>()
            .Query<ContentItem, ContentItemIndex>(index => index.Latest && index.ContentType == AssessmentsConstants.ContentTypes.SingleChoiceQuestion)
            .ListAsync()).Count(item => item.Get<QuestionPart>(nameof(QuestionPart))?.SourceFile is "bankless-a.zip/items/choice.xml" or "bankless-b.zip/items/choice.xml"));

        // Assert: B's import added questions of its own and left A's as they were.
        Assert.Equal(6, after.Count);
        Assert.True(before.IsSubsetOf(after.Select(question => question.ContentItemId)));
        Assert.Equal(3, after.Count(question => question.Get<QuestionPart>(nameof(QuestionPart)).SourceFile.StartsWith("instructor-a.zip/", StringComparison.Ordinal)));
        Assert.Equal(3, firstBankless.GetOrCreate<ImportFileProcessStatsPart>().ImportedCount);
        Assert.Equal(3, secondBankless.GetOrCreate<ImportFileProcessStatsPart>().ImportedCount);
        Assert.Equal(2, bankless);
    }

    [Fact]
    public async Task Round9Low_AFeedbackOnlyPicture_StaysOutOfTheMediaLibrary_AndOnlyStaffReadIt()
    {
        // Arrange: an item whose prompt and general feedback each show a picture of their own.
        var bankId = await _fixture.InTenantAsync(async services =>
        {
            var contentManager = services.GetRequiredService<IContentManager>();
            var bank = await contentManager.NewAsync(AssessmentsConstants.ContentTypes.QuestionBank);
            bank.Alter<TitlePart>(part => part.Title = "QTI feedback picture bank");
            await contentManager.CreateAsync(bank, VersionOptions.Published);

            return bank.ContentItemId;
        });

        // Act
        var entry = await ImportAsync(FeedbackPackage(), bankId, "feedback.zip");
        var part = Assert.Single(await QuestionsAsync(bankId)).Get<QuestionPart>(nameof(QuestionPart));
        var promptPath = $"qti-imports/{entry.EntryId}/items/images/sign.png";
        var feedbackPath = $"qti-imports/{entry.EntryId}/items/images/answer.png";
        var (promptStored, feedbackStored) = await _fixture.InTenantAsync(async services =>
        {
            var media = services.GetRequiredService<IMediaFileStore>();

            return (await media.GetFileInfoAsync(promptPath), await media.GetFileInfoAsync(feedbackPath));
        });

        // Assert: the prompt's picture is in the media library, as learners see it while they answer; the feedback's is
        // not, and is served only to staff (a learner reads it only once their attempt's review shows the feedback).
        Assert.Contains(promptPath, part.Prompt, StringComparison.Ordinal);
        Assert.NotNull(promptStored);
        Assert.Null(feedbackStored);
        Assert.DoesNotContain("qti-imports", part.GeneralFeedback, StringComparison.Ordinal);
        Assert.DoesNotContain("qti-media:", part.GeneralFeedback, StringComparison.Ordinal);

        var source = System.Text.RegularExpressions.Regex.Match(part.GeneralFeedback, "src=\"([^\"]+)\"").Groups[1].Value;
        Assert.Contains("learning/assessments/feedback-images/", source, StringComparison.Ordinal);

        var address = new Uri(_fixture.BaseAddress, System.Net.WebUtility.HtmlDecode(source));
        using var admin = await _fixture.CreateClientAsync("admin");
        using var learner = await _fixture.CreateClientAsync("learner1");
        using var anonymous = new HttpClient { BaseAddress = _fixture.BaseAddress };
        var byAdmin = await admin.GetAsync(address, TestContext.Current.CancellationToken);
        var byLearner = await learner.GetAsync(address, TestContext.Current.CancellationToken);
        var byAnonymous = await anonymous.GetAsync(address, TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, byAdmin.StatusCode);
        Assert.Equal("image/png", byAdmin.Content.Headers.ContentType?.MediaType);
        Assert.NotEqual(System.Net.HttpStatusCode.OK, byLearner.StatusCode);
        Assert.NotEqual(System.Net.HttpStatusCode.OK, byAnonymous.StatusCode);
    }

    private async Task<ContentTransferEntry> ImportAsync(byte[] package, string bankId, string uploadedFileName = "bank.zip")
    {
        var entryId = await QueueAndRunAsync(package, bankId, uploadedFileName);

        // The import of the entry runs in a scope of its own, as the deferred task the upload starts runs it.
        await _fixture.InTenantAsync(services =>
        {
            var method = typeof(ImportFilesBackgroundTask).GetMethod("ProcessEntriesAsync", BindingFlags.NonPublic | BindingFlags.Static, [typeof(IServiceProvider), typeof(string), typeof(CancellationToken)]);

            return (Task)method.Invoke(null, [services, entryId, CancellationToken.None]);
        });

        // Read in a scope of its own: the session that queued the entry keeps the copy it saved.
        return await _fixture.InTenantAsync(services => services.GetRequiredService<ISession>()
            .Query<ContentTransferEntry, ContentTransferEntryIndex>(index => index.EntryId == entryId)
            .FirstOrDefaultAsync());
    }

    private Task<string> QueueAndRunAsync(byte[] package, string bankId, string uploadedFileName)
        => _fixture.InTenantAsync(async services =>
        {
            var users = services.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<global::OrchardCore.Users.IUser>>();
            var admin = await users.FindByNameAsync("admin");

            var store = services.GetRequiredService<IContentTransferFileStore>();
            var session = services.GetRequiredService<ISession>();
            var storedName = await store.CreateFileFromStreamAsync(Guid.NewGuid().ToString("n") + ".zip", new MemoryStream(package), false);
            var entry = new ContentTransferEntry
            {
                EntryId = Guid.NewGuid().ToString("n")[..26],
                ContentType = AssessmentsConstants.ContentTypes.SingleChoiceQuestion,
                UploadedFileName = uploadedFileName,
                Owner = await users.GetUserIdAsync(admin),
                StoredFileName = storedName,
                Status = ContentTransferEntryStatus.Pending,
                Direction = ContentTransferDirection.Import,
                CreatedUtc = DateTime.UtcNow,
            };

            entry.Put(new ImportContentOptionsPart
            {
                PublishImportedContent = true,
                RowContentTypes =
                [
                    AssessmentsConstants.ContentTypes.SingleChoiceQuestion,
                    AssessmentsConstants.ContentTypes.MultipleChoiceQuestion,
                    AssessmentsConstants.ContentTypes.TrueFalseQuestion,
                    AssessmentsConstants.ContentTypes.ShortTextQuestion,
                    AssessmentsConstants.ContentTypes.NumericQuestion,
                    AssessmentsConstants.ContentTypes.MatchingQuestion,
                    AssessmentsConstants.ContentTypes.OrderingQuestion,
                    AssessmentsConstants.ContentTypes.FillInBlanksQuestion,
                    AssessmentsConstants.ContentTypes.EssayQuestion,
                ],
            });
            entry.Put(new QuestionImportOptionsPart { QuestionBankId = bankId });

            await session.SaveAsync(entry, false, collection: null);
            await session.SaveChangesAsync();

            return entry.EntryId;
        });

    private Task<List<ContentItem>> QuestionsAsync(string bankId)
        => _fixture.InTenantAsync(async services =>
        {
            var items = await services.GetRequiredService<ISession>()
                .Query<ContentItem, ContainedPartIndex>(index => index.ListContentItemId == bankId)
                .ListAsync();

            return items.Where(item => item.Latest).ToList();
        });

    private static byte[] FeedbackPackage()
    {
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");
        var files = new (string Name, byte[] Content)[]
        {
            ("imsmanifest.xml", Encoding.UTF8.GetBytes("""
                <?xml version="1.0" encoding="UTF-8"?>
                <manifest xmlns="http://www.imsglobal.org/xsd/qti/qtiv3p0/imscp_v1p1" identifier="M">
                  <resources>
                    <resource identifier="essay" type="imsqti_item_xmlv3p0" href="items/essay.xml"><file href="items/essay.xml"/></resource>
                  </resources>
                </manifest>
                """)),
            ("items/essay.xml", Encoding.UTF8.GetBytes($"""
                <?xml version="1.0" encoding="UTF-8"?>
                <qti-assessment-item xmlns="{Namespace30}" identifier="essay" title="essay" adaptive="false" time-dependent="false">
                <qti-response-declaration identifier="RESPONSE" cardinality="single" base-type="string"/>
                <qti-outcome-declaration identifier="COMPLETION" cardinality="single" base-type="identifier"/>
                <qti-item-body><p><img src="images/sign.png" alt="Sign"/></p><p>What does the sign ask of you?</p><qti-extended-text-interaction response-identifier="RESPONSE"/></qti-item-body>
                <qti-modal-feedback outcome-identifier="COMPLETION" identifier="done" show-hide="show"><p><img src="images/answer.png" alt="The answer"/></p></qti-modal-feedback>
                </qti-assessment-item>
                """)),
            ("items/images/sign.png", png),
            ("items/images/answer.png", png),
        };

        using var stream = new MemoryStream();

        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in files)
            {
                using var writer = archive.CreateEntry(name).Open();
                writer.Write(content);
            }
        }

        return stream.ToArray();
    }

    private static byte[] Package()
    {
        string Item(string identifier, string declarations, string body, string processing) => $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <qti-assessment-item xmlns="{Namespace30}" identifier="{identifier}" title="{identifier}" adaptive="false" time-dependent="false">
            {declarations}
            <qti-item-body>{body}</qti-item-body>
            {processing}
            </qti-assessment-item>
            """;

        var files = new (string Name, byte[] Content)[]
        {
            ("imsmanifest.xml", Encoding.UTF8.GetBytes("""
                <?xml version="1.0" encoding="UTF-8"?>
                <manifest xmlns="http://www.imsglobal.org/xsd/qti/qtiv3p0/imscp_v1p1" identifier="M">
                  <resources>
                    <resource identifier="choice" type="imsqti_item_xmlv3p0" href="items/choice.xml"><file href="items/choice.xml"/></resource>
                    <resource identifier="order" type="imsqti_item_xmlv3p0" href="items/order.xml"><file href="items/order.xml"/></resource>
                    <resource identifier="essay" type="imsqti_item_xmlv3p0" href="items/essay.xml"><file href="items/essay.xml"/></resource>
                    <resource identifier="hotspot" type="imsqti_item_xmlv3p0" href="items/hotspot.xml"><file href="items/hotspot.xml"/></resource>
                  </resources>
                </manifest>
                """)),
            ("items/choice.xml", Encoding.UTF8.GetBytes(Item("choice",
                """<qti-response-declaration identifier="RESPONSE" cardinality="single" base-type="identifier"><qti-correct-response><qti-value>A</qti-value></qti-correct-response></qti-response-declaration>""",
                """<p><img src="images/sign.png" alt="Sign"/></p><qti-choice-interaction response-identifier="RESPONSE" max-choices="1"><qti-prompt>What does the sign say?</qti-prompt><qti-simple-choice identifier="A">Stay with it</qti-simple-choice><qti-simple-choice identifier="B">Leave it</qti-simple-choice></qti-choice-interaction>""",
                """<qti-response-processing template="https://purl.imsglobal.org/spec/qti/v3p0/rptemplates/match_correct.xml"/>"""))),
            ("items/order.xml", Encoding.UTF8.GetBytes(Item("order",
                """<qti-response-declaration identifier="RESPONSE" cardinality="ordered" base-type="identifier"><qti-correct-response><qti-value>P</qti-value><qti-value>D</qti-value><qti-value>C</qti-value><qti-value>A</qti-value></qti-correct-response></qti-response-declaration>""",
                """<qti-order-interaction response-identifier="RESPONSE"><qti-prompt>Put the steps in order.</qti-prompt><qti-simple-choice identifier="D">Do</qti-simple-choice><qti-simple-choice identifier="A">Act</qti-simple-choice><qti-simple-choice identifier="P">Plan</qti-simple-choice><qti-simple-choice identifier="C">Check</qti-simple-choice></qti-order-interaction>""",
                """<qti-response-processing template="https://purl.imsglobal.org/spec/qti/v3p0/rptemplates/match_correct.xml"/>"""))),
            ("items/essay.xml", Encoding.UTF8.GetBytes(Item("essay",
                """<qti-response-declaration identifier="RESPONSE" cardinality="single" base-type="string"/>""",
                """<p>Describe a safe lift.</p><qti-extended-text-interaction response-identifier="RESPONSE"/>""",
                string.Empty))),
            ("items/hotspot.xml", Encoding.UTF8.GetBytes(Item("hotspot",
                """<qti-response-declaration identifier="RESPONSE" cardinality="single" base-type="identifier"/>""",
                """<qti-hotspot-interaction response-identifier="RESPONSE" max-choices="1"/>""",
                string.Empty))),
            ("items/images/sign.png", Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==")),
        };

        using var stream = new MemoryStream();

        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in files)
            {
                using var writer = archive.CreateEntry(name).Open();
                writer.Write(content);
            }
        }

        return stream.ToArray();
    }
}
