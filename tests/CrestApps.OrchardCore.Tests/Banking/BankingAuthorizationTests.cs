using CrestApps.OrchardCore.Banking;
using CrestApps.OrchardCore.Banking.Controllers;
using CrestApps.OrchardCore.Banking.Deployments;
using CrestApps.OrchardCore.Banking.Drivers;
using CrestApps.OrchardCore.Banking.Models;
using CrestApps.OrchardCore.Banking.ViewModels;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Parties.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;

namespace CrestApps.OrchardCore.Tests.Banking;

/// <summary>
/// Who may see and change which bank accounts: the self-service pages, the admin editor's owner, the export, deletes
/// of accounts in use and the trace every number read leaves.
/// </summary>
public sealed class BankingAuthorizationTests
{
    private static readonly PartyRef _pat = new(PartyKinds.User, "pat-id");
    private static readonly PartyRef _lee = new(PartyKinds.User, "lee-id");

    [Fact]
    public async Task MyAccounts_SomeoneElsesAccount_IsNotFound_ForEditViewEditPostAndClose()
    {
        // Arrange
        var context = new BankingTestContext();
        var account = await context.CreateAsync(owner: _lee);
        var controller = MyAccountsController(context, "pat");

        // Act
        var edit = await controller.Edit(account.ItemId);
        var editPost = await controller.EditPost(account.ItemId);
        var close = await controller.Close(account.ItemId);

        // Assert
        Assert.IsType<NotFoundResult>(edit);
        Assert.IsType<NotFoundResult>(editPost);
        Assert.IsType<NotFoundResult>(close);
        Assert.Equal(BankAccountStatus.Unverified, (await context.Manager.FindByIdAsync(account.ItemId, TestContext.Current.CancellationToken)).Status);
    }

    [Fact]
    public async Task MyAccounts_WithoutThePermission_IsForbidden()
    {
        // Arrange
        var context = new BankingTestContext();
        var account = await context.CreateAsync(owner: _pat);
        var controller = MyAccountsController(context, "pat", granted: false);

        // Act
        var close = await controller.Close(account.ItemId);

        // Assert
        Assert.IsType<ForbidResult>(close);
    }

    [Fact]
    public async Task MyAccounts_AClosedAccount_IsNotChanged()
    {
        // Arrange
        var context = new BankingTestContext();
        var account = await context.CreateAsync(owner: _pat);
        Assert.True(await context.Service.ChangeStatusAsync(account, BankAccountStatus.Closed, "Closed", cancellationToken: TestContext.Current.CancellationToken));
        var notifier = new RecordingNotifier();
        var displayManager = new Mock<IDisplayManager<BankAccount>>();
        var controller = MyAccountsController(context, "pat", notifier: notifier, displayManager: displayManager);

        // Act
        var result = await controller.EditPost(account.ItemId);

        // Assert
        Assert.IsType<RedirectToActionResult>(result);
        Assert.Contains(notifier.Entries, entry => entry.Type == NotifyType.Error);
        displayManager.Verify(manager => manager.UpdateEditorAsync(It.IsAny<BankAccount>(), It.IsAny<IUpdateModel>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task OwnEditor_APostedOwnerThePersonIsNot_IsNotSaved()
    {
        // Arrange
        var context = new BankingTestContext();
        var account = await context.CreateAsync(owner: _pat);
        var driver = Driver(context, "pat");

        // Act
        await driver.UpdateAsync(account, OwnContext(new BankAccountViewModel { Name = "Mine", HolderName = "Pat", Owner = _lee.ToString(), Country = "US", Currency = "USD" }, out _));

        // Assert
        Assert.Equal(_pat, account.Owner);
    }

    [Fact]
    public async Task OwnEditor_ANewAccountForSomeoneElse_IsRefused()
    {
        // Arrange
        var context = new BankingTestContext();
        var account = await context.Manager.NewAsync(cancellationToken: TestContext.Current.CancellationToken);
        var driver = Driver(context, "pat");

        // Act
        await driver.UpdateAsync(account, OwnContext(new BankAccountViewModel { Name = "Mine", HolderName = "Pat", Owner = _lee.ToString(), RoutingNumber = BankingTestContext.ValidRoutingNumber, AccountNumber = "123456789" }, out var updater, isNew: true));

        // Assert
        Assert.False(updater.ModelState.IsValid);
        Assert.True(account.Owner.IsEmpty);
    }

    [Fact]
    public async Task OwnEditor_APostedStatus_IsIgnored()
    {
        // Arrange
        var context = new BankingTestContext();
        var account = await context.CreateAsync(owner: _pat);
        var driver = Driver(context, "pat");

        // Act
        await driver.UpdateAsync(account, OwnContext(new BankAccountViewModel { Name = "Mine", HolderName = "Pat", Owner = _pat.ToString(), Status = BankAccountStatus.Verified, Country = "US", Currency = "USD" }, out _));

        // Assert
        Assert.Equal(BankAccountStatus.Unverified, account.Status);
    }

    [Theory]
    [InlineData("Vendor:9")]
    [InlineData("Staff:missing")]
    [InlineData("not-a-party")]
    public async Task AdminEditor_AnOwnerOfAnUnknownKindOrThatDoesNotExist_IsRefused(string owner)
    {
        // Arrange
        var context = new BankingTestContext();
        var account = await context.CreateAsync();
        var driver = Driver(context, "admin");
        var posted = PostedFormUpdateModel.CreateContext(new BankAccountViewModel { Name = "Main", HolderName = "Contoso", Owner = owner, Country = "US", Currency = "USD", Status = account.Status });

        // Act
        await driver.UpdateAsync(account, posted);

        // Assert
        Assert.False(posted.Updater.ModelState.IsValid);
        Assert.True(account.Owner.IsEmpty);
    }

    [Fact]
    public async Task AdminEditor_AnOwnerThatExists_IsSaved()
    {
        // Arrange
        var context = new BankingTestContext();
        var account = await context.CreateAsync();
        var driver = Driver(context, "admin");
        var posted = PostedFormUpdateModel.CreateContext(new BankAccountViewModel { Name = "Main", HolderName = "Contoso", Owner = "Staff:1", Country = "US", Currency = "USD", Status = account.Status });

        // Act
        await driver.UpdateAsync(account, posted);

        // Assert
        Assert.True(posted.Updater.ModelState.IsValid);
        Assert.Equal(new PartyRef("Staff", "1"), account.Owner);
    }

    [Fact]
    public async Task Export_RunByAUserWhoMayNotManageBankAccounts_WritesNothing_AndSaysSo()
    {
        // Arrange
        var context = new BankingTestContext();
        await context.CreateAsync();
        var notifier = new RecordingNotifier();
        var source = new BankAccountsDeploymentSource(
            context.Manager,
            context.Service,
            new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = TestSecurity.User("lee") } },
            TestSecurity.Granting(BankingPermissions.ManageOwnBankAccounts),
            notifier,
            new EchoHtmlLocalizer<BankAccountsDeploymentSource>(),
            RecipeRoundTrip.Localizer<BankAccountsDeploymentSource>());
        var result = new global::OrchardCore.Deployment.DeploymentPlanResult(Mock.Of<global::OrchardCore.Deployment.IFileBuilder>(), new global::OrchardCore.Recipes.Models.RecipeDescriptor());

        // Act
        await source.ProcessDeploymentStepAsync(new BankAccountsDeploymentStep(), result);

        // Assert
        Assert.Empty(result.Steps);
        Assert.Contains(notifier.Entries, entry => entry.Type == NotifyType.Warning);
    }

    [Fact]
    public async Task Delete_AnAccountInUse_IsRefused_AndSaysWhy()
    {
        // Arrange
        var context = new BankingTestContext();
        var used = await context.CreateAsync(name: "Operating");
        var unused = await context.CreateAsync(name: "Spare");
        context.Usages.Add(new FixedBankAccountUsage("Checks are drawn on it.", used.ItemId));

        // Act
        var usages = await context.Service.GetUsagesAsync(used, TestContext.Current.CancellationToken);
        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(async () => await context.Manager.DeleteAsync(used, TestContext.Current.CancellationToken));
        var deleted = await context.Manager.DeleteAsync(unused, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["Checks are drawn on it."], usages);
        Assert.Contains("Checks are drawn on it.", refusal.Message, StringComparison.Ordinal);
        Assert.NotNull(await context.Manager.FindByIdAsync(used.ItemId, TestContext.Current.CancellationToken));
        Assert.True(deleted);
    }

    [Fact]
    public async Task Reveal_AlwaysWritesALogEntry_WithTheAccountAndWho_ButNeverTheNumber()
    {
        // Arrange
        var context = new BankingTestContext();
        var account = await context.CreateAsync("987654321");
        context.HttpContextAccessor.HttpContext = new DefaultHttpContext { User = TestSecurity.User("pat") };

        // Act
        var number = await context.Service.RevealAccountNumberAsync(account, "Shown on the bank account page", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("987654321", number);
        var entry = Assert.Single(context.Logger.Entries);
        Assert.Contains(account.ItemId, entry.Message, StringComparison.Ordinal);
        Assert.Contains("pat", entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("987654321", entry.Message, StringComparison.Ordinal);
        Assert.Equal("pat-id", context.Observer.Changes[^1].ActorId);
    }

    [Fact]
    public async Task Changes_CarryTheUserIdOfWhoMadeThem()
    {
        // Arrange
        var context = new BankingTestContext();
        context.HttpContextAccessor.HttpContext = new DefaultHttpContext { User = TestSecurity.User("pat") };

        // Act
        await context.CreateAsync();

        // Assert
        var change = Assert.Single(context.Observer.Changes);
        Assert.Equal("pat", change.Actor);
        Assert.Equal("pat-id", change.ActorId);
    }

    private static MyBankAccountsController MyAccountsController(BankingTestContext context, string userName, bool granted = true, RecordingNotifier notifier = null, Mock<IDisplayManager<BankAccount>> displayManager = null)
    {
        var updateModelAccessor = new Mock<IUpdateModelAccessor>();

        updateModelAccessor.SetupGet(accessor => accessor.ModelUpdater).Returns(Mock.Of<IUpdateModel>());

        var controller = new MyBankAccountsController(
            context.Manager,
            context.Service,
            [],
            granted ? TestSecurity.Granting(BankingPermissions.ManageOwnBankAccounts) : TestSecurity.Granting(),
            updateModelAccessor.Object,
            (displayManager ?? new Mock<IDisplayManager<BankAccount>>()).Object,
            notifier ?? new RecordingNotifier(),
            new EchoHtmlLocalizer<MyBankAccountsController>());

        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = TestSecurity.User(userName) } };

        return controller;
    }

    private static BankAccountDisplayDriver Driver(BankingTestContext context, string userName)
    {
        var directory = new Mock<IPartyDirectory>();

        directory.SetupGet(entry => entry.Kinds).Returns(["Staff", PartyKinds.User]);
        directory
            .Setup(entry => entry.GetNameAsync(It.IsAny<PartyRef>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PartyRef party, CancellationToken _) => party == new PartyRef("Staff", "1") ? "Pat Doe" : null);
        directory
            .Setup(entry => entry.GetNamesAsync(It.IsAny<IEnumerable<PartyRef>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<PartyRef, string>());

        return new BankAccountDisplayDriver(
            context.Service,
            directory.Object,
            [],
            new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = TestSecurity.User(userName) } },
            RecipeRoundTrip.Localizer<BankAccountDisplayDriver>());
    }

    private static UpdateEditorContext OwnContext(BankAccountViewModel posted, out IUpdateModel updater, bool isNew = false)
    {
        var context = new UpdateEditorContext(
            Mock.Of<IShape>(),
            BankAccountEditorGroups.Own,
            isNew,
            string.Empty,
            Mock.Of<IShapeFactory>(),
            null,
            new PostedFormUpdateModel(posted));

        updater = context.Updater;

        return context;
    }
}
