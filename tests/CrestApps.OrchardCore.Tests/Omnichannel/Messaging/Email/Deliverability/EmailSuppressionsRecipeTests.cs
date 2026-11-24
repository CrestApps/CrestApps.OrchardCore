using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Deployments;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Recipes;
using OrchardCore.Deployment;
using OrchardCore.Recipes.Models;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging.Email.Deliverability;

/// <summary>
/// The suppression list travels between sites, so another site stops sending to the addresses that bounced or
/// complained here.
/// </summary>
public sealed class EmailSuppressionsRecipeTests
{
    [Fact]
    public async Task Deployment_ExportsEachAddressWithItsReasonAndDetail_AndTheStepImportsThem()
    {
        // Arrange
        var source = new InMemoryEmailSuppressionList();
        await source.SuppressAsync("gone@example.com", EmailSuppressionReason.HardBounce, "5.1.1 User unknown", cancellationToken: TestContext.Current.CancellationToken);
        await source.SuppressAsync("angry@example.com", EmailSuppressionReason.Complaint, null, cancellationToken: TestContext.Current.CancellationToken);

        var result = new DeploymentPlanResult(null, new RecipeDescriptor());

        // Act
        await new EmailSuppressionsDeploymentSource(source).ProcessDeploymentStepAsync(new EmailSuppressionsDeploymentStep(), result);

        var target = new InMemoryEmailSuppressionList();
        var step = Assert.Single(result.Steps);
        var context = new RecipeExecutionContext { Name = EmailSuppressionsStep.StepKey, Step = step };

        await new EmailSuppressionsStep(target).ExecuteAsync(context);

        // Assert
        Assert.Equal(EmailSuppressionsStep.StepKey, step["name"]!.GetValue<string>());
        Assert.Equal(2, step[EmailSuppressionsStep.EntriesName]!.AsArray().Count);
        Assert.Empty(context.Errors);
        Assert.Equal(EmailSuppressionReason.HardBounce, target.Items["gone@example.com"].Reason);
        Assert.Equal("5.1.1 User unknown", target.Items["gone@example.com"].Detail);
        Assert.Equal(EmailSuppressionReason.Complaint, target.Items["angry@example.com"].Reason);
    }

    [Fact]
    public async Task Step_AddsEachAddressOnce_DefaultsToManual_AndReportsWhatItCannotRead()
    {
        // Arrange
        var list = new InMemoryEmailSuppressionList();
        await list.SuppressAsync("kept@example.com", EmailSuppressionReason.HardBounce, "first", cancellationToken: TestContext.Current.CancellationToken);

        var step = new JsonObject
        {
            ["name"] = EmailSuppressionsStep.StepKey,
            [EmailSuppressionsStep.EntriesName] = new JsonArray
            {
                new JsonObject { ["Address"] = "New@Example.com" },
                new JsonObject { ["Address"] = "new@example.com", ["Reason"] = "Complaint" },
                new JsonObject { ["Address"] = "kept@example.com", ["Reason"] = "Complaint" },
                new JsonObject { ["Address"] = "numbered@example.com", ["Reason"] = 1 },
                new JsonObject { ["Address"] = "not-an-address" },
                new JsonObject { ["Address"] = "odd@example.com", ["Reason"] = "Unhappy" },
            },
        };

        var context = new RecipeExecutionContext { Name = EmailSuppressionsStep.StepKey, Step = step };

        // Act
        await new EmailSuppressionsStep(list).ExecuteAsync(context);

        // Assert
        Assert.Equal(EmailSuppressionReason.Manual, list.Items["new@example.com"].Reason);
        Assert.Equal(EmailSuppressionReason.HardBounce, list.Items["kept@example.com"].Reason);
        Assert.Equal(EmailSuppressionReason.RepeatedSoftBounces, list.Items["numbered@example.com"].Reason);
        Assert.False(list.Items.ContainsKey("odd@example.com"));
        Assert.Equal(2, context.Errors.Count);
    }
}
