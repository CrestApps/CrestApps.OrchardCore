using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using OrchardCore.ContentManagement.Metadata.Records;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.ContentTypes.Events;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Subscriptions.Services;

/// <summary>
/// Automatically injects the <see cref="SubscriptionPart"/> and the <see cref="ProductPart"/> into any content type
/// whose stereotype is set to <see cref="SubscriptionConstants.Stereotype"/>. Both parts are marked as
/// system-defined so they cannot be removed, which means an editor only needs to set the stereotype for the type to
/// participate in subscriptions. Because every plan is a product, plans are listed, filtered and shown by the
/// Product Listing feature like any other product.
/// </summary>
public sealed class SubscriptionPartContentTypeDefinitionHandler : IContentDefinitionHandler
{
    /// <summary>
    /// Adds the <see cref="SubscriptionPart"/> to the content type definition when the stereotype is set
    /// to <see cref="SubscriptionConstants.Stereotype"/>. This occurs while the content type definition is
    /// being built, so the part is present without any manual configuration.
    /// </summary>
    public void ContentTypeBuilding(ContentTypeBuildingContext context)
    {
        if (context?.Record?.Settings is null || !context.Record.Settings.TryGetPropertyValue(nameof(ContentTypeSettings), out var node))
        {
            return;
        }

        var settings = node.ToObject<ContentTypeSettings>();

        if (settings.Stereotype == null || !string.Equals(settings.Stereotype, SubscriptionConstants.Stereotype, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        AddSystemPart(context.Record, nameof(ProductPart));
        AddSystemPart(context.Record, nameof(SubscriptionPart));
    }

    private static void AddSystemPart(ContentTypeDefinitionRecord record, string partName)
    {
        if (record.ContentTypePartDefinitionRecords.Any(x => x.PartName.EqualsOrdinalIgnoreCase(partName)))
        {
            return;
        }

        record.ContentTypePartDefinitionRecords.Add(new ContentTypePartDefinitionRecord
        {
            Name = partName,
            PartName = partName,
            Settings = new JsonObject
            {
                [nameof(ContentSettings)] = JObject.FromObject(new ContentSettings
                {
                    IsSystemDefined = true,
                }),
            },
        });
    }

    /// <summary>
    /// Marks the part on the content type as system-defined to prevent its removal.
    /// </summary>
    public void ContentTypePartBuilding(ContentTypePartBuildingContext context)
    {
        if (context?.Record?.Settings is null || !context.Record.PartName.EqualsOrdinalIgnoreCase(nameof(SubscriptionPart)))
        {
            return;
        }

        var settings = context.Record.Settings[nameof(ContentSettings)]?.ToObject<ContentSettings>()
            ?? new ContentSettings();

        settings.IsSystemDefined = true;

        context.Record.Settings[nameof(ContentSettings)] = JObject.FromObject(settings);
    }

    /// <summary>
    /// Creates a definition when the record is missing and the part name is <see cref="SubscriptionPart"/>.
    /// </summary>
    public void ContentPartBuilding(ContentPartBuildingContext context)
    {
        if (context.Record is not null || context.PartName != nameof(SubscriptionPart))
        {
            return;
        }

        context.Record = new ContentPartDefinitionRecord
        {
            Name = context.PartName,
            Settings = new JsonObject
            {
                [nameof(ContentPartSettings)] = JObject.FromObject(new ContentPartSettings
                {
                    Attachable = false,
                    Reusable = false,
                }),
                [nameof(ContentSettings)] = JObject.FromObject(new ContentSettings
                {
                    IsSystemDefined = true,
                }),
            },
        };
    }

    public void ContentPartFieldBuilding(ContentPartFieldBuildingContext context)
    {
    }
}
