using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using OrchardCore.ContentManagement.Metadata.Records;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.ContentTypes.Events;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Subscriptions.Services;

/// <summary>
/// Automatically injects the <see cref="SubscriptionPart"/> into any content type whose stereotype is
/// set to <see cref="SubscriptionConstants.Stereotype"/>. The part is marked as system-defined so it
/// cannot be removed, which means an editor only needs to set the stereotype (and add their own product
/// part) for the type to participate in subscriptions.
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

        if (context.Record.ContentTypePartDefinitionRecords.Any(x => x.Name.EqualsOrdinalIgnoreCase(nameof(SubscriptionPart))))
        {
            return;
        }

        context.Record.ContentTypePartDefinitionRecords.Add(new ContentTypePartDefinitionRecord
        {
            Name = nameof(SubscriptionPart),
            PartName = nameof(SubscriptionPart),
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
