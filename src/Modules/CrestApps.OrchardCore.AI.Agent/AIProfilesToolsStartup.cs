using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Agent.Profiles;
using CrestApps.OrchardCore.AI.Agent.ProfileTemplates;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Registers the tools that list, create, change, version, test and delete AI profiles and agents, and the tools for
/// profile templates. Every tool that changes something refuses to change the assistant that runs it, and checks the
/// tools it gives a profile that visitors can reach.
/// </summary>
[RequireFeatures(AIConstants.Feature.Area)]
public sealed class AIProfilesToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfilesToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIProfilesToolsStartup(IStringLocalizer<AIProfilesToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddScoped<IPublishedChatWidgetFinder, PublishedChatWidgetFinder>();

        services.AddCoreAITool<ListAIToolsTool>(ListAIToolsTool.TheName)
            .WithTitle(S["List AI Tools"])
            .WithDescription(S["Lists what can be attached to a profile: registered tools by category (with whether they ask for approval and whether they are safe for visitors), tool instances, and agent profiles. Only items the current user may attach are returned."])
            .WithCategory(S["AI Profiles"])
            .Selectable();

        services.AddCoreAITool<SaveAIProfileTool>(SaveAIProfileTool.TheName)
            .WithTitle(S["Save AI Profile"])
            .WithDescription(S["Creates a profile or agent, or updates one by id: title, description, type, instructions, welcome message, deployments, model parameters, persona and prompt templates. It does not change tools, agents or connections (use setAIProfileCapabilities) and cannot change the assistant that is running or its agents."])
            .WithCategory(S["AI Profiles"])
            .Selectable();

        services.AddCoreAITool<SetAIProfileCapabilitiesTool>(SetAIProfileCapabilitiesTool.TheName)
            .WithTitle(S["Set AI Profile Capabilities"])
            .WithDescription(S["Sets which tools, tool instances and agents a profile can use, and for an agent whether it may run its tools and whether it is always available. Refused for the running assistant and for tools that are unsafe on a profile visitors can reach. MCP and A2A connections are set on the profile's page."])
            .WithCategory(S["AI Profiles"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<CreateAIProfileFromTemplateTool>(CreateAIProfileFromTemplateTool.TheName)
            .WithTitle(S["Create AI Profile From Template"])
            .WithDescription(S["Creates a profile from a profile template (the New Profile wizard): give the template id, a title, a technical name and optionally a chat deployment. Fails with the missing feature names when the template's scenario is not available."])
            .WithCategory(S["AI Profiles"])
            .Selectable();

        services.AddCoreAITool<DeleteAIProfileTool>(DeleteAIProfileTool.TheName)
            .WithTitle(S["Delete AI Profiles"])
            .WithDescription(S["Deletes one or more profiles by id. Profiles marked not removable, the running assistant and its agents are skipped and reported."])
            .WithCategory(S["AI Profiles"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<ListAIProfileVersionsTool>(ListAIProfileVersionsTool.TheName)
            .WithTitle(S["List AI Profile Versions"])
            .WithDescription(S["Lists a profile's saved versions (number, date, author, note). With versionId it also returns the field-by-field differences against the current profile or another version."])
            .WithCategory(S["AI Profiles"])
            .Selectable();

        services.AddCoreAITool<RestoreAIProfileVersionTool>(RestoreAIProfileVersionTool.TheName)
            .WithTitle(S["Restore AI Profile Version"])
            .WithDescription(S["Restores a profile to a saved version; the restore is saved as a new version, so it can be undone. It can bring back earlier tools and instructions, so the same refusals as setAIProfileCapabilities apply."])
            .WithCategory(S["AI Profiles"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<TestAIProfileTool>(TestAIProfileTool.TheName)
            .WithTitle(S["Test AI Profile"])
            .WithDescription(S["Sends one message to a profile and returns its reply, with the profile's tools turned off. Use it to check an edit. It makes a paid model call."])
            .WithCategory(S["AI Profiles"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<ListAIProfileTemplatesTool>(ListAIProfileTemplatesTool.TheName)
            .WithTitle(S["List AI Profile Templates"])
            .WithDescription(S["Lists templates with source (Profile or SystemPrompt), category, title, whether they are shipped with a module (read-only) and whether their scenario is available. Filter by source, category (for example speaking styles or guardrail topics) or name."])
            .WithCategory(S["AI Profile Templates"])
            .Selectable();

        services.AddCoreAITool<GetAIProfileTemplateTool>(GetAIProfileTemplateTool.TheName)
            .WithTitle(S["Get AI Profile Template"])
            .WithDescription(S["Returns one template's full content: instructions, profile settings, tool and agent names."])
            .WithCategory(S["AI Profile Templates"])
            .Selectable();

        services.AddCoreAITool<SaveAIProfileTemplateTool>(SaveAIProfileTemplateTool.TheName)
            .WithTitle(S["Save AI Profile Template"])
            .WithDescription(S["Creates or updates a stored template of source Profile or SystemPrompt, including a speaking style or guardrail topic (set category). A SystemPrompt template changes every profile that selects it from the next message, so it cannot be one the running assistant uses. Shipped templates cannot be changed."])
            .WithCategory(S["AI Profile Templates"])
            .Selectable();

        services.AddCoreAITool<DeleteAIProfileTemplateTool>(DeleteAIProfileTemplateTool.TheName)
            .WithTitle(S["Delete AI Profile Templates"])
            .WithDescription(S["Deletes stored templates by id. Profiles that selected a deleted SystemPrompt template lose that block of instructions."])
            .WithCategory(S["AI Profile Templates"])
            .RequireApproval()
            .Selectable();
    }
}
