using CrestApps.Core.AI;
using CrestApps.OrchardCore.DncRegistry.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.DncRegistry;

/// <summary>
/// Registers the local do-not-call list tools whenever the AI feature is on. The lists decide who may be contacted, so
/// every tool that adds numbers to screening, stops it or removes a list asks for approval. Every tool checks the
/// caller's permission itself.
/// </summary>
[RequireFeatures(AIFeatureIds.Area)]
[Feature(DncRegistryConstants.Features.Local)]
public sealed class AIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIToolsStartup(IStringLocalizer<AIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListLocalDncListsTool>(ListLocalDncListsTool.TheName)
            .WithTitle(S["List Local Do-Not-Call Lists"])
            .WithDescription(S["Lists the local do-not-call lists with country, status, number count, processed and imported totals and any import error."])
            .WithCategory(S["Telephony"])
            .Selectable();

        services.AddCoreAITool<ImportLocalDncListTool>(ImportLocalDncListTool.TheName)
            .WithTitle(S["Import Local Do-Not-Call List"])
            .WithDescription(S["Creates a local do-not-call list for a country from phone numbers given inline or from a CSV file in the media library, and queues it for background import."])
            .WithCategory(S["Telephony"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<SetLocalDncListImportStateTool>(SetLocalDncListImportStateTool.TheName)
            .WithTitle(S["Set Local Do-Not-Call List Import State"])
            .WithDescription(S["Starts or resumes processing of a queued or failed list now, or pauses a running import."])
            .WithCategory(S["Telephony"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<DeleteLocalDncListTool>(DeleteLocalDncListTool.TheName)
            .WithTitle(S["Delete Local Do-Not-Call List"])
            .WithDescription(S["Deletes a local do-not-call list and all its numbers in the background. Those numbers can be dialed again afterwards, so this weakens compliance screening and cannot be undone."])
            .WithCategory(S["Telephony"])
            .RequireApproval()
            .Selectable();
    }
}
