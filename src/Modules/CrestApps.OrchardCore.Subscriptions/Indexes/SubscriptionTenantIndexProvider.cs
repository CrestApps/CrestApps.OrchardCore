using System.Text.Json;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Indexes;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using Microsoft.Extensions.Options;
using OrchardCore.Json;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Subscriptions.Indexes;

public sealed class SubscriptionTenantIndexProvider : IndexProvider<SubscriptionSession>
{
    private readonly DocumentJsonSerializerOptions _documentJsonSerializerOptions;

    public SubscriptionTenantIndexProvider(IOptions<DocumentJsonSerializerOptions> documentJsonSerializerOptions)
    {
        _documentJsonSerializerOptions = documentJsonSerializerOptions.Value;
    }

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
            };
        });
    }
}
