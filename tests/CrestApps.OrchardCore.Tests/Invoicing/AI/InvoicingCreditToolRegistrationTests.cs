using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.Invoicing;
using CrestApps.OrchardCore.Invoicing.AI;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Invoicing.AI;

/// <summary>
/// The credit note, estimate, recurring plan and billing AI tools are registered by the startup of the feature they belong
/// to, which requires the AI feature, with a title, a description and a category, can be picked on a profile, and ask for
/// approval only where the action moves money or credit or leaves the site: issuing a credit note, sending an estimate,
/// and pausing or resuming a recurring plan, whose next run may issue, e-mail or charge with nobody asked again.
/// </summary>
public sealed class InvoicingCreditToolRegistrationTests
{
    public static TheoryData<string, bool> Tools => new()
    {
        { PreviewCreditNoteTool.TheName, false },
        { IssueCreditNoteTool.TheName, true },
        { DraftEstimateTool.TheName, false },
        { SendEstimateTool.TheName, true },
        { ConvertEstimateToInvoiceTool.TheName, false },
        { PauseOrResumeRecurringPlanTool.TheName, true },
    };

    [Theory]
    [MemberData(nameof(Tools))]
    public void Tool_IsRegisteredWithItsTextsAndApproval(string toolName, bool requiresApproval)
    {
        // Act
        var tools = Register(typeof(CreditNoteAIToolsStartup), typeof(EstimateAIToolsStartup), typeof(RecurringPlanAIToolsStartup), typeof(BillingAIToolsStartup));

        // Assert
        Assert.True(tools.TryGetValue(toolName, out var tool), $"The tool '{toolName}' is not registered.");
        Assert.Equal(toolName, tool.Name);
        Assert.False(string.IsNullOrWhiteSpace(tool.Title));
        Assert.NotEqual(toolName, tool.Title);
        Assert.False(string.IsNullOrWhiteSpace(tool.Description));
        Assert.Equal("Invoicing", tool.Category);
        Assert.True(tool.IsSelectable());
        Assert.Equal(requiresApproval, tool.RequiresApproval);
    }

    [Theory]
    [InlineData(typeof(CreditNoteAIToolsStartup), 2, InvoicingConstants.Features.Area)]
    [InlineData(typeof(EstimateAIToolsStartup), 3, InvoicingConstants.Features.Estimates)]
    [InlineData(typeof(RecurringPlanAIToolsStartup), 1, InvoicingConstants.Features.Recurring)]
    [InlineData(typeof(BillingAIToolsStartup), 0, InvoicingConstants.Features.Billing)]
    public void EachStartup_RegistersItsTools_ForItsFeature_WhenTheAIFeatureIsOn(Type startupType, int expectedCount, string featureId)
    {
        // Act
        var tools = Register(startupType);
        var required = startupType.GetCustomAttributes(typeof(RequireFeaturesAttribute), inherit: false).Cast<RequireFeaturesAttribute>().Single();
        var feature = startupType.GetCustomAttributes(typeof(FeatureAttribute), inherit: false).Cast<FeatureAttribute>().Single();

        // Assert
        Assert.Equal(expectedCount, tools.Count);
        Assert.Equal(AIFeatureIds.Area, Assert.Single(required.RequiredFeatureNames));
        Assert.Equal(featureId, feature.FeatureName);
    }

    private static IReadOnlyDictionary<string, AIToolDefinitionEntry> Register(params Type[] startupTypes)
    {
        var services = new ServiceCollection();

        foreach (var type in startupTypes)
        {
            var constructor = type.GetConstructors().Single();
            var localizer = Activator.CreateInstance(typeof(PassThroughStringLocalizer<>).MakeGenericType(type));

            ((StartupBase)constructor.Invoke([localizer])).ConfigureServices(services);
        }

        using var provider = services.BuildServiceProvider();

        return provider.GetRequiredService<IOptions<AIToolDefinitionOptions>>().Value.Tools;
    }
}
