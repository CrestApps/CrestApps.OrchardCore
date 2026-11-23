using CrestApps.OrchardCore.Banking.Checks;
using CrestApps.OrchardCore.Banking.Checks.Controllers;
using CrestApps.OrchardCore.Banking.Checks.Models;
using CrestApps.OrchardCore.Banking.Checks.Services;
using CrestApps.OrchardCore.Banking.Checks.ViewModels;
using CrestApps.OrchardCore.Banking.Models;
using CrestApps.OrchardCore.Core.SourceAccess;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;

namespace CrestApps.OrchardCore.Tests.Checks;

/// <summary>
/// Editing a check that waits to print: only before it prints, checked by the check rules, asked of the change handlers
/// (the module the check pays may refuse) and told to the observers with what it was.
/// </summary>
public sealed class CheckUpdateTests
{
    [Theory]
    [InlineData(CheckNumbering.AtPrint)]
    [InlineData(CheckNumbering.Next)]
    public async Task UpdateAsync_ACheckWaitingToPrint_SavesTheNewDetails_AndTellsTheHandlersAndTheObservers(CheckNumbering numbering)
    {
        // Arrange
        var context = new ChecksTestContext();
        var handler = new RecordingCheckChangeHandler();
        context.Handlers.Add(handler);
        var account = await context.BusinessAccountAsync();
        var check = await WriteAsync(context, account, numbering);
        var number = check.Number;
        context.Observer.Changes.Clear();

        // Act
        var result = await context.Service.UpdateAsync(
            check,
            new CheckDetails
            {
                PayeeName = "  Lee Roe  ",
                Amount = 250.555m,
                CheckDate = new DateOnly(2026, 11, 20),
                Memo = "Invoice 42",
            },
            "Wrong payee",
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        Assert.Equal(("Lee Roe", 250.56m, new DateOnly(2026, 11, 20), "Invoice 42"), (check.PayeeName, check.Amount, check.CheckDate, check.Memo));
        Assert.Equal((number, account.ItemId), (check.Number, check.BankAccountId));
        var asked = Assert.Single(handler.Contexts);
        Assert.Equal((CheckChangeKind.Updated, check.Status, "Wrong payee"), (asked.Kind, asked.NewStatus, asked.Reason));
        Assert.Equal(("Lee Roe", 250.56m), (asked.Details.PayeeName, asked.Details.Amount));
        Assert.Equal(("Pat Doe", 100m), handler.ValuesWhenAsked[0]);
        var change = Assert.Single(context.Observer.Changes);
        Assert.Equal((CheckChangeKind.Updated, "Wrong payee"), (change.Kind, change.Reason));
        Assert.Equal(("Pat Doe", 100m, new DateOnly(2026, 11, 13)), (change.PreviousDetails.PayeeName, change.PreviousDetails.Amount, change.PreviousDetails.CheckDate));
    }

    [Fact]
    public async Task UpdateAsync_RefusedByAHandler_SavesNothing_AndSaysWhy()
    {
        // Arrange
        var context = new ChecksTestContext();
        context.Handlers.Add(new RecordingCheckChangeHandler("The bill is already paid in full."));
        var check = await WriteAsync(context, await context.BusinessAccountAsync(), CheckNumbering.AtPrint);
        context.Observer.Changes.Clear();

        // Act
        var result = await context.Service.UpdateAsync(check, Details(500m), cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(["The bill is already paid in full."], result.Errors);
        Assert.Equal(100m, check.Amount);
        Assert.Empty(context.Observer.Changes);
    }

    [Fact]
    public async Task UpdateAsync_APrintedCheck_IsRefused_WithoutAskingTheHandlers()
    {
        // Arrange
        var context = new ChecksTestContext();
        var handler = new RecordingCheckChangeHandler();
        context.Handlers.Add(handler);
        var check = await context.IssueAsync(await context.BusinessAccountAsync());
        await context.Service.MarkPrintedAsync([check], "run1", TestContext.Current.CancellationToken);

        // Act
        var result = await context.Service.UpdateAsync(check, Details(500m), cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(1234.56m, check.Amount);
        Assert.Empty(handler.Contexts);
    }

    [Fact]
    public async Task CanEdit_OnlyAQueuedOrIssuedCheck_ThatIsNotHistoryOrASpoiledSheet()
    {
        // Arrange
        var context = new ChecksTestContext();
        var check = await context.IssueAsync(await context.BusinessAccountAsync());

        // Act and assert
        Assert.True(CheckStatuses.CanEdit(check));
        Assert.True(CheckStatuses.CanEdit(new CheckRecord { Status = CheckStatus.Queued }));
        Assert.False(CheckStatuses.CanEdit(new CheckRecord { Status = CheckStatus.Issued, IsHistorical = true }));
        Assert.False(CheckStatuses.CanEdit(new CheckRecord { Status = CheckStatus.Queued, IsSpoiled = true }));
        Assert.False(CheckStatuses.CanEdit(new CheckRecord { Status = CheckStatus.Printed }));
        Assert.False(CheckStatuses.CanEdit(new CheckRecord { Status = CheckStatus.Voided }));
    }

    [Fact]
    public async Task UpdateAsync_ANoAmountOrPayee_IsRefused()
    {
        // Arrange
        var context = new ChecksTestContext();
        var check = await WriteAsync(context, await context.BusinessAccountAsync(), CheckNumbering.AtPrint);

        // Act
        var result = await context.Service.UpdateAsync(check, new CheckDetails { PayeeName = " ", Amount = 0m, CheckDate = check.CheckDate }, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(2, result.Errors.Count);
        Assert.Equal(("Pat Doe", 100m), (check.PayeeName, check.Amount));
    }

    [Fact]
    public async Task UpdateAsync_Properties_ReplaceTheChecks_AndNullKeepsThem()
    {
        // Arrange
        var context = new ChecksTestContext();
        var check = await WriteAsync(context, await context.BusinessAccountAsync(), CheckNumbering.AtPrint);
        check.Properties["ExpenseLines"] = "old";

        // Act
        await context.Service.UpdateAsync(check, Details(120m), cancellationToken: TestContext.Current.CancellationToken);
        var kept = check.Properties["ExpenseLines"];
        var details = Details(130m);
        details.Properties = new Dictionary<string, object> { ["ExpenseLines"] = "new" };
        await context.Service.UpdateAsync(check, details, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("old", kept);
        Assert.Equal("new", check.Properties["ExpenseLines"]);
        Assert.Equal(130m, check.Amount);
    }

    [Fact]
    public async Task EditPost_ACheckWaitingToPrint_SavesWhatTheEditorsChanged()
    {
        // Arrange
        var context = new ChecksTestContext();
        var check = await WriteAsync(context, await context.BusinessAccountAsync(), CheckNumbering.AtPrint);
        var displayManager = new Mock<IDisplayManager<CheckRecord>>();

        displayManager
            .Setup(manager => manager.UpdateEditorAsync(It.IsAny<CheckRecord>(), It.IsAny<IUpdateModel>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback<CheckRecord, IUpdateModel, bool, string, string>((draft, _, isNew, _, _) =>
            {
                Assert.False(isNew);
                Assert.NotSame(check, draft);
                draft.Amount = 175m;
                draft.Properties["ExpenseLines"] = "edited";
            })
            .ReturnsAsync(Mock.Of<IShape>());

        var notifier = new RecordingNotifier();
        var controller = Controller(context, displayManager.Object, notifier);

        // Act
        var result = await controller.EditPost(check.ItemId, new CheckEditViewModel { Reason = "Wrong amount" });

        // Assert
        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(175m, check.Amount);
        Assert.Equal("edited", check.Properties["ExpenseLines"]);
        Assert.Contains(notifier.Entries, entry => entry.Type == NotifyType.Success);
        Assert.Equal("Wrong amount", context.Observer.Changes[^1].Reason);
    }

    [Fact]
    public async Task EditPost_APrintedCheck_GoesBackToTheRegister_AndSavesNothing()
    {
        // Arrange
        var context = new ChecksTestContext();
        var check = await context.IssueAsync(await context.BusinessAccountAsync());
        await context.Service.MarkPrintedAsync([check], "run1", TestContext.Current.CancellationToken);
        var displayManager = new Mock<IDisplayManager<CheckRecord>>();
        var notifier = new RecordingNotifier();
        var controller = Controller(context, displayManager.Object, notifier);

        // Act
        var result = await controller.EditPost(check.ItemId, new CheckEditViewModel());

        // Assert
        Assert.IsType<RedirectToActionResult>(result);
        Assert.Contains(notifier.Entries, entry => entry.Type == NotifyType.Error);
        displayManager.Verify(manager => manager.UpdateEditorAsync(It.IsAny<CheckRecord>(), It.IsAny<IUpdateModel>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Edit_WithoutManageChecks_IsForbidden()
    {
        // Arrange
        var context = new ChecksTestContext();
        var check = await WriteAsync(context, await context.BusinessAccountAsync(), CheckNumbering.AtPrint);
        var controller = Controller(context, Mock.Of<IDisplayManager<CheckRecord>>(), new RecordingNotifier(), ChecksPermissions.ViewCheckRegister);

        // Act
        var get = await controller.Edit(check.ItemId);
        var post = await controller.EditPost(check.ItemId, new CheckEditViewModel());

        // Assert
        Assert.IsType<ForbidResult>(get);
        Assert.IsType<ForbidResult>(post);
    }

    [Fact]
    public async Task IssuePost_PassesWhatOtherModulesEditorsPutOnTheCheck_ToTheWriteHandlers()
    {
        // Arrange
        var context = new ChecksTestContext();
        var account = await context.BusinessAccountAsync();
        var handler = new CapturingWriteHandler();
        context.WriteHandlers.Add(handler);
        var displayManager = new Mock<IDisplayManager<CheckRecord>>();

        displayManager
            .Setup(manager => manager.UpdateEditorAsync(It.IsAny<CheckRecord>(), It.IsAny<IUpdateModel>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback<CheckRecord, IUpdateModel, bool, string, string>((draft, _, _, _, _) =>
            {
                draft.BankAccountId = account.ItemId;
                draft.PayeeName = "Pat Doe";
                draft.Amount = 80m;
                draft.CheckDate = new DateOnly(2026, 11, 13);
                draft.Numbering = CheckNumbering.AtPrint;
                draft.Properties["ExpenseLines"] = "lines";
            })
            .ReturnsAsync(Mock.Of<IShape>());

        var controller = Controller(context, displayManager.Object, new RecordingNotifier());

        // Act
        var result = await controller.IssuePost();

        // Assert
        Assert.IsType<RedirectToActionResult>(result);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("lines", request.Properties["ExpenseLines"]);
        Assert.Equal("lines", Assert.Single(context.Store.Entries).Properties["ExpenseLines"]);
    }

    private static CheckDetails Details(decimal amount)
        => new()
        {
            PayeeName = "Pat Doe",
            Amount = amount,
            CheckDate = new DateOnly(2026, 11, 13),
        };

    private static async Task<CheckRecord> WriteAsync(ChecksTestContext context, BankAccount account, CheckNumbering numbering)
    {
        var result = await context.Service.IssueAsync(new CheckIssueRequest
        {
            BankAccountId = account.ItemId,
            PayeeName = "Pat Doe",
            Amount = 100m,
            CheckDate = new DateOnly(2026, 11, 13),
            Numbering = numbering,
        });

        Assert.True(result.Succeeded, string.Join(" ", result.Errors));

        return result.Check;
    }

    private static ChecksController Controller(ChecksTestContext context, IDisplayManager<CheckRecord> displayManager, RecordingNotifier notifier, global::OrchardCore.Security.Permissions.Permission grant = null)
    {
        var updateModelAccessor = new Mock<IUpdateModelAccessor>();

        updateModelAccessor.SetupGet(accessor => accessor.ModelUpdater).Returns(Mock.Of<IUpdateModel>());

        var controller = new ChecksController(
            context.Manager,
            context.Service,
            context.Printer(),
            Mock.Of<CrestApps.Core.Services.INamedCatalogManager<CheckLayout>>(),
            context.Banking.Manager,
            context.Store,
            new SourceAccessService([]),
            grant is null ? TestSecurity.Granting(ChecksPermissions.ManageChecks, ChecksPermissions.ViewCheckRegister) : TestSecurity.Granting(grant),
            updateModelAccessor.Object,
            displayManager,
            notifier,
            context.LocalClock,
            new EchoHtmlLocalizer<ChecksController>(),
            RecipeRoundTrip.Localizer<ChecksController>());

        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = TestSecurity.User("pat") } };

        return controller;
    }

    private sealed class RecordingCheckChangeHandler : ICheckChangeHandler
    {
        private readonly string _refusal;

        public RecordingCheckChangeHandler(string refusal = null)
        {
            _refusal = refusal;
        }

        public List<CheckChangingContext> Contexts { get; } = [];

        public List<(string PayeeName, decimal Amount)> ValuesWhenAsked { get; } = [];

        public Task ChangingAsync(CheckChangingContext context, CancellationToken cancellationToken = default)
        {
            Contexts.Add(context);
            ValuesWhenAsked.Add((context.Check.PayeeName, context.Check.Amount));

            if (_refusal is not null)
            {
                context.Refuse(_refusal);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class CapturingWriteHandler : CheckWriteHandlerBase
    {
        public List<CheckIssueRequest> Requests { get; } = [];

        public override Task WritingAsync(CheckWritingContext context, CancellationToken cancellationToken = default)
        {
            Requests.Add(context.Request);

            return Task.CompletedTask;
        }
    }
}
