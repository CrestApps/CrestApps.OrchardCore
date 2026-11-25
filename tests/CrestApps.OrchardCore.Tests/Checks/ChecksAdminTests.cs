using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Banking.Checks;
using CrestApps.OrchardCore.Banking.Checks.Controllers;
using CrestApps.OrchardCore.Banking.Checks.Drivers;
using CrestApps.OrchardCore.Banking.Checks.Models;
using CrestApps.OrchardCore.Banking.Checks.Recipes;
using CrestApps.OrchardCore.Banking.Checks.ViewModels;
using CrestApps.OrchardCore.Banking.Models;
using CrestApps.OrchardCore.Core.SourceAccess;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;

namespace CrestApps.OrchardCore.Tests.Checks;

/// <summary>
/// The admin pages of checks: print runs of queued checks, printing with an account's default layout, voids on a date,
/// un-clearing, the Checks card of a bank account and the register recipe.
/// </summary>
public sealed class ChecksAdminTests
{
    [Fact]
    public async Task ConfirmPrintRun_WithAMisprint_PrintsTheOthers_AndKeepsTheSpoiledSheet()
    {
        // Arrange
        var context = new ChecksTestContext();
        var account = await context.BusinessAccountAsync();
        var first = await QueueAsync(context, account, "Pat");
        var second = await QueueAsync(context, account, "Lee");
        var notifier = new RecordingNotifier();
        var controller = Controller(context, notifier);

        // Act
        var result = await controller.ConfirmPrintRun(new CheckPrintRunViewModel
        {
            BankAccountId = account.ItemId,
            LayoutId = "layout1",
            FirstNumber = "3001",
            CheckIds = [first.ItemId, second.ItemId],
            MisprintedCheckIds = [second.ItemId],
            PrintRunId = "run1",
        });

        // Assert
        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal((3001L, CheckStatus.Printed), (first.Number, first.Status));
        Assert.Equal(CheckStatus.Queued, second.Status);
        Assert.Contains(context.Store.Entries, entry => entry.IsSpoiled && entry.Number == 3002);
        Assert.Contains(notifier.Entries, entry => entry.Type == NotifyType.Warning);
    }

    [Fact]
    public async Task PrintRunPost_AQueuedCheckOfAnotherAccount_ShowsTheFormAgain_AndSavesNothing()
    {
        // Arrange
        var context = new ChecksTestContext();
        var operating = await context.BusinessAccountAsync("111122223333", "Operating");
        var payroll = await context.BusinessAccountAsync("444455556666", "Payroll");
        var other = await QueueAsync(context, payroll, "Lee");
        var controller = Controller(context);

        // Act
        var result = await controller.PrintRunPost(new CheckPrintRunViewModel { BankAccountId = operating.ItemId, LayoutId = "layout1", FirstNumber = "3001", CheckIds = [other.ItemId] });

        // Assert
        var view = Assert.IsType<ViewResult>(result);
        Assert.Null(view.ViewName);
        Assert.False(controller.ModelState.IsValid);
        Assert.Equal(0, other.Number);
    }

    [Fact]
    public async Task PrintRunPost_ValidRun_ShowsTheConfirmPage_WithTheChecksInTheChosenOrder()
    {
        // Arrange
        var context = new ChecksTestContext();
        var account = await context.BusinessAccountAsync();
        var first = await QueueAsync(context, account, "Pat");
        var second = await QueueAsync(context, account, "Lee");
        var controller = Controller(context);

        // Act
        var result = await controller.PrintRunPost(new CheckPrintRunViewModel { BankAccountId = account.ItemId, LayoutId = "layout1", FirstNumber = "3001", CheckIds = [second.ItemId, first.ItemId] });

        // Assert
        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("PrintRunConfirm", view.ViewName);
        var model = Assert.IsType<CheckPrintRunViewModel>(view.Model);
        Assert.Equal([second.ItemId, first.ItemId], model.Checks.Select(check => check.ItemId));
        Assert.Equal(3001, model.First);
        Assert.False(string.IsNullOrEmpty(model.PrintRunId));
    }

    [Fact]
    public async Task PrintRunPdf_PrintsTheRun_WithoutRecordingIt()
    {
        // Arrange
        var context = new ChecksTestContext();
        var account = await context.BusinessAccountAsync();
        var queued = await QueueAsync(context, account, "Pat");
        var controller = Controller(context);

        // Act
        var result = await controller.PrintRunPdf(new CheckPrintRunViewModel { BankAccountId = account.ItemId, LayoutId = "layout1", FirstNumber = "3001", CheckIds = [queued.ItemId], PrintRunId = "run1" });

        // Assert
        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal((CheckStatus.Queued, 0L), (queued.Status, queued.Number));
    }

    [Fact]
    public async Task Print_QueuedChecksOfOneAccount_GoToItsPrintRun()
    {
        // Arrange
        var context = new ChecksTestContext();
        var account = await context.BusinessAccountAsync();
        var queued = await QueueAsync(context, account, "Pat");
        var controller = Controller(context);

        // Act
        var result = await controller.Print([queued.ItemId], ChecksController.DefaultLayout);

        // Assert
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(ChecksController.PrintRun), redirect.ActionName);
        Assert.Equal(account.ItemId, redirect.RouteValues["bankAccountId"]);
    }

    [Fact]
    public async Task Print_WithTheDefaultLayout_UsesTheBankAccountsLayout()
    {
        // Arrange
        var context = new ChecksTestContext();
        var account = await context.BusinessAccountAsync();
        account.Put(new BankAccountCheckSettings { DefaultLayoutId = "layout1" });
        await context.Banking.Manager.UpdateAsync(account, cancellationToken: TestContext.Current.CancellationToken);
        var check = await context.IssueAsync(account);
        var controller = Controller(context);

        // Act
        var result = await controller.Print([check.ItemId], ChecksController.DefaultLayout);

        // Assert
        Assert.IsType<FileContentResult>(result);
        Assert.Equal(CheckStatus.Printed, check.Status);
    }

    [Fact]
    public async Task Print_WithTheDefaultLayoutOfAnAccountThatHasNone_SaysSo()
    {
        // Arrange
        var context = new ChecksTestContext();
        var account = await context.BusinessAccountAsync();
        var check = await context.IssueAsync(account);
        var notifier = new RecordingNotifier();
        var controller = Controller(context, notifier);

        // Act
        var result = await controller.Print([check.ItemId], ChecksController.DefaultLayout);

        // Assert
        Assert.IsType<RedirectToActionResult>(result);
        Assert.Contains(notifier.Entries, entry => entry.Type == NotifyType.Error);
        Assert.Equal(CheckStatus.Issued, check.Status);
    }

    [Fact]
    public async Task Void_OnADate_VoidsOnThatDate()
    {
        // Arrange
        var context = new ChecksTestContext();
        var account = await context.BusinessAccountAsync();
        var check = await context.IssueAsync(account);
        var controller = Controller(context);

        // Act
        var result = await controller.Void(check.ItemId, "Wrong amount", "2026-11-18");

        // Assert
        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal((CheckStatus.Voided, new DateOnly(2026, 11, 18)), (check.Status, check.VoidedOn));
    }

    [Fact]
    public async Task Void_BeforeTheCheckDate_ShowsThePageAgainWithTheReason()
    {
        // Arrange
        var context = new ChecksTestContext();
        var account = await context.BusinessAccountAsync();
        var check = await context.IssueAsync(account);
        var controller = Controller(context);

        // Act
        var result = await controller.Void(check.ItemId, null, "2026-01-01");

        // Assert
        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.Equal(CheckStatus.Issued, check.Status);
    }

    [Fact]
    public async Task VoidPage_OffersTodayInTheSitesTimeZone()
    {
        // Arrange
        var context = new ChecksTestContext();
        var account = await context.BusinessAccountAsync();
        var check = await context.IssueAsync(account);
        context.LocalClock.Offset = TimeSpan.FromHours(10);
        var controller = Controller(context);

        // Act
        var result = await controller.Void(check.ItemId);

        // Assert
        var model = Assert.IsType<CheckVoidViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal("2026-11-14", model.VoidedOn);
    }

    [Fact]
    public async Task Unclear_AClearedCheck_GoesBackToPrinted()
    {
        // Arrange
        var context = new ChecksTestContext();
        var account = await context.BusinessAccountAsync();
        var check = await context.IssueAsync(account);
        await context.Service.MarkPrintedAsync([check], "run1", TestContext.Current.CancellationToken);
        await context.Service.MarkClearedAsync(check, new DateOnly(2026, 11, 20), TestContext.Current.CancellationToken);
        var controller = Controller(context);

        // Act
        var result = await controller.Unclear(check.ItemId, null);

        // Assert
        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(CheckStatus.Printed, check.Status);
        Assert.Null(check.ClearedOn);
    }

    [Fact]
    public async Task Unclear_WithoutManageChecks_IsForbidden()
    {
        // Arrange
        var context = new ChecksTestContext();
        var account = await context.BusinessAccountAsync();
        var check = await context.IssueAsync(account);
        var controller = Controller(context, grant: ChecksPermissions.ViewCheckRegister);

        // Act
        var result = await controller.Unclear(check.ItemId, null);

        // Assert
        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task ChecksCard_SavesTheDefaultLayoutOfABusinessAccount_AndRefusesAnUnknownOne()
    {
        // Arrange
        var context = new ChecksTestContext();
        var account = await context.BusinessAccountAsync();
        var driver = new BankAccountCheckSettingsDisplayDriver(Layouts().Object, RecipeRoundTrip.Localizer<BankAccountCheckSettingsDisplayDriver>());
        var good = PostedFormUpdateModel.CreateContext(new BankAccountCheckSettingsViewModel { Shown = true, DefaultLayoutId = "layout1" });
        var bad = PostedFormUpdateModel.CreateContext(new BankAccountCheckSettingsViewModel { Shown = true, DefaultLayoutId = "missing" });

        // Act
        await driver.UpdateAsync(account, good);
        var saved = account.TryGet<BankAccountCheckSettings>(out var settings) ? settings.DefaultLayoutId : null;
        await driver.UpdateAsync(account, bad);

        // Assert
        Assert.True(good.Updater.ModelState.IsValid);
        Assert.Equal("layout1", saved);
        Assert.False(bad.Updater.ModelState.IsValid);
        Assert.Equal("layout1", account.TryGet<BankAccountCheckSettings>(out var kept) ? kept.DefaultLayoutId : null);
    }

    [Fact]
    public async Task ChecksCard_IsNotOnAPayeesAccount()
    {
        // Arrange
        var context = new ChecksTestContext();
        var account = await context.Banking.CreateAsync(owner: new PartyRef("Staff", "s1"));
        var driver = new BankAccountCheckSettingsDisplayDriver(Layouts().Object, RecipeRoundTrip.Localizer<BankAccountCheckSettingsDisplayDriver>());

        // Act
        var result = await driver.UpdateAsync(account, PostedFormUpdateModel.CreateContext(new BankAccountCheckSettingsViewModel { Shown = true, DefaultLayoutId = "layout1" }));

        // Assert
        Assert.Null(result);
        Assert.False(account.TryGet<BankAccountCheckSettings>(out _));
    }

    [Fact]
    public async Task CheckRegisterRecipe_KeepsTheVoidDateAndTheNumbering_AndRefusesAQueuedCheck()
    {
        // Arrange
        var context = new ChecksTestContext();
        var account = await context.BusinessAccountAsync();
        var step = new JsonObject
        {
            ["name"] = ChecksConstants.RegisterRecipeStepName,
            [CheckRegisterRecipeStep.ChecksProperty] = new JsonArray(
                new JsonObject
                {
                    ["bankAccountId"] = account.ItemId,
                    ["number"] = 700,
                    ["payeeName"] = "Old payee",
                    ["amount"] = 12.5,
                    ["checkDate"] = "2025-06-30",
                    ["status"] = "Voided",
                    ["voidedOn"] = "2025-07-02",
                    ["numbering"] = "Handwritten",
                },
                new JsonObject
                {
                    ["bankAccountId"] = account.ItemId,
                    ["number"] = 701,
                    ["payeeName"] = "Waiting",
                    ["status"] = "Queued",
                }),
        };

        // Act
        var errors = await RecipeRoundTrip.RunAsync(new CheckRegisterRecipeStep(context.Manager), step);

        // Assert
        Assert.Single(errors);
        var imported = Assert.Single(context.Store.Entries);
        Assert.Equal((new DateOnly(2025, 7, 2), CheckNumbering.Handwritten), (imported.VoidedOn, imported.Numbering));
    }

    [Fact]
    public void PositivePayRecipe_LeavesOutChecksThatWaitForANumber()
    {
        // Arrange
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CrestApps.OrchardCore.slnx")))
        {
            directory = directory.Parent;
        }

        var recipe = JsonNode.Parse(File.ReadAllText(Path.Combine(directory!.FullName, "src", "Modules", "CrestApps.OrchardCore.Banking", "Recipes", "checks-positive-pay.recipe.json")))!;

        // Act
        var source = recipe["steps"]!.AsArray()
            .Single(step => step!["name"]!.GetValue<string>() == "DataPipelines")!["Pipelines"]![0]!["Definition"]!["Steps"]!.AsArray()
            .Single(step => step!["Type"]!.GetValue<string>() == "DataSource")!["Properties"]!["DataSourceStepSettings"]!;

        // Assert
        var filter = Assert.Single(source["Filters"]!.AsArray())!;
        Assert.Equal(("Status", "NotEquals", "Queued"), (filter["Field"]!.GetValue<string>(), filter["Operator"]!.GetValue<string>(), filter["Values"]![0]!.GetValue<string>()));
    }

    private static async Task<CheckRecord> QueueAsync(ChecksTestContext context, BankAccount account, string payee)
    {
        var result = await context.Service.IssueAsync(new CheckIssueRequest
        {
            BankAccountId = account.ItemId,
            PayeeName = payee,
            Amount = 100m,
            CheckDate = new DateOnly(2026, 11, 13),
            Numbering = CheckNumbering.AtPrint,
        });

        Assert.True(result.Succeeded, string.Join(" ", result.Errors));

        return result.Check;
    }

    private static Mock<INamedCatalogManager<CheckLayout>> Layouts()
    {
        var layouts = new Mock<INamedCatalogManager<CheckLayout>>();
        var layout = new CheckLayout { ItemId = "layout1", Name = "Pre-printed", Stock = CheckStock.PrePrinted };

        layouts.Setup(manager => manager.FindByIdAsync("layout1", It.IsAny<CancellationToken>())).ReturnsAsync(layout);
        layouts.Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([layout]);

        return layouts;
    }

    private static ChecksController Controller(ChecksTestContext context, RecordingNotifier notifier = null, global::OrchardCore.Security.Permissions.Permission grant = null)
    {
        var updateModelAccessor = new Mock<IUpdateModelAccessor>();

        updateModelAccessor.SetupGet(accessor => accessor.ModelUpdater).Returns(Mock.Of<IUpdateModel>());

        var controller = new ChecksController(
            context.Manager,
            context.Service,
            context.Printer(),
            Layouts().Object,
            context.Banking.Manager,
            context.Store,
            new SourceAccessService([]),
            grant is null ? TestSecurity.Granting(ChecksPermissions.ManageChecks, ChecksPermissions.ViewCheckRegister) : TestSecurity.Granting(grant),
            updateModelAccessor.Object,
            Mock.Of<IDisplayManager<CheckRecord>>(),
            Mock.Of<IDisplayManager<CheckVoidViewModel>>(),
            Mock.Of<IDisplayManager<CheckReprintViewModel>>(),
            Mock.Of<IDisplayManager<CheckNextNumberViewModel>>(),
            Mock.Of<IDisplayManager<CheckPrintRunViewModel>>(),
            notifier ?? new RecordingNotifier(),
            context.LocalClock,
            new EchoHtmlLocalizer<ChecksController>(),
            RecipeRoundTrip.Localizer<ChecksController>());

        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = TestSecurity.User("pat") } };

        return controller;
    }
}
