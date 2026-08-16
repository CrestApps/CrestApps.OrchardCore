using System.Text.Json;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Indexes;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using Microsoft.Extensions.Options;
using OrchardCore.Json;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Subscriptions.Indexes;

/// <summary>
/// Maps tenant onboarding step data from subscription sessions to tenant lookup index rows.
/// </summary>
public sealed class SubscriptionTenantIndexProvider : IndexProvider<SubscriptionSession>
{
    private readonly DocumentJsonSerializerOptions _documentJsonSerializerOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionTenantIndexProvider"/> class.
    /// </summary>
    /// <param name="documentJsonSerializerOptions">The document JSON serializer options used to deserialize tenant onboarding data.</param>
    public SubscriptionTenantIndexProvider(IOptions<DocumentJsonSerializerOptions> documentJsonSerializerOptions)
    {
        _documentJsonSerializerOptions = documentJsonSerializerOptions.Value;
    }

    /// <summary>
    /// Describes how tenant onboarding step data is projected into <see cref="SubscriptionTenantIndex"/> rows.
    /// </summary>
    /// <param name="context">The YesSql describe context for subscription sessions.</param>
    public override void Describe(DescribeContext<SubscriptionSession> context)
    {
        context.For<SubscriptionTenantIndex>()
        .Map(session =>
        {
            if (!session.SavedSteps.TryGetPropertyValue(SubscriptionConstants.StepKey.TenantOnboarding, out var node))
            {
                return null;
            }

            var info = node.Deserialize<TenantOnboardingStep>(_documentJsonSerializerOptions.SerializerOptions);

            return new SubscriptionTenantIndex()
            {
                SessionId = session.SessionId,
                TenantName = info.TenantName,
                Recipe = info.RecipeName,
            };
        });
    }
}
