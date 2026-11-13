using CrestApps.Core.Services;
using CrestApps.OrchardCore.Checks;
using CrestApps.OrchardCore.Checks.Controllers;
using CrestApps.OrchardCore.Checks.Core.Services;
using CrestApps.OrchardCore.Checks.Deployments;
using CrestApps.OrchardCore.Checks.Models;
using CrestApps.OrchardCore.Checks.ViewModels;
using CrestApps.OrchardCore.Core.SourceAccess;
using CrestApps.OrchardCore.SourceAccess;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Recipes.Models;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.Checks;

/// <summary>
/// The checks of a source are seen and acted on only by those the source allows, in the register, the Check Register
/// data set, the register export and every action on a check by its id.
/// </summary>
public sealed class ChecksAuthorizationTests
{
    private const string Paychecks = "Paychecks";

    [Fact]
    public async Task Register_ShowsOnlyTheChecksOfTheSourcesAPersonMaySee()
    {
        // Arrange
        var (context, checks) = await RegisterAsync();
        var access = Access();

        // Act
        var (patCount, pat) = await context.Store.ListPageAsync(new CheckRegisterQuery { Access = await access.GetFilterAsync(TestSecurity.User("pat"), SourceAccessLevel.View, TestContext.Current.CancellationToken) }, 0, 50, TestContext.Current.CancellationToken);
        var (leeCount, lee) = await context.Store.ListPageAsync(new CheckRegisterQuery { Access = await access.GetFilterAsync(TestSecurity.User("lee"), SourceAccessLevel.View, TestContext.Current.CancellationToken) }, 0, 50, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, patCount);
        Assert.Equal(new[] { checks.Manual.ItemId, checks.GroupA.ItemId }.Order(StringComparer.Ordinal), pat.Select(check => check.ItemId).Order(StringComparer.Ordinal));
        Assert.Equal(1, leeCount);
        Assert.Equal(checks.Manual.ItemId, Assert.Single(lee).ItemId);
    }

    [Fact]
    public async Task Register_WithoutASourceAccess_ShowsEveryCheck()
    {
        // Arrange
        var (context, _) = await RegisterAsync();
        var access = new SourceAccessService([]);

        // Act
        var filter = await access.GetFilterAsync(TestSecurity.User("lee"), SourceAccessLevel.View, TestContext.Current.CancellationToken);
        var (count, _) = await context.Store.ListPageAsync(new CheckRegisterQuery { Access = filter }, 0, 50, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(filter.IsUnrestricted);
        Assert.Equal(3, count);
    }

    [Fact]
    public async Task Export_WritesOnlyTheChecksOfTheSourcesThePersonMaySee()
    {
        // Arrange
        var (context, checks) = await RegisterAsync();
        var source = ExportSource(context, "lee", new RecordingNotifier(), ChecksPermissions.ViewCheckRegister);

        // Act
        var step = await RecipeRoundTrip.ExportAsync(source, new CheckRegisterDeploymentStep());

        // Assert
        var exported = step["Checks"]!.AsArray().Select(node => node!["number"]!.GetValue<long>()).ToList();
        Assert.Equal([checks.Manual.Number], exported);
    }

    [Fact]
    public async Task Export_RunWithoutASignedInPerson_WritesOnlyTheChecksNoSourceRestricts()
    {
        // Arrange: a remote deployment, with no request, of a step anyone who may view the register can save.
        var (context, checks) = await RegisterAsync();
        var source = new CheckRegisterDeploymentSource(
            context.Store,
            Access(),
            new HttpContextAccessor(),
            TestSecurity.Granting(),
            new RecordingNotifier(),
            new EchoHtmlLocalizer<CheckRegisterDeploymentSource>());

        // Act
        var step = await RecipeRoundTrip.ExportAsync(source, new CheckRegisterDeploymentStep());

        // Assert
        var exported = step["Checks"]!.AsArray().Select(node => node!["number"]!.GetValue<long>()).ToList();
        Assert.Equal([checks.Manual.Number], exported);
    }

    [Fact]
    public async Task Export_RunByAPersonWhoMayNotViewTheRegister_WritesNothing_AndSaysSo()
    {
        // Arrange
        var (context, _) = await RegisterAsync();
        var notifier = new RecordingNotifier();
        var source = ExportSource(context, "pat", notifier);
        var result = new DeploymentPlanResult(Mock.Of<IFileBuilder>(), new RecipeDescriptor());

        // Act
        await source.ProcessDeploymentStepAsync(new CheckRegisterDeploymentStep(), result);

        // Assert
        Assert.Empty(result.Steps);
        Assert.Contains(notifier.Entries, entry => entry.Type == NotifyType.Warning);
    }

    [Fact]
    public async Task Actions_OnACheckOfASourceThePersonMayNotSee_AreNotFound()
    {
        // Arrange
        var (context, checks) = await RegisterAsync();
        await context.Service.MarkPrintedAsync([checks.GroupA], "run1", TestContext.Current.CancellationToken);
        var controller = Controller(context, "lee");
        var id = checks.GroupA.ItemId;

        // Act + Assert
        Assert.IsType<NotFoundResult>(await controller.Void(id, null));
        Assert.IsType<NotFoundResult>(await controller.Stop(id, null));
        Assert.IsType<NotFoundResult>(await controller.Cleared(id, null));
        Assert.IsType<NotFoundResult>(await controller.Reissue(id, null));
        Assert.IsType<NotFoundResult>(await controller.Reprint(id));
        Assert.IsType<NotFoundResult>(await controller.ReprintPost(id, new CheckReprintViewModel { LayoutId = "layout1", Reason = "Jam" }));
        Assert.IsType<NotFoundResult>(await controller.Print([checks.Manual.ItemId, id], "layout1"));
        Assert.Equal(CheckStatus.Printed, checks.GroupA.Status);
    }

    [Fact]
    public async Task Actions_OnACheckOfASourceThePersonMaySee_AreDone()
    {
        // Arrange
        var (context, checks) = await RegisterAsync();
        var controller = Controller(context, "pat");

        // Act
        var result = await controller.Void(checks.GroupA.ItemId, "Spoiled");

        // Assert
        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(CheckStatus.Voided, checks.GroupA.Status);
    }

    [Fact]
    public async Task Actions_WithoutManageChecks_AreForbidden()
    {
        // Arrange
        var (context, checks) = await RegisterAsync();
        var controller = Controller(context, "pat", ChecksPermissions.ViewCheckRegister);

        // Act + Assert
        Assert.IsType<ForbidResult>(await controller.Void(checks.Manual.ItemId, null));
        Assert.IsType<ForbidResult>(await controller.Print([checks.Manual.ItemId], "layout1"));
        Assert.IsType<ForbidResult>(await controller.Reprint(checks.Manual.ItemId));
        Assert.Equal(CheckStatus.Issued, checks.Manual.Status);
    }

    [Fact]
    public async Task Print_APrintedCheck_IsRefused_UntilItIsReprintedWithAReason()
    {
        // Arrange
        var (context, checks) = await RegisterAsync();
        await context.Service.MarkPrintedAsync([checks.Manual], "run1", TestContext.Current.CancellationToken);
        var notifier = new RecordingNotifier();
        var controller = Controller(context, "pat", notifier: notifier);

        // Act
        var result = await controller.Print([checks.Manual.ItemId], "layout1");

        // Assert
        Assert.IsType<RedirectToActionResult>(result);
        Assert.Contains(notifier.Entries, entry => entry.Type == NotifyType.Error);
        Assert.Equal("run1", checks.Manual.PrintRunId);
    }

    [Fact]
    public async Task Cleared_WithoutADate_RecordsTheSitesLocalDay()
    {
        // Arrange: 03:30 UTC on the 14th is the evening of the 13th five hours behind UTC.
        var (context, checks) = await RegisterAsync();
        await context.Service.MarkPrintedAsync([checks.Manual], "run1", TestContext.Current.CancellationToken);
        context.Banking.Now = new DateTime(2026, 11, 14, 3, 30, 0, DateTimeKind.Utc);
        context.LocalClock.Offset = TimeSpan.FromHours(-5);
        var controller = Controller(context, "pat");

        // Act
        await controller.Cleared(checks.Manual.ItemId, null);

        // Assert
        Assert.Equal(new DateOnly(2026, 11, 13), checks.Manual.ClearedOn);
    }

    private static async Task<(ChecksTestContext Context, (CheckRecord Manual, CheckRecord GroupA, CheckRecord GroupB) Checks)> RegisterAsync()
    {
        var context = new ChecksTestContext();
        var account = await context.BusinessAccountAsync();
        var manual = await context.IssueAsync(account, 10m, "Manual payee");
        var groupA = await IssueAsync(context, account, "groupA");
        var groupB = await IssueAsync(context, account, "groupB");

        return (context, (manual, groupA, groupB));
    }

    private static async Task<CheckRecord> IssueAsync(ChecksTestContext context, global::CrestApps.OrchardCore.Banking.Models.BankAccount account, string scope)
    {
        var result = await context.Service.IssueAsync(new CheckIssueRequest
        {
            BankAccountId = account.ItemId,
            PayeeName = "Paid " + scope,
            Amount = 20,
            CheckDate = new DateOnly(2026, 11, 13),
            SourceKind = Paychecks,
            SourceId = "statement-" + scope,
            AccessScope = scope,
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, string.Join(" ", result.Errors));

        return result.Check;
    }

    // Pat runs payroll for group A; Lee sees no paycheck.
    private static SourceAccessService Access()
        => new([new FixedSourceAccess(Paychecks, new Dictionary<string, SourceAccessScope> { ["pat"] = SourceAccessScope.Of(["groupA"]) })]);

    private static CheckRegisterDeploymentSource ExportSource(ChecksTestContext context, string userName, RecordingNotifier notifier, params Permission[] grants)
        => new(
            context.Store,
            Access(),
            new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = TestSecurity.User(userName) } },
            TestSecurity.Granting(grants),
            notifier,
            new EchoHtmlLocalizer<CheckRegisterDeploymentSource>());

    private static ChecksController Controller(ChecksTestContext context, string userName, Permission grant = null, RecordingNotifier notifier = null)
    {
        var layouts = new Mock<INamedCatalogManager<CheckLayout>>();
        var layout = new CheckLayout { ItemId = "layout1", Name = "Pre-printed", Stock = CheckStock.PrePrinted };

        layouts.Setup(manager => manager.FindByIdAsync("layout1", It.IsAny<CancellationToken>())).ReturnsAsync(layout);
        layouts.Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([layout]);

        var updateModelAccessor = new Mock<IUpdateModelAccessor>();

        updateModelAccessor.SetupGet(accessor => accessor.ModelUpdater).Returns(Mock.Of<IUpdateModel>());

        var controller = new ChecksController(
            context.Manager,
            context.Service,
            context.Printer(),
            layouts.Object,
            context.Banking.Manager,
            context.Store,
            Access(),
            grant is null ? TestSecurity.Granting(ChecksPermissions.ManageChecks, ChecksPermissions.ViewCheckRegister) : TestSecurity.Granting(grant),
            updateModelAccessor.Object,
            Mock.Of<IDisplayManager<CheckRecord>>(),
            notifier ?? new RecordingNotifier(),
            context.LocalClock,
            new EchoHtmlLocalizer<ChecksController>(),
            RecipeRoundTrip.Localizer<ChecksController>());

        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = TestSecurity.User(userName) } };

        return controller;
    }
}
