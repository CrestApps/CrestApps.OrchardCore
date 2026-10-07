using System.Globalization;
using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements;

/// <summary>
/// Covers cloning a batch from the list and the Save &amp; Load button on the batch editor. Save &amp; Load must save
/// exactly as Save does and only then start the load the list's Load activities action starts; a batch that fails
/// validation is shown again and never loaded.
/// </summary>
public sealed class ActivityBatchesControllerCloneAndLoadTests
{
    private const string BatchId = "batch-1";
    private const string SourceName = "Manual";

    [Fact]
    public async Task Clone_CreatesANewBatch_AndOpensItForEditing()
    {
        var source = new OmnichannelActivityBatch
        {
            ItemId = BatchId,
            DisplayText = "Spring follow-up",
            CampaignId = "campaign-1",
            Status = OmnichannelActivityBatchStatus.Loaded,
            TotalLoaded = 12,
            TotalMatched = 15,
        };
        var harness = CreateHarness(source);
        harness.Manager
            .Setup(manager => manager.NewAsync(It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OmnichannelActivityBatch { ItemId = "copy-1", OwnerId = "user-1" });

        OmnichannelActivityBatch created = null;
        harness.Manager
            .Setup(manager => manager.CreateAsync(It.IsAny<OmnichannelActivityBatch>(), It.IsAny<CancellationToken>()))
            .Callback((OmnichannelActivityBatch batch, CancellationToken _) => created = batch)
            .Returns(ValueTask.CompletedTask);

        var result = await harness.Controller.Clone(BatchId);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(ActivityBatchesController.Edit), redirect.ActionName);
        Assert.Equal("copy-1", redirect.RouteValues["id"]);

        Assert.NotNull(created);
        Assert.Equal("copy-1", created.ItemId);
        Assert.Equal("Spring follow-up (copy)", created.DisplayText);
        Assert.Equal("campaign-1", created.CampaignId);
        Assert.Equal(OmnichannelActivityBatchStatus.New, created.Status);
        Assert.Null(created.TotalLoaded);
        Assert.Null(created.TotalMatched);

        // The source keeps its own load.
        Assert.Equal(OmnichannelActivityBatchStatus.Loaded, source.Status);
        Assert.Equal(12, source.TotalLoaded);
        harness.Manager.Verify(manager => manager.UpdateAsync(It.IsAny<OmnichannelActivityBatch>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal(NotifyType.Success, Assert.Single(harness.Notices).Type);
    }

    [Fact]
    public async Task Clone_UnknownBatch_ReturnsNotFound()
    {
        var harness = CreateHarness();

        var result = await harness.Controller.Clone("missing");

        Assert.IsType<NotFoundResult>(result);
        harness.Manager.Verify(manager => manager.CreateAsync(It.IsAny<OmnichannelActivityBatch>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Clone_WithoutTheManagePermission_IsForbidden()
    {
        var harness = CreateHarness(canManage: false, batches: Batch(OmnichannelActivityBatchStatus.New));

        var result = await harness.Controller.Clone(BatchId);

        Assert.IsType<ForbidResult>(result);
        harness.Manager.Verify(manager => manager.CreateAsync(It.IsAny<OmnichannelActivityBatch>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EditAndLoad_ValidBatch_SavesItThenStartsTheLoad()
    {
        var batch = Batch(OmnichannelActivityBatchStatus.New);
        var harness = CreateHarness(batch);
        var savedStatuses = new List<OmnichannelActivityBatchStatus>();
        harness.Manager
            .Setup(manager => manager.UpdateAsync(batch, It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .Callback((OmnichannelActivityBatch saved, JsonNode _, CancellationToken _) => savedStatuses.Add(saved.Status))
            .Returns(ValueTask.CompletedTask);

        var result = await harness.Controller.EditAndLoadPost(BatchId);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(ActivityBatchesController.Index), redirect.ActionName);

        // Saved once as edited, then once more as started -- the same order the list's Load action follows.
        Assert.Equal([OmnichannelActivityBatchStatus.New, OmnichannelActivityBatchStatus.Started], savedStatuses);
        harness.DisplayManager.Verify(manager => manager.UpdateEditorAsync(batch, It.IsAny<IUpdateModel>(), false, It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        Assert.Equal(OmnichannelActivityBatchStatus.Started, batch.Status);
        Assert.Contains("loading in the background", Assert.Single(harness.Notices).Message);
    }

    [Fact]
    public async Task EditAndLoad_InvalidBatch_RedisplaysTheFormAndDoesNotLoad()
    {
        var batch = Batch(OmnichannelActivityBatchStatus.New);
        var harness = CreateHarness(batch);
        harness.Controller.ModelState.AddModelError("DisplayText", "Name is required.");

        var result = await harness.Controller.EditAndLoadPost(BatchId);

        Assert.IsType<ViewResult>(result);
        harness.Manager.Verify(manager => manager.UpdateAsync(It.IsAny<OmnichannelActivityBatch>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal(OmnichannelActivityBatchStatus.New, batch.Status);
        Assert.Empty(harness.Notices);
    }

    [Theory]
    [InlineData(OmnichannelActivityBatchStatus.Started)]
    [InlineData(OmnichannelActivityBatchStatus.Loading)]
    [InlineData(OmnichannelActivityBatchStatus.Loaded)]
    public async Task EditAndLoad_BatchThatCannotBeLoaded_IsRefused(OmnichannelActivityBatchStatus status)
    {
        var batch = Batch(status);
        var harness = CreateHarness(batch);

        var result = await harness.Controller.EditAndLoadPost(BatchId);

        Assert.IsType<RedirectToActionResult>(result);
        harness.Manager.Verify(manager => manager.UpdateAsync(It.IsAny<OmnichannelActivityBatch>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal(status, batch.Status);
        Assert.Equal(NotifyType.Error, Assert.Single(harness.Notices).Type);
    }

    [Fact]
    public async Task Edit_Save_DoesNotStartTheLoad()
    {
        var batch = Batch(OmnichannelActivityBatchStatus.New);
        var harness = CreateHarness(batch);

        await harness.Controller.EditPost(BatchId);

        harness.Manager.Verify(manager => manager.UpdateAsync(batch, It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(OmnichannelActivityBatchStatus.New, batch.Status);
    }

    [Fact]
    public async Task CreateAndLoad_ValidBatch_CreatesItThenStartsTheLoad()
    {
        var harness = CreateHarness();
        var batch = new OmnichannelActivityBatch { ItemId = "new-1", DisplayText = "New load" };
        harness.Manager
            .Setup(manager => manager.NewAsync(It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(batch);

        var result = await harness.Controller.CreateAndLoadPost(SourceName);

        Assert.IsType<RedirectToActionResult>(result);
        harness.Manager.Verify(manager => manager.CreateAsync(batch, It.IsAny<CancellationToken>()), Times.Once);
        harness.Manager.Verify(manager => manager.UpdateAsync(batch, It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(OmnichannelActivityBatchStatus.Started, batch.Status);
    }

    [Fact]
    public async Task CreateAndLoad_InvalidBatch_CreatesNothing()
    {
        var harness = CreateHarness();
        var batch = new OmnichannelActivityBatch { ItemId = "new-1" };
        harness.Manager
            .Setup(manager => manager.NewAsync(It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(batch);
        harness.Controller.ModelState.AddModelError("DisplayText", "Name is required.");

        var result = await harness.Controller.CreateAndLoadPost(SourceName);

        Assert.IsType<ViewResult>(result);
        harness.Manager.Verify(manager => manager.CreateAsync(It.IsAny<OmnichannelActivityBatch>(), It.IsAny<CancellationToken>()), Times.Never);
        harness.Manager.Verify(manager => manager.UpdateAsync(It.IsAny<OmnichannelActivityBatch>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal(OmnichannelActivityBatchStatus.New, batch.Status);
    }

    private static OmnichannelActivityBatch Batch(OmnichannelActivityBatchStatus status)
        => new()
        {
            ItemId = BatchId,
            DisplayText = BatchId,
            Status = status,
        };

    private static Harness CreateHarness(params OmnichannelActivityBatch[] batches)
        => CreateHarness(canManage: true, batches);

    private static Harness CreateHarness(bool canManage, params OmnichannelActivityBatch[] batches)
    {
        var harness = new Harness();

        foreach (var batch in batches)
        {
            harness.Manager
                .Setup(manager => manager.FindByIdAsync(batch.ItemId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(batch);
        }

        var authorization = new Mock<IAuthorizationService>();
        authorization
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(canManage ? AuthorizationResult.Success() : AuthorizationResult.Failed());

        var notifier = new Mock<INotifier>();
        notifier
            .Setup(value => value.AddAsync(It.IsAny<NotifyType>(), It.IsAny<LocalizedHtmlString>()))
            .Callback((NotifyType type, LocalizedHtmlString message) => harness.Notices.Add((type, message.Value)))
            .Returns(ValueTask.CompletedTask);
        notifier
            .Setup(value => value.AddAsync(It.IsAny<NotifyType>(), It.IsAny<LocalizedHtmlString>(), It.IsAny<NotifyContext>()))
            .Callback((NotifyType type, LocalizedHtmlString message, NotifyContext _) => harness.Notices.Add((type, message.Value)))
            .Returns(ValueTask.CompletedTask);

        var sources = new ActivityBatchSourceOptions();
        sources.AddSource(SourceName, entry => entry.DisplayName = new LocalizedString(SourceName, SourceName));

        harness.Controller = new ActivityBatchesController(
            harness.Manager.Object,
            authorization.Object,
            Mock.Of<IUpdateModelAccessor>(),
            harness.DisplayManager.Object,
            notifier.Object,
            Microsoft.Extensions.Options.Options.Create(sources),
            new TestHtmlLocalizer(),
            new TestStringLocalizer())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")], "Test")),
                },
            },
        };

        return harness;
    }

    private sealed class TestHtmlLocalizer : IHtmlLocalizer<ActivityBatchesController>
    {
        public LocalizedHtmlString this[string name] => new(name, name);

        public LocalizedHtmlString this[string name, params object[] arguments] => new(name, string.Format(CultureInfo.InvariantCulture, name, arguments));

        public LocalizedString GetString(string name) => new(name, name);

        public LocalizedString GetString(string name, params object[] arguments) => new(name, string.Format(CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

    private sealed class TestStringLocalizer : IStringLocalizer<ActivityBatchesController>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

    private sealed class Harness
    {
        public Mock<ICatalogManager<OmnichannelActivityBatch>> Manager { get; } = new();

        public Mock<IDisplayManager<OmnichannelActivityBatch>> DisplayManager { get; } = new();

        public ActivityBatchesController Controller { get; set; }

        public List<(NotifyType Type, string Message)> Notices { get; } = [];
    }
}
