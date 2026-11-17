using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Core.SourceAccess;
using CrestApps.OrchardCore.Banking.Disbursements;
using CrestApps.OrchardCore.Banking.Disbursements.Controllers;
using CrestApps.OrchardCore.Banking.Disbursements.Deployments;
using CrestApps.OrchardCore.Banking.Disbursements.Models;
using CrestApps.OrchardCore.Banking.Disbursements.Recipes;
using CrestApps.OrchardCore.Banking.Disbursements.ViewModels;
using CrestApps.OrchardCore.SourceAccess;
using CrestApps.OrchardCore.Tests.Checks;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Environment.Extensions.Features;
using OrchardCore.Environment.Shell;
using OrchardCore.Recipes.Models;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.Disbursements;

/// <summary>
/// The disbursements of a source are seen and acted on only by those the source allows: the list, the batches, the
/// export and every action by id; and the permissions on every path.
/// </summary>
public sealed class DisbursementsAuthorizationTests
{
    private const string PayStatement = "PayStatement";

    [Fact]
    public async Task Actions_OnADisbursementOfASourceThePersonMayNotSee_AreNotFound()
    {
        // Arrange
        var (context, _, groupA) = await SeedAsync();
        var disbursements = DisbursementsController(context, "lee");

        // Act + Assert
        Assert.IsType<NotFoundResult>(await disbursements.Cancel(groupA.ItemId, null));
        Assert.IsType<NotFoundResult>(await disbursements.IndexBulkPost([groupA.ItemId], new DisbursementsIndexViewModel { Options = new() { BulkAction = "Nacha" } }));
        Assert.Equal(DisbursementStatus.Pending, groupA.Status);
    }

    [Fact]
    public async Task Actions_OnADisbursementOfASourceThePersonMaySee_AreDone()
    {
        // Arrange
        var (context, _, groupA) = await SeedAsync();
        var disbursements = DisbursementsController(context, "pat");

        // Act
        var result = await disbursements.Cancel(groupA.ItemId, "Wrong amount");

        // Assert
        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(DisbursementStatus.Canceled, groupA.Status);
    }

    [Fact]
    public async Task BatchActions_OnABatchHoldingASourceThePersonMayNotSee_AreNotFound()
    {
        // Arrange
        var (context, manual, groupA) = await SeedAsync();
        var (batch, _) = await context.Service.CreateBatchAsync("Nacha", [groupA.ItemId], new DateOnly(2026, 11, 16), TestContext.Current.CancellationToken);
        var batches = BatchesController(context, "lee");

        // Act + Assert
        Assert.IsType<NotFoundResult>(await batches.Release(batch.ItemId));
        Assert.IsType<NotFoundResult>(await batches.Cancel(batch.ItemId));
        Assert.IsType<NotFoundResult>(await batches.Settle(batch.ItemId));
        Assert.IsType<NotFoundResult>(await batches.Download(batch.ItemId));
        Assert.Equal(DisbursementBatchStatus.Open, batch.Status);
        Assert.NotNull(manual);
    }

    [Fact]
    public async Task Download_SendsTheBankFile_NeverCached()
    {
        // Arrange
        var (context, _, groupA) = await SeedAsync();
        var (batch, _) = await context.Service.CreateBatchAsync("Nacha", [groupA.ItemId], new DateOnly(2026, 11, 16), TestContext.Current.CancellationToken);
        await context.Service.ReleaseBatchAsync(batch, TestContext.Current.CancellationToken);
        var batches = BatchesController(context, "pat");

        // Act
        var result = await batches.Download(batch.ItemId);

        // Assert
        Assert.IsType<FileContentResult>(result);
        Assert.Equal("no-store", batches.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task Actions_WithoutManageDisbursements_AreForbidden()
    {
        // Arrange
        var (context, manual, _) = await SeedAsync();
        var disbursements = DisbursementsController(context, "pat", DisbursementsPermissions.ViewDisbursements);
        var batches = BatchesController(context, "pat", DisbursementsPermissions.ViewDisbursements);

        // Act + Assert
        Assert.IsType<ForbidResult>(await disbursements.Cancel(manual.ItemId, null));
        Assert.IsType<ForbidResult>(await disbursements.Create());
        Assert.IsType<ForbidResult>(await batches.Release("any"));
        Assert.IsType<ForbidResult>(await batches.Download("any"));
    }

    [Fact]
    public async Task Export_WritesOnlyTheDisbursementsOfTheSourcesThePersonMaySee()
    {
        // Arrange
        var (context, manual, _) = await SeedAsync();

        // Act
        var step = await RecipeRoundTrip.ExportAsync(ExportSource(context, new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = TestSecurity.User("lee") } }, new RecordingNotifier(), DisbursementsPermissions.ViewDisbursements), new DisbursementsDeploymentStep());

        // Assert
        var exported = step[DisbursementsRecipeStep.DisbursementsProperty]!.AsArray().Select(node => node!["itemId"]!.GetValue<string>()).ToList();
        Assert.Equal([manual.ItemId], exported);
        Assert.DoesNotContain("123456789", step.ToJsonString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Export_RunWithoutASignedInPerson_WritesOnlyTheDisbursementsNoSourceRestricts()
    {
        // Arrange
        var (context, manual, _) = await SeedAsync();

        // Act
        var step = await RecipeRoundTrip.ExportAsync(ExportSource(context, new HttpContextAccessor(), new RecordingNotifier()), new DisbursementsDeploymentStep());

        // Assert
        var exported = step[DisbursementsRecipeStep.DisbursementsProperty]!.AsArray().Select(node => node!["itemId"]!.GetValue<string>()).ToList();
        Assert.Equal([manual.ItemId], exported);
    }

    [Fact]
    public async Task Export_RunByAPersonWhoMayNotView_WritesNothing_AndSaysSo()
    {
        // Arrange
        var (context, _, _) = await SeedAsync();
        var notifier = new RecordingNotifier();
        var result = new DeploymentPlanResult(Mock.Of<IFileBuilder>(), new RecipeDescriptor());

        // Act
        await ExportSource(context, new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = TestSecurity.User("pat") } }, notifier).ProcessDeploymentStepAsync(new DisbursementsDeploymentStep(), result);

        // Assert
        Assert.Empty(result.Steps);
        Assert.Contains(notifier.Entries, entry => entry.Type == NotifyType.Warning);
    }

    [Fact]
    public async Task RecipeStep_ImportsHistory_ThatIsNeverBatched()
    {
        // Arrange
        var context = new DisbursementsTestContext();
        var step = new JsonObject
        {
            ["name"] = DisbursementsConstants.RecipeStepName,
            [DisbursementsRecipeStep.DisbursementsProperty] = new JsonArray(new JsonObject
            {
                ["idempotencyKey"] = "legacy-1",
                ["payeeName"] = "Pat Doe",
                ["amount"] = 120.5,
                ["currency"] = "USD",
                ["method"] = "Ach",
                ["status"] = "Settled",
            }),
        };

        // Act
        var errors = await RecipeRoundTrip.RunAsync(new DisbursementsRecipeStep(context.Manager), step);
        var again = await RecipeRoundTrip.RunAsync(new DisbursementsRecipeStep(context.Manager), step);
        var imported = Assert.Single(context.Store.Entries);
        var (batch, _) = await context.Service.CreateBatchAsync("Manual", [imported.ItemId], new DateOnly(2026, 11, 16), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(errors);
        Assert.Empty(again);
        Assert.True(imported.IsHistorical);
        Assert.Equal(DisbursementStatus.Settled, imported.Status);
        Assert.Null(batch);
    }

    // A manual payment (no source), and a pay statement deposit of employer group A, which Pat may see and Lee may not.
    private static async Task<(DisbursementsTestContext Context, Disbursement Manual, Disbursement GroupA)> SeedAsync()
    {
        var context = new DisbursementsTestContext();
        var origin = await context.OriginAsync();
        var payee = await context.PayeeAsync();
        var manual = (await context.Service.CreateAsync(new DisbursementRequest { PayeeName = "Kim", Amount = 5, Currency = "USD", Method = "Manual", IdempotencyKey = "m1" }, TestContext.Current.CancellationToken)).Disbursement;
        var groupA = await context.AchAsync(origin, payee, 100m, "statement-a", "groupA");

        return (context, manual, groupA);
    }

    private static SourceAccessService Access()
        => new([new FixedSourceAccess(PayStatement, new Dictionary<string, SourceAccessScope> { ["pat"] = SourceAccessScope.Of(["groupA"]) })]);

    private static DisbursementsDeploymentSource ExportSource(DisbursementsTestContext context, IHttpContextAccessor accessor, RecordingNotifier notifier, params Permission[] grants)
        => new(context.Store, Access(), accessor, TestSecurity.Granting(grants), notifier, new EchoHtmlLocalizer<DisbursementsDeploymentSource>());

    private static DisbursementsController DisbursementsController(DisbursementsTestContext context, string userName, Permission grant = null)
    {
        var updateModelAccessor = new Mock<IUpdateModelAccessor>();

        updateModelAccessor.SetupGet(accessor => accessor.ModelUpdater).Returns(Mock.Of<IUpdateModel>());

        var controller = new DisbursementsController(
            context.Manager,
            context.Store,
            context.Service,
            Access(),
            grant is null ? TestSecurity.Granting(DisbursementsPermissions.ManageDisbursements, DisbursementsPermissions.ViewDisbursements) : TestSecurity.Granting(grant),
            updateModelAccessor.Object,
            Mock.Of<IDisplayManager<Disbursement>>(),
            new RecordingNotifier(),
            context.Checks.LocalClock,
            new EchoHtmlLocalizer<DisbursementsController>(),
            RecipeRoundTrip.Localizer<DisbursementsController>());

        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = TestSecurity.User(userName) } };

        return controller;
    }

    private static DisbursementBatchesController BatchesController(DisbursementsTestContext context, string userName, Permission grant = null)
    {
        var features = new Mock<IShellFeaturesManager>();

        features.Setup(manager => manager.GetEnabledFeaturesAsync()).ReturnsAsync(Array.Empty<IFeatureInfo>());

        var controller = new DisbursementBatchesController(
            context.Batches,
            context.BatchStore,
            context.Service,
            context.Files,
            Access(),
            grant is null ? TestSecurity.Granting(DisbursementsPermissions.ManageDisbursements, DisbursementsPermissions.ViewDisbursements) : TestSecurity.Granting(grant),
            features.Object,
            new RecordingNotifier(),
            new global::CrestApps.OrchardCore.Banking.Disbursements.Services.DisbursementFileRecorder(new ServiceCollection().BuildServiceProvider(), Microsoft.Extensions.Logging.Abstractions.NullLogger<global::CrestApps.OrchardCore.Banking.Disbursements.Services.DisbursementFileRecorder>.Instance),
            new EchoHtmlLocalizer<DisbursementBatchesController>());

        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = TestSecurity.User(userName) } };

        return controller;
    }
}
