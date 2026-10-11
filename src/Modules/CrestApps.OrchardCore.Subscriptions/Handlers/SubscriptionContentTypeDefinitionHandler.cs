using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using OrchardCore.ContentManagement.Metadata.Records;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.ContentTypes.Events;

namespace CrestApps.OrchardCore.Subscriptions.Handlers;

/// <summary>
/// Applies the subscription stereotype to any content type that has the <see cref="SubscriptionPart"/>
/// attached, so it is recognized as a service plan (indexed, listed, and synced with Stripe) without
/// requiring the stereotype to be configured manually. An explicitly configured stereotype is respected.
/// </summary>
internal sealed class SubscriptionContentTypeDefinitionHandler : IContentDefinitionHandler
{
    public void ContentTypeBuilding(ContentTypeBuildingContext context)
    {
        if (context?.Record is null || !HasSubscriptionPart(context.Record))
        {
            return;
        }

        var settings = context.Record.Settings ??= new JsonObject();

        var typeSettings = settings[nameof(ContentTypeSettings)] as JsonObject;

        var stereotype = typeSettings?[nameof(ContentTypeSettings.Stereotype)]?.GetValue<string>();

        // Respect a stereotype that was explicitly configured for the type.
        if (!string.IsNullOrEmpty(stereotype))
        {
            return;
        }

        if (typeSettings is null)
        {
            typeSettings = new JsonObject();
            settings[nameof(ContentTypeSettings)] = typeSettings;
        }

        typeSettings[nameof(ContentTypeSettings.Stereotype)] = SubscriptionConstants.Stereotype;
    }

    public void ContentTypePartBuilding(ContentTypePartBuildingContext context)
    {
    }

    public void ContentPartBuilding(ContentPartBuildingContext context)
    {
    }

    public void ContentPartFieldBuilding(ContentPartFieldBuildingContext context)
    {
    }

    private static bool HasSubscriptionPart(ContentTypeDefinitionRecord record)
        => record.ContentTypePartDefinitionRecords.Any(part =>
            string.Equals(part.PartName, nameof(SubscriptionPart), StringComparison.Ordinal));
}
