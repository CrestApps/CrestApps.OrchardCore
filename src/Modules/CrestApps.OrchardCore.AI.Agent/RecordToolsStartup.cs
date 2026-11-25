using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Agent.RecordTools;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Registers the generic record tools, which list, read, save and delete the records of every record kind an enabled
/// module registered with <c>AddAIRecordKind</c>. Each kind brings its own permission, its allow-listed fields and its
/// admin editor, which the writes replay.
/// </summary>
[RequireFeatures(AIConstants.Feature.Area)]
public sealed class RecordToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecordToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public RecordToolsStartup(IStringLocalizer<RecordToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<AIRecordScopeChanges>();

        services.AddCoreAITool<ListRecordsTool>(ListRecordsTool.TheName)
            .WithTitle(S["List Records"])
            .WithDescription(S["Lists the records of one kind (a setup catalog such as tax categories), a page at a time, optionally filtered by a text in their names. Returns each record's id and main fields."])
            .WithCategory(S["Records"])
            .Selectable();

        services.AddCoreAITool<GetRecordTool>(GetRecordTool.TheName)
            .WithTitle(S["Get Record"])
            .WithDescription(S["Gets one record of a kind by its id, with its main fields."])
            .WithCategory(S["Records"])
            .Selectable();

        services.AddCoreAITool<SaveRecordTool>(SaveRecordTool.TheName)
            .WithTitle(S["Save Record"])
            .WithDescription(S["Creates a record of a kind, or changes one when an id is given, through the same editor and rules as its admin page. Returns the saved record, or the errors and the field schema. With dryRun true it only checks the values."])
            .WithCategory(S["Records"])
            .Selectable();

        services.AddCoreAITool<DeleteRecordTool>(DeleteRecordTool.TheName)
            .WithTitle(S["Delete Record"])
            .WithDescription(S["Deletes records of a kind by their ids. A record that is still in use is not deleted. This cannot be undone."])
            .WithCategory(S["Records"])
            .Selectable()
            .AsDestructive();
    }
}
