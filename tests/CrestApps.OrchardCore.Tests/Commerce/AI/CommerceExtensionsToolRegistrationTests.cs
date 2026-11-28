using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.AgenticCommerce;
using CrestApps.OrchardCore.AgenticCommerce.AI;
using CrestApps.OrchardCore.AgenticCommerce.Feeds;
using CrestApps.OrchardCore.AI.Records;
using CrestApps.OrchardCore.Bookings;
using CrestApps.OrchardCore.Bookings.AI;
using CrestApps.OrchardCore.Classifieds;
using CrestApps.OrchardCore.Classifieds.AI;
using CrestApps.OrchardCore.Downloads;
using CrestApps.OrchardCore.Downloads.AI;
using CrestApps.OrchardCore.Fundraising;
using CrestApps.OrchardCore.Fundraising.AI;
using CrestApps.OrchardCore.Marketing;
using CrestApps.OrchardCore.Marketing.AI;
using CrestApps.OrchardCore.Marketplace;
using CrestApps.OrchardCore.Marketplace.AI;
using CrestApps.OrchardCore.Memberships;
using CrestApps.OrchardCore.Memberships.AI;
using CrestApps.OrchardCore.Reviews;
using CrestApps.OrchardCore.Reviews.AI;
using CrestApps.OrchardCore.Subscriptions;
using CrestApps.OrchardCore.Subscriptions.AI;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Tests.Agent.AIModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;
using BookingsAIToolsStartup = CrestApps.OrchardCore.Bookings.AIToolsStartup;
using ClassifiedsAIToolsStartup = CrestApps.OrchardCore.Classifieds.AIToolsStartup;
using DownloadsAIToolsStartup = CrestApps.OrchardCore.Downloads.AIToolsStartup;
using FundraisingAIToolsStartup = CrestApps.OrchardCore.Fundraising.AIToolsStartup;
using MarketingAIToolsStartup = CrestApps.OrchardCore.Marketing.AIToolsStartup;
using MarketplaceAIToolsStartup = CrestApps.OrchardCore.Marketplace.AIToolsStartup;
using MembershipsAIToolsStartup = CrestApps.OrchardCore.Memberships.AIToolsStartup;
using ReviewsAIToolsStartup = CrestApps.OrchardCore.Reviews.AIToolsStartup;
using SubscriptionsAIToolsStartup = CrestApps.OrchardCore.Subscriptions.AIToolsStartup;

namespace CrestApps.OrchardCore.Tests.Commerce.AI;

/// <summary>
/// The commerce extension tools (subscriptions, sites, installment plans, memberships, marketplace, classifieds,
/// fundraising, downloads, reviews, marketing, bookings and the agentic commerce admin tools) are registered with a
/// title, a description and a category, can be picked on a profile, ask for approval only where the action cannot be
/// undone or leaves the site, and are registered by the startup of the feature they belong to, which requires the AI
/// feature. Their record kinds are registered with the agreed write access.
/// </summary>
public sealed class CommerceExtensionsToolRegistrationTests
{
    private static readonly Type[] _startups =
    [
        typeof(SubscriptionsAIToolsStartup),
        typeof(TenantProvisioningAIToolsStartup),
        typeof(InstallmentPlansAIToolsStartup),
        typeof(MembershipsAIToolsStartup),
        typeof(MarketplaceAIToolsStartup),
        typeof(ClassifiedsAIToolsStartup),
        typeof(FundraisingAIToolsStartup),
        typeof(DownloadsAIToolsStartup),
        typeof(ReviewsAIToolsStartup),
        typeof(MarketingAIToolsStartup),
        typeof(BookingsAIToolsStartup),
        typeof(AdminAIToolsStartup),
        typeof(FeedsAIToolsStartup),
    ];

    public static TheoryData<string, bool> Tools => new()
    {
        { ListSubscriptionsTool.TheName, false },
        { GetSubscriptionTool.TheName, false },
        { CancelSubscriptionTool.TheName, true },
        { SetSubscriptionPausedTool.TheName, true },
        { ListSiteProvisioningJobsTool.TheName, false },
        { ResolveSiteProvisioningJobTool.TheName, false },
        { ListInstallmentPlansTool.TheName, false },
        { GetInstallmentPlanTool.TheName, false },
        { CreateInstallmentPlanTool.TheName, false },
        { ChargeInstallmentPaymentTool.TheName, true },
        { CancelInstallmentPlanTool.TheName, true },
        { ListMembershipsTool.TheName, false },
        { EndMembershipTool.TheName, true },
        { GetVendorTool.TheName, false },
        { ListClassifiedsTool.TheName, false },
        { ModerateClassifiedTool.TheName, true },
        { GetFundraisingCampaignProgressTool.TheName, false },
        { ListOrderDownloadsTool.TheName, false },
        { SetOrderDownloadAccessTool.TheName, false },
        { ListProductReviewsTool.TheName, false },
        { ModerateProductReviewTool.TheName, false },
        { DeleteProductReviewTool.TheName, true },
        { GetMarketingSegmentTool.TheName, false },
        { SendMarketingCampaignTestTool.TheName, true },
        { SendMarketingCampaignTool.TheName, true },
        { CancelMarketingCampaignTool.TheName, false },
        { ListBookingsTool.TheName, false },
        { GetBookingTool.TheName, false },
        { CompleteBookingTool.TheName, false },
        { CancelBookingTool.TheName, true },
        { ChargeBookingBalanceTool.TheName, true },
        { ListBookingDatesTool.TheName, false },
        { SaveBookingDateTool.TheName, false },
        { ListAgentCheckoutsTool.TheName, false },
        { GetAgentFeedStatusTool.TheName, false },
        { PushAgentFeedsTool.TheName, true },
    };

    [Theory]
    [MemberData(nameof(Tools))]
    public void Tool_IsRegisteredWithItsTextsAndApproval(string toolName, bool requiresApproval)
    {
        // Arrange
        var (tools, _) = Register(_startups);

        // Act
        var found = tools.TryGetValue(toolName, out var tool);

        // Assert
        Assert.True(found, $"The tool '{toolName}' is not registered.");
        Assert.Equal(toolName, tool.Name);
        Assert.False(string.IsNullOrWhiteSpace(tool.Title));
        Assert.NotEqual(toolName, tool.Title);
        Assert.False(string.IsNullOrWhiteSpace(tool.Description));
        Assert.False(string.IsNullOrWhiteSpace(tool.Category));
        Assert.True(tool.IsSelectable());
        Assert.Equal(requiresApproval, tool.RequiresApproval);
    }

    [Fact]
    public void TheArea_RegistersThirtySixTools()
    {
        // Act
        var (tools, _) = Register(_startups);

        // Assert
        Assert.Equal(36, tools.Count);
    }

    [Theory]
    [InlineData("vendor", false)]
    [InlineData("marketingSegment", false)]
    [InlineData("marketingCampaign", false)]
    [InlineData("agentPlatform", true)]
    public void RecordKind_IsRegisteredWithItsWriteAccess(string kindName, bool canWrite)
    {
        // Act
        var (_, kinds) = Register(_startups);

        // Assert
        Assert.True(kinds.TryGetValue(kindName, out var kind), $"The record kind '{kindName}' is not registered.");
        Assert.Equal(canWrite, kind.CanWrite);
        Assert.NotNull(kind.Permission);
        Assert.False(string.IsNullOrWhiteSpace(kind.Description));
    }

    [Theory]
    [InlineData(typeof(SubscriptionsAIToolsStartup), 4, SubscriptionConstants.Features.Area)]
    [InlineData(typeof(TenantProvisioningAIToolsStartup), 2, SubscriptionConstants.Features.Tenants)]
    [InlineData(typeof(InstallmentPlansAIToolsStartup), 5, SubscriptionConstants.Features.Installments)]
    [InlineData(typeof(MembershipsAIToolsStartup), 2, MembershipsConstants.Feature)]
    [InlineData(typeof(MarketplaceAIToolsStartup), 1, MarketplaceConstants.Feature)]
    [InlineData(typeof(ClassifiedsAIToolsStartup), 2, ClassifiedsConstants.Feature)]
    [InlineData(typeof(FundraisingAIToolsStartup), 1, FundraisingConstants.Feature)]
    [InlineData(typeof(DownloadsAIToolsStartup), 2, DownloadsConstants.Feature)]
    [InlineData(typeof(ReviewsAIToolsStartup), 3, ReviewsConstants.Feature)]
    [InlineData(typeof(MarketingAIToolsStartup), 4, MarketingConstants.Feature)]
    [InlineData(typeof(BookingsAIToolsStartup), 7, BookingsConstants.Feature)]
    [InlineData(typeof(AdminAIToolsStartup), 1, AgenticCommerceConstants.Features.AgenticCommerce)]
    [InlineData(typeof(FeedsAIToolsStartup), 2, AgenticCommerceConstants.Features.Feeds)]
    public void EachStartup_RegistersItsTools_ForItsFeature_WhenTheAIFeatureIsOn(Type startupType, int expectedCount, string featureId)
    {
        // Act
        var (tools, _) = Register([startupType]);
        var required = startupType.GetCustomAttributes(typeof(RequireFeaturesAttribute), inherit: false)
            .Cast<RequireFeaturesAttribute>()
            .Single();
        var feature = startupType.GetCustomAttributes(typeof(FeatureAttribute), inherit: false)
            .Cast<FeatureAttribute>()
            .Single();

        // Assert
        Assert.Equal(expectedCount, tools.Count);
        Assert.Equal(AIFeatureIds.Area, Assert.Single(required.RequiredFeatureNames));
        Assert.Equal(featureId, feature.FeatureName);
    }

    private static (IReadOnlyDictionary<string, AIToolDefinitionEntry> Tools, IReadOnlyDictionary<string, AIRecordKind> Kinds) Register(IEnumerable<Type> startupTypes)
    {
        var services = new ServiceCollection();

        foreach (var type in startupTypes)
        {
            var constructor = type.GetConstructors().Single();
            var arguments = constructor.GetParameters()
                .Select(parameter => Activator.CreateInstance(typeof(PassThroughStringLocalizer<>).MakeGenericType(parameter.ParameterType.GetGenericArguments()[0])))
                .ToArray();

            ((StartupBase)constructor.Invoke(arguments)).ConfigureServices(services);
        }

        using var provider = services.BuildServiceProvider();

        return (
            provider.GetRequiredService<IOptions<AIToolDefinitionOptions>>().Value.Tools,
            provider.GetRequiredService<IOptions<AIRecordKindOptions>>().Value.Kinds);
    }
}
