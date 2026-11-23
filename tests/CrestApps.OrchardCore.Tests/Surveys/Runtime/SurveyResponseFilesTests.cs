using System.Security.Claims;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Attachments.Services;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Core;
using CrestApps.OrchardCore.Surveys.Core.Controls.FileUpload;
using CrestApps.OrchardCore.Surveys.Core.Controls.Signature;
using CrestApps.OrchardCore.Surveys.Core.Runtime;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Moq;

namespace CrestApps.OrchardCore.Tests.Surveys.Runtime;

/// <summary>
/// Files of responses are stored only by a page posted forward that passes validation, the files an update releases
/// are removed, and every file of a deleted response goes with it.
/// </summary>
public sealed class SurveyResponseFilesTests
{
    private static readonly SurveyRespondentIdentity _identity = new() { UserId = "u1" };

    [Fact]
    public async Task EvaluateSaveCheckAndBack_WithAFileAndASignature_StoreNothing()
    {
        // Arrange
        var (harness, files) = await CreateAsync();
        await using var _ = harness;
        var definition = RuntimeSurveys.Definition(
            RuntimeSurveys.Page("p1", RuntimeSurveys.Text("t1")),
            RuntimeSurveys.Page("p2", FileQuestion("f1"), SignatureQuestion("s1")));
        definition.Scoring = new SurveyScoringSettings { Enabled = true, FeedbackMode = FeedbackMode.Immediate };
        var survey = await harness.PublishAsync(definition);
        var response = (await harness.StartAsync(survey, "u1")).Response;
        var onPage2 = (await harness.PostAsync(response, _identity, SurveyPageDirection.Next, ("t1", "x"))).Response;

        // Act
        await harness.ResponsesAsync(responses => responses.EvaluateAsync(new EvaluatePageRequest { ResponseId = response.ItemId, Identity = _identity, Form = FormWithFile() }));
        await harness.ResponsesAsync(responses => responses.SaveAsync(new SavePageRequest { ResponseId = response.ItemId, Identity = _identity, PageId = "p2", Form = FormWithFile() }));
        await harness.ResponsesAsync(responses => responses.CheckAnswerAsync(new CheckAnswerRequest { ResponseId = response.ItemId, Identity = _identity, PageId = "p2", QuestionId = "f1", Form = FormWithFile() }));
        await harness.ResponsesAsync(responses => responses.CheckAnswerAsync(new CheckAnswerRequest { ResponseId = response.ItemId, Identity = _identity, PageId = "p2", QuestionId = "s1", Form = FormWithFile() }));
        var back = await harness.ResponsesAsync(responses => responses.SubmitPageAsync(new SubmitPageRequest { ResponseId = response.ItemId, Identity = _identity, PageId = onPage2.CurrentPageId, Direction = SurveyPageDirection.Back, Form = FormWithFile() }));

        // Assert
        Assert.Equal("p1", back.Response.CurrentPageId);
        Assert.Equal(0, files.AddCalls);
        Assert.Empty(files.Stored);

        var stored = await harness.FindAsync(response.ItemId);

        Assert.False(stored.Answers.ContainsKey("f1"));
        Assert.False(stored.Answers.ContainsKey("s1"));
    }

    [Fact]
    public async Task Next_OnARefusedPage_StoresNoFile()
    {
        // Arrange
        var (harness, files) = await CreateAsync();
        await using var _ = harness;
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(
            RuntimeSurveys.Page("p1", FileQuestion("f1"), SignatureQuestion("s1"), RuntimeSurveys.Text("t1", required: true)),
            RuntimeSurveys.Page("p2", RuntimeSurveys.Text("t2"))));
        var response = (await harness.StartAsync(survey, "u1")).Response;

        // Act
        var refused = await harness.ResponsesAsync(responses => responses.SubmitPageAsync(new SubmitPageRequest { ResponseId = response.ItemId, Identity = _identity, PageId = "p1", Direction = SurveyPageDirection.Next, Form = FormWithFile() }));

        // Assert
        Assert.Equal(SurveyErrorCode.InvalidAnswer, refused.Error);
        Assert.Equal(0, files.AddCalls);

        var stored = await harness.FindAsync(response.ItemId);

        Assert.False(stored.Answers.ContainsKey("f1"));
        Assert.False(stored.Answers.ContainsKey("s1"));
    }

    [Fact]
    public async Task Next_StoresTheFiles_ThenAnUntickedFileAndAReplacedSignatureAreRemoved()
    {
        // Arrange
        var (harness, files) = await CreateAsync();
        await using var _ = harness;
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(
            RuntimeSurveys.Page("p1", FileQuestion("f1"), SignatureQuestion("s1"), RuntimeSurveys.Text("t1", required: true)),
            RuntimeSurveys.Page("p2", RuntimeSurveys.Text("t2"))));
        var response = (await harness.StartAsync(survey, "u1")).Response;

        // Act
        var first = await harness.ResponsesAsync(responses => responses.SubmitPageAsync(new SubmitPageRequest { ResponseId = response.ItemId, Identity = _identity, PageId = "p1", Direction = SurveyPageDirection.Next, Form = FormWithFile(("q.t1", "x")) }));
        var afterFirst = await harness.FindAsync(response.ItemId);
        var fileId = afterFirst.Answers["f1"].AttachmentIds.Single();
        var signatureId = afterFirst.Answers["s1"].AttachmentIds.Single();
        var back = await harness.PostAsync(first.Response, _identity, SurveyPageDirection.Back);
        var second = await harness.ResponsesAsync(responses => responses.SubmitPageAsync(new SubmitPageRequest
        {
            ResponseId = response.ItemId,
            Identity = _identity,
            PageId = back.Response.CurrentPageId,
            Direction = SurveyPageDirection.Next,
            Form = Form([("q.t1", "x"), ("q.s1", SignatureDataUrl(2))]),
        }));

        // Assert
        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.Equal(3, files.AddCalls);
        Assert.False(files.Stored.ContainsKey(fileId));
        Assert.False(files.Stored.ContainsKey(signatureId));

        var stored = await harness.FindAsync(response.ItemId);

        Assert.False(stored.Answers.ContainsKey("f1"));
        Assert.True(files.Stored.ContainsKey(stored.Answers["s1"].AttachmentIds.Single()));
        Assert.Single(files.Stored);
    }

    [Fact]
    public async Task Delete_RemovesEveryFileOfTheResponse()
    {
        // Arrange
        var (harness, files) = await CreateAsync();
        await using var _ = harness;
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(
            RuntimeSurveys.Page("p1", FileQuestion("f1"), RuntimeSurveys.Text("t1")),
            RuntimeSurveys.Page("p2", RuntimeSurveys.Text("t2"))));
        var response = (await harness.StartAsync(survey, "u1")).Response;
        await harness.ResponsesAsync(responses => responses.SubmitPageAsync(new SubmitPageRequest { ResponseId = response.ItemId, Identity = _identity, PageId = "p1", Direction = SurveyPageDirection.Next, Form = FormWithFile() }));
        var unreferenced = files.Seed(SurveyResponseFiles.OwnerOf(response.ItemId), RuntimeTestHarness.Now);
        var other = files.Seed(SurveyResponseFiles.OwnerOf("another"), RuntimeTestHarness.Now);

        // Act
        var deleted = await harness.ResponsesAsync(responses => responses.DeleteAsync(response.ItemId, "admin"));

        // Assert
        Assert.True(deleted.Succeeded);
        Assert.False(files.Stored.ContainsKey(unreferenced.ItemId));
        Assert.Equal([other.ItemId], files.Stored.Keys);
    }

    [Fact]
    public async Task OrphanSweep_RemovesOldFilesNoAnswerKeeps()
    {
        // Arrange
        var (harness, files) = await CreateAsync();
        await using var _ = harness;
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(
            RuntimeSurveys.Page("p1", FileQuestion("f1"), RuntimeSurveys.Text("t1")),
            RuntimeSurveys.Page("p2", RuntimeSurveys.Text("t2"))));
        var response = (await harness.StartAsync(survey, "u1")).Response;
        await harness.ResponsesAsync(responses => responses.SubmitPageAsync(new SubmitPageRequest { ResponseId = response.ItemId, Identity = _identity, PageId = "p1", Direction = SurveyPageDirection.Next, Form = FormWithFile() }));
        var kept = (await harness.FindAsync(response.ItemId)).Answers["f1"].AttachmentIds.Single();
        var old = RuntimeTestHarness.Now.AddDays(-2);
        var ofDeleted = files.Seed(SurveyResponseFiles.OwnerOf("deleted-response"), old);
        var dropped = files.Seed(SurveyResponseFiles.OwnerOf(response.ItemId), old);
        var young = files.Seed(SurveyResponseFiles.OwnerOf("deleted-response"), RuntimeTestHarness.Now.AddHours(-1));
        files.Stored[kept].UploadedUtc = old;

        // Act
        var removed = await harness.RunAsync(services => services.GetRequiredService<SurveyResponseOrphanFileSweeper>().SweepAsync(TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(2, removed);
        Assert.False(files.Stored.ContainsKey(ofDeleted.ItemId));
        Assert.False(files.Stored.ContainsKey(dropped.ItemId));
        Assert.True(files.Stored.ContainsKey(young.ItemId));
        Assert.True(files.Stored.ContainsKey(kept));
    }

    [Fact]
    public async Task AccessProvider_RefusesTheFilesOfADeletedResponse_EvenToStaff()
    {
        // Arrange
        var (harness, _) = await CreateAsync();
        await using var disposable = harness;
        var authorization = new Mock<IAuthorizationService>();
        authorization.Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(AuthorizationResult.Success());
        var staff = RuntimeTestHarness.User("staff");

        // Act
        var (view, manage) = await harness.RunAsync(async services =>
        {
            var provider = new SurveyResponseAttachmentAccessProvider(
                services.GetRequiredService<SurveyResponseStore>(),
                Mock.Of<ICatalog<Survey>>(),
                authorization.Object,
                []);
            var owner = SurveyResponseFiles.OwnerOf("deleted-response");

            return (await provider.CanViewAsync(staff, owner), await provider.CanManageAsync(staff, owner));
        });

        // Assert
        Assert.False(view);
        Assert.False(manage);
    }

    [Fact]
    public async Task Next_OnARefusedPage_KeepsTheFilesSavedEarlier()
    {
        // Arrange
        var (harness, files) = await CreateAsync();
        await using var _ = harness;
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(
            RuntimeSurveys.Page("p1", FileQuestion("f1"), RuntimeSurveys.Text("t1", required: true)),
            RuntimeSurveys.Page("p2", RuntimeSurveys.Text("t2"))));
        var response = (await harness.StartAsync(survey, "u1")).Response;
        var first = await harness.ResponsesAsync(responses => responses.SubmitPageAsync(new SubmitPageRequest { ResponseId = response.ItemId, Identity = _identity, PageId = "p1", Direction = SurveyPageDirection.Next, Form = Form([("q.t1", "x")], ("q.f1", "a.txt")) }));
        var fileId = (await harness.FindAsync(response.ItemId)).Answers["f1"].AttachmentIds.Single();
        var back = await harness.ResponsesAsync(responses => responses.SubmitPageAsync(new SubmitPageRequest { ResponseId = response.ItemId, Identity = _identity, PageId = first.Response.CurrentPageId, Direction = SurveyPageDirection.Back, Form = Form([("q.t1", "x"), ("q.f1.keep", fileId)]) }));

        // Act: the question takes one file, so a second one is refused, with the page.
        var refused = await harness.ResponsesAsync(responses => responses.SubmitPageAsync(new SubmitPageRequest
        {
            ResponseId = response.ItemId,
            Identity = _identity,
            PageId = back.Response.CurrentPageId,
            Direction = SurveyPageDirection.Next,
            Form = Form([("q.t1", "x"), ("q.f1.keep", fileId)], ("q.f1", "b.txt")),
        }));

        files.Stored[fileId].UploadedUtc = RuntimeTestHarness.Now.AddDays(-2);
        var swept = await harness.RunAsync(services => services.GetRequiredService<SurveyResponseOrphanFileSweeper>().SweepAsync(TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal("p1", back.Response.CurrentPageId);
        Assert.Equal(SurveyErrorCode.InvalidAnswer, refused.Error);
        Assert.True(refused.Errors.ContainsKey("f1"));
        Assert.Equal(1, files.AddCalls);
        Assert.Equal([fileId], (await harness.FindAsync(response.ItemId)).Answers["f1"].AttachmentIds);
        Assert.Equal(0, swept);
        Assert.True(files.Stored.ContainsKey(fileId));
    }

    [Fact]
    public async Task Next_WhenAttachmentsRefusesAReDrawnSignature_KeepsTheStoredSignature()
    {
        // Arrange: a signature stored by the first page.
        var (harness, files) = await CreateAsync();
        await using var _ = harness;
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(
            RuntimeSurveys.Page("p1", SignatureQuestion("s1"), RuntimeSurveys.Text("t1", required: true)),
            RuntimeSurveys.Page("p2", RuntimeSurveys.Text("t2"))));
        var response = (await harness.StartAsync(survey, "u1")).Response;
        var first = await harness.ResponsesAsync(responses => responses.SubmitPageAsync(new SubmitPageRequest { ResponseId = response.ItemId, Identity = _identity, PageId = "p1", Direction = SurveyPageDirection.Next, Form = Form([("q.t1", "x"), ("q.s1", SignatureDataUrl(1))]) }));
        var signatureId = (await harness.FindAsync(response.ItemId)).Answers["s1"].AttachmentIds.Single();
        var back = await harness.PostAsync(first.Response, _identity, SurveyPageDirection.Back);

        // Act: the re-drawn signature passes validation, but Attachments refuses to store it.
        files.Refuse = true;
        var refused = await harness.ResponsesAsync(responses => responses.SubmitPageAsync(new SubmitPageRequest
        {
            ResponseId = response.ItemId,
            Identity = _identity,
            PageId = back.Response.CurrentPageId,
            Direction = SurveyPageDirection.Next,
            Form = Form([("q.t1", "x"), ("q.s1", SignatureDataUrl(2))]),
        }));

        // Assert
        Assert.Equal(SurveyErrorCode.InvalidAnswer, refused.Error);
        Assert.True(refused.Errors.ContainsKey("s1"));
        Assert.Equal([signatureId], (await harness.FindAsync(response.ItemId)).Answers["s1"].AttachmentIds);
        Assert.True(files.Stored.ContainsKey(signatureId));
    }

    [Fact]
    public async Task Finish_FailingAfterTheFilesAreStored_RemovesThem()
    {
        // Arrange
        var scoring = new Mock<ISurveyScoringEngine>();
        scoring.Setup(engine => engine.Score(It.IsAny<SurveyDefinition>(), It.IsAny<SurveyResponse>())).Throws(new InvalidOperationException("Scoring failed."));
        var (harness, files) = await CreateAsync(services => services.AddSingleton(scoring.Object));
        await using var _ = harness;
        var definition = RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", FileQuestion("f1"), RuntimeSurveys.Text("t1")));
        definition.Scoring = new SurveyScoringSettings { Enabled = true };
        var survey = await harness.PublishAsync(definition);
        var response = (await harness.StartAsync(survey, "u1")).Response;

        // Act
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.ResponsesAsync(responses => responses.SubmitPageAsync(new SubmitPageRequest
        {
            ResponseId = response.ItemId,
            Identity = _identity,
            PageId = "p1",
            Direction = SurveyPageDirection.Finish,
            Form = Form([("q.t1", "x")], ("q.f1", "a.txt")),
        })));

        // Assert
        Assert.Equal("Scoring failed.", failure.Message);
        Assert.Equal(1, files.AddCalls);
        Assert.Empty(files.Stored);
    }

    [Fact]
    public async Task OrphanSweep_KeepsACursor_SoItGetsPastMoreKeptFilesThanOneRunLooksAt()
    {
        // Arrange: 2,500 old files a response keeps (more than one run looks at), then 3 newer orphans.
        var (harness, files) = await CreateAsync();
        await using var _ = harness;
        var start = RuntimeTestHarness.Now.AddDays(-10);
        var keptIds = Enumerable.Range(0, 2500).Select(i => files.Seed(SurveyResponseFiles.OwnerOf("kept-response"), start.AddSeconds(i)).ItemId).ToList();
        var orphans = Enumerable.Range(0, 3).Select(i => files.Seed(SurveyResponseFiles.OwnerOf("deleted-response"), start.AddDays(1).AddSeconds(i)).ItemId).ToList();

        await harness.RunAsync(async services =>
        {
            await services.GetRequiredService<SurveyResponseStore>().CreateAsync(new SurveyResponse
            {
                ItemId = "kept-response",
                SurveyId = "survey",
                Status = SurveyResponseStatus.Completed,
                Answers = { ["f1"] = new SurveyAnswer { AttachmentIds = keptIds } },
            });

            return true;
        });

        // Act
        var firstRun = await harness.RunAsync(services => services.GetRequiredService<SurveyResponseOrphanFileSweeper>().SweepAsync(TestContext.Current.CancellationToken));
        var secondRun = await harness.RunAsync(services => services.GetRequiredService<SurveyResponseOrphanFileSweeper>().SweepAsync(TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(0, firstRun);
        Assert.Equal(3, secondRun);
        Assert.All(orphans, id => Assert.False(files.Stored.ContainsKey(id)));
        Assert.All(keptIds, id => Assert.True(files.Stored.ContainsKey(id)));
    }

    [Fact]
    public async Task Delete_LeavesTheFilesOfAnotherResponseItsAnswersList()
    {
        // Arrange
        var (harness, files) = await CreateAsync();
        await using var _ = harness;
        var others = files.Seed(SurveyResponseFiles.OwnerOf("other-response"), RuntimeTestHarness.Now);
        var own = files.Seed(SurveyResponseFiles.OwnerOf("imported-response"), RuntimeTestHarness.Now);
        var imported = new SurveyResponse
        {
            ItemId = "imported-response",
            SurveyId = "survey",
            Answers = { ["f1"] = new SurveyAnswer { AttachmentIds = [others.ItemId, own.ItemId] } },
        };

        // Act
        var removed = await harness.RunAsync(services => services.GetRequiredService<SurveyResponseFiles>().DeleteAllAsync(imported, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(1, removed);
        Assert.True(files.Stored.ContainsKey(others.ItemId));
        Assert.False(files.Stored.ContainsKey(own.ItemId));
    }

    private static async Task<(RuntimeTestHarness Harness, SurveyFilesFakeAttachmentService Files)> CreateAsync(Action<IServiceCollection> configure = null)
    {
        var files = new SurveyFilesFakeAttachmentService { Clock = () => RuntimeTestHarness.Now };
        var harness = await RuntimeTestHarness.CreateAsync(services =>
        {
            services.AddSingleton<IAttachmentService>(files);
            services.AddSurveysFileUploadControl();
            services.AddSurveyControl<SignatureControlHandler>(SurveyControlNames.Signature, descriptor => descriptor.Capabilities = SurveyControlCapabilities.Answerable | SurveyControlCapabilities.StoresFiles);
            configure?.Invoke(services);
        });

        return (harness, files);
    }

    private static SurveyQuestion FileQuestion(string id)
        => new() { Id = id, Name = id, Title = id, ControlType = SurveyControlNames.FileUpload };

    private static SurveyQuestion SignatureQuestion(string id)
        => new() { Id = id, Name = id, Title = id, ControlType = SurveyControlNames.Signature };

    private static string SignatureDataUrl(byte seed)
        => "data:image/png;base64," + Convert.ToBase64String([0x89, 0x50, 0x4E, 0x47, seed]);

    private static FormCollection FormWithFile(params (string Key, string Value)[] fields)
        => Form([("q.s1", SignatureDataUrl(1)), .. fields], ("q.f1", "notes.txt"));

    private static FormCollection Form((string Key, string Value)[] fields, params (string Name, string FileName)[] uploads)
    {
        var formFiles = new FormFileCollection();

        foreach (var (name, fileName) in uploads)
        {
            var bytes = "hello"u8.ToArray();

            formFiles.Add(new FormFile(new MemoryStream(bytes), 0, bytes.Length, name, fileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = "text/plain",
            });
        }

        return new FormCollection(
            fields.GroupBy(field => field.Key, StringComparer.Ordinal).ToDictionary(group => group.Key, group => new StringValues([.. group.Select(field => field.Value)]), StringComparer.Ordinal),
            formFiles);
    }
}
