using System.Globalization;
using System.Security.Claims;
using CrestApps.Core.Models;
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
using OrchardCore.Security;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements;

public sealed class ActivityBatchesControllerDeleteTests
{
    private const string BatchId = "batch-1";

    // A finished load is only the record of that load: the activities it created carry no reference back to it, so a
    // user allowed to delete loaded batches can remove it, and the activities are left alone.
    [Fact]
    public async Task Delete_LoadedBatch_WithTheDeleteLoadedPermission_RemovesTheLoad()
    {
        var batch = Batch(OmnichannelActivityBatchStatus.Loaded);
        var harness = CreateHarness(canDeleteLoaded: true, batch);
        harness.Manager
            .Setup(manager => manager.DeleteAsync(batch, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await harness.Controller.Delete(BatchId);

        Assert.IsType<RedirectToActionResult>(result);
        harness.Manager.Verify(manager => manager.DeleteAsync(batch, It.IsAny<CancellationToken>()), Times.Once);

        var notice = Assert.Single(harness.Notices);
        Assert.Equal(NotifyType.Success, notice.Type);
        Assert.Equal("The activity load has been deleted. The activities it loaded were kept.", notice.Message);
    }

    [Fact]
    public async Task Delete_LoadedBatch_WithoutTheDeleteLoadedPermission_KeepsTheLoad()
    {
        var batch = Batch(OmnichannelActivityBatchStatus.Loaded);
        var harness = CreateHarness(canDeleteLoaded: false, batch);

        var result = await harness.Controller.Delete(BatchId);

        Assert.IsType<RedirectToActionResult>(result);
        harness.Manager.Verify(manager => manager.DeleteAsync(It.IsAny<OmnichannelActivityBatch>(), It.IsAny<CancellationToken>()), Times.Never);

        var notice = Assert.Single(harness.Notices);
        Assert.Equal(NotifyType.Error, notice.Type);
    }

    // The load reads the batch again as it commits; removing it mid-load would have the load store it a second time.
    [Theory]
    [InlineData(OmnichannelActivityBatchStatus.Started)]
    [InlineData(OmnichannelActivityBatchStatus.Loading)]
    public async Task Delete_BatchBeingLoaded_IsRefusedEvenWithTheDeleteLoadedPermission(OmnichannelActivityBatchStatus status)
    {
        var batch = Batch(status);
        var harness = CreateHarness(canDeleteLoaded: true, batch);

        await harness.Controller.Delete(BatchId);

        harness.Manager.Verify(manager => manager.DeleteAsync(It.IsAny<OmnichannelActivityBatch>(), It.IsAny<CancellationToken>()), Times.Never);

        var notice = Assert.Single(harness.Notices);
        Assert.Equal(NotifyType.Error, notice.Type);
        Assert.Equal("This batch is being loaded and can't be removed.", notice.Message);
    }

    [Fact]
    public async Task Delete_NewBatch_NeedsOnlyTheManagePermission()
    {
        var batch = Batch(OmnichannelActivityBatchStatus.New);
        var harness = CreateHarness(canDeleteLoaded: false, batch);
        harness.Manager
            .Setup(manager => manager.DeleteAsync(batch, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await harness.Controller.Delete(BatchId);

        harness.Manager.Verify(manager => manager.DeleteAsync(batch, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(NotifyType.Success, Assert.Single(harness.Notices).Type);
    }

    // The bulk remove used to delete whatever it was posted, whatever its status, so a batch could be removed from
    // under its own load. It now follows the same rules as a single delete.
    [Fact]
    public async Task BulkRemove_SkipsBatchesBeingLoaded()
    {
        var loading = Batch(OmnichannelActivityBatchStatus.Loading, "loading");
        var fresh = Batch(OmnichannelActivityBatchStatus.New, "fresh");
        var harness = CreateHarness(canDeleteLoaded: true, loading, fresh);
        harness.Manager
            .Setup(manager => manager.DeleteAsync(fresh, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await harness.Controller.IndexPost(new CatalogEntryOptions { BulkAction = CatalogEntryAction.Remove }, ["loading", "fresh"]);

        harness.Manager.Verify(manager => manager.DeleteAsync(fresh, It.IsAny<CancellationToken>()), Times.Once);
        harness.Manager.Verify(manager => manager.DeleteAsync(loading, It.IsAny<CancellationToken>()), Times.Never);
        Assert.Contains(harness.Notices, notice => notice.Type == NotifyType.Warning);
        Assert.Contains(harness.Notices, notice => notice.Type == NotifyType.Success);
    }

    [Fact]
    public async Task BulkRemove_LoadedBatch_WithoutTheDeleteLoadedPermission_KeepsIt()
    {
        var loaded = Batch(OmnichannelActivityBatchStatus.Loaded);
        var harness = CreateHarness(canDeleteLoaded: false, loaded);

        await harness.Controller.IndexPost(new CatalogEntryOptions { BulkAction = CatalogEntryAction.Remove }, [BatchId]);

        harness.Manager.Verify(manager => manager.DeleteAsync(It.IsAny<OmnichannelActivityBatch>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static OmnichannelActivityBatch Batch(OmnichannelActivityBatchStatus status, string id = BatchId)
        => new()
        {
            ItemId = id,
            DisplayText = id,
            Status = status,
        };

    private static Harness CreateHarness(bool canDeleteLoaded, params OmnichannelActivityBatch[] batches)
    {
        var harness = new Harness();

        foreach (var batch in batches)
        {
            harness.Manager
                .Setup(manager => manager.FindByIdAsync(batch.ItemId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(batch);
        }

        // Managing batches is always granted here; whether a loaded batch may be deleted is the variable under test.
        var authorization = new Mock<IAuthorizationService>();
        authorization
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync((ClaimsPrincipal _, object _, IEnumerable<IAuthorizationRequirement> requirements) =>
                requirements.OfType<PermissionRequirement>().All(requirement =>
                    requirement.Permission.Name != OmnichannelConstants.Permissions.DeleteLoadedActivityBatches.Name || canDeleteLoaded)
                    ? AuthorizationResult.Success()
                    : AuthorizationResult.Failed());

        var notifier = new Mock<INotifier>();
        notifier
            .Setup(value => value.AddAsync(It.IsAny<NotifyType>(), It.IsAny<LocalizedHtmlString>()))
            .Callback((NotifyType type, LocalizedHtmlString message) => harness.Notices.Add((type, message.Value)))
            .Returns(ValueTask.CompletedTask);
        notifier
            .Setup(value => value.AddAsync(It.IsAny<NotifyType>(), It.IsAny<LocalizedHtmlString>(), It.IsAny<NotifyContext>()))
            .Callback((NotifyType type, LocalizedHtmlString message, NotifyContext _) => harness.Notices.Add((type, message.Value)))
            .Returns(ValueTask.CompletedTask);

        harness.Controller = new ActivityBatchesController(
            harness.Manager.Object,
            authorization.Object,
            Mock.Of<IUpdateModelAccessor>(),
            Mock.Of<IDisplayManager<OmnichannelActivityBatch>>(),
            notifier.Object,
            Microsoft.Extensions.Options.Options.Create(new ActivityBatchSourceOptions()),
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

        public ActivityBatchesController Controller { get; set; }

        public List<(NotifyType Type, string Message)> Notices { get; } = [];
    }
}
