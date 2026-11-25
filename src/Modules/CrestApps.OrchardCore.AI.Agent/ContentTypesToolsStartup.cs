using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Agent.ContentTypes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Registers the content definition tools that attach, configure and remove single parts and fields, and the tool that
/// lists the template names (shape alternates) of a content type, when the Content Types feature is on.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.ContentTypes)]
public sealed class ContentTypesToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentTypesToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ContentTypesToolsStartup(IStringLocalizer<ContentTypesToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<SaveContentTypePartTool>(SaveContentTypePartTool.TheName)
            .WithTitle(S["Save Content Type Part"])
            .WithDescription(S["Attaches a part to a content type or updates how it is attached: display name and description (named parts), editor, display mode and the part's settings for this type. Use it to add or reconfigure a single part without resending the whole type definition."])
            .WithCategory(S["Content Definitions"])
            .Selectable();

        services.AddCoreAITool<RemoveContentTypePartTool>(RemoveContentTypePartTool.TheName)
            .WithTitle(S["Remove Content Type Part"])
            .WithDescription(S["Removes a part from a content type. Existing items keep the stored data but it is no longer edited or displayed; a part that owns the type's fields cannot be re-created with the same data automatically."])
            .WithCategory(S["Content Definitions"])
            .AsDestructive()
            .Selectable();

        services.AddCoreAITool<SaveContentPartFieldTool>(SaveContentPartFieldTool.TheName)
            .WithTitle(S["Save Content Part Field"])
            .WithDescription(S["Adds a field to a content part (or to a content type's own part) or updates an existing field: display name, field type on creation, editor, display mode and the field's settings. The field type cannot be changed after creation."])
            .WithCategory(S["Content Definitions"])
            .Selectable();

        services.AddCoreAITool<RemoveContentPartFieldTool>(RemoveContentPartFieldTool.TheName)
            .WithTitle(S["Remove Content Part Field"])
            .WithDescription(S["Removes a field from a content part or content type. Existing items keep the stored values but they are no longer edited or displayed."])
            .WithCategory(S["Content Definitions"])
            .AsDestructive()
            .Selectable();

        services.AddCoreAITool<ListShapeAlternatesTool>(ListShapeAlternatesTool.TheName)
            .WithTitle(S["List Shape Alternates"])
            .WithDescription(S["Lists the template names Orchard Core looks for when it shows items of a content type in a display type (Detail or Summary): the content item shape and the shapes of each part and field, each with its shape name (for the Templates admin page) and its theme file name. Use it before naming a template, so the name is one Orchard Core actually uses; the last matching name with a template wins."])
            .WithCategory(S["Content Definitions"])
            .Selectable();
    }
}
