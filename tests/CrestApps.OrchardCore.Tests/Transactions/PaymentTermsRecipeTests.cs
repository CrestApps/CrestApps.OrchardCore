using System.Text.Json.Nodes;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Transactions.Deployments;
using CrestApps.OrchardCore.Transactions.Handlers;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Recipes;
using CrestApps.OrchardCore.Transactions.Services;
using Moq;
using OrchardCore.Deployment;
using OrchardCore.Recipes.Models;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Transactions;

/// <summary>
/// A payment term is checked the same way from the admin, a recipe or code, so a recipe cannot store a term that would
/// give no due date.
/// </summary>
public sealed class PaymentTermsRecipeTests
{
    [Theory]
    [InlineData("Net 30", PaymentTermKind.NetDays, 30, 0, null, null, true)]
    [InlineData("", PaymentTermKind.NetDays, 30, 0, null, null, false)]
    [InlineData("Bad days", PaymentTermKind.NetDays, -1, 0, null, null, false)]
    [InlineData("Bad day", PaymentTermKind.DayOfNextMonth, 0, 32, null, null, false)]
    [InlineData("10th", PaymentTermKind.DayOfNextMonth, 0, 10, null, null, true)]
    [InlineData("2/10 net 30", PaymentTermKind.NetDays, 30, 0, 2, 10, true)]
    [InlineData("Discount without days", PaymentTermKind.NetDays, 30, 0, 2, null, false)]
    [InlineData("Too much", PaymentTermKind.NetDays, 30, 0, 100, 10, false)]
    public async Task Validating_ChecksTheTerm(string name, PaymentTermKind kind, int days, int dayOfMonth, int? percent, int? discountDays, bool valid)
    {
        // Arrange
        var handler = new PaymentTermHandler(new TestClock(new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc)), new PassThroughStringLocalizer<PaymentTermHandler>());
        var term = new PaymentTerm
        {
            Name = name,
            Kind = kind,
            Days = days,
            DayOfMonth = dayOfMonth,
            DiscountPercent = percent,
            DiscountDays = discountDays,
        };

        var context = new ValidatingContext<PaymentTerm>(term);

        // Act
        await handler.ValidatingAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(valid, context.Result.Succeeded);
    }

    [Fact]
    public async Task RecipeStep_ImportsTheTerms_AndTheExportReplaysWithoutDuplicates()
    {
        // Arrange
        var store = new List<PaymentTerm>();
        var manager = Backed(store);
        var step = new PaymentTermsStep(manager.Object);
        var recipe = new JsonObject
        {
            ["Terms"] = new JsonArray(
                new JsonObject { ["ItemId"] = "4paymentterm00000000000001", ["Name"] = "Net 30", ["Kind"] = "NetDays", ["Days"] = 30 },
                new JsonObject { ["ItemId"] = "4paymentterm00000000000002", ["Name"] = "2/10 net 30", ["Kind"] = 1, ["Days"] = 30, ["DiscountPercent"] = 2, ["DiscountDays"] = 10 }),
        };

        // Act
        var first = await RunAsync(step, recipe);
        var exported = await ExportAsync(new PaymentTermsDeploymentSource(manager.Object));
        var second = await RunAsync(step, exported);

        // Assert
        Assert.Empty(first.Errors);
        Assert.Empty(second.Errors);
        Assert.Equal(2, store.Count);
        Assert.Equal(PaymentTermKind.NetDays, store[0].Kind);
        Assert.Equal(30, store[0].Days);
        Assert.Equal(2m, store[1].DiscountPercent);
        Assert.Equal(PaymentTermsDeploymentStep.StepName, exported["name"].GetValue<string>());
        Assert.Equal(["4paymentterm00000000000001", "4paymentterm00000000000002"], exported["Terms"].AsArray().Select(node => node["ItemId"].GetValue<string>()));
        manager.Verify(value => value.CreateAsync(It.IsAny<PaymentTerm>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    private static Mock<IPaymentTermManager> Backed(List<PaymentTerm> store)
    {
        var manager = new Mock<IPaymentTermManager>();
        manager.Setup(value => value.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, CancellationToken _) => store.FirstOrDefault(entry => entry.ItemId == id));
        manager.Setup(value => value.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => store.ToArray());
        manager.Setup(value => value.NewAsync(It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((JsonNode data, CancellationToken _) =>
            {
                var entry = new PaymentTerm { ItemId = Guid.NewGuid().ToString("n") };
                CatalogDeploymentSerializer.Populate(entry, data);

                return entry;
            });
        manager.Setup(value => value.CreateAsync(It.IsAny<PaymentTerm>(), It.IsAny<CancellationToken>()))
            .Callback((PaymentTerm entry, CancellationToken _) => store.Add(entry))
            .Returns(ValueTask.CompletedTask);
        manager.Setup(value => value.UpdateAsync(It.IsAny<PaymentTerm>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .Callback((PaymentTerm entry, JsonNode data, CancellationToken _) => CatalogDeploymentSerializer.Populate(entry, data))
            .Returns(ValueTask.CompletedTask);
        manager.Setup(value => value.ValidateAsync(It.IsAny<PaymentTerm>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResultDetails());

        return manager;
    }

    private static async Task<RecipeExecutionContext> RunAsync(PaymentTermsStep step, JsonObject data)
    {
        var context = new RecipeExecutionContext
        {
            Name = PaymentTermsDeploymentStep.StepName,
            Step = data,
        };

        await step.ExecuteAsync(context);

        return context;
    }

    private static async Task<JsonObject> ExportAsync(PaymentTermsDeploymentSource source)
    {
        var result = new DeploymentPlanResult(Mock.Of<IFileBuilder>(), new RecipeDescriptor());

        await source.ProcessDeploymentStepAsync(new PaymentTermsDeploymentStep(), result);

        return Assert.Single(result.Steps);
    }
}
