using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Agent.UrlRewriting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Registers the tools that list, save and delete URL redirect and rewrite rules, when the URL Rewriting feature is on.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.UrlRewriting)]
public sealed class UrlRewritingToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="UrlRewritingToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public UrlRewritingToolsStartup(IStringLocalizer<UrlRewritingToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListUrlRewriteRulesTool>(ListUrlRewriteRulesTool.TheName)
            .WithTitle(S["List URL Rewrite Rules"])
            .WithDescription(S["Lists the URL redirect and rewrite rules in the order they are applied, with their patterns and options. Call it before adding a rule so it does not conflict with an existing one."])
            .WithCategory(S["URL Rewriting"])
            .Selectable();

        services.AddCoreAITool<SaveUrlRewriteRuleTool>(SaveUrlRewriteRuleTool.TheName)
            .WithTitle(S["Save URL Rewrite Rule"])
            .WithDescription(S["Creates or updates one redirect or rewrite rule (pattern, substitution and options); a new rule is applied after the existing ones. Rules apply to live requests at once; a wrong pattern can make site pages unreachable."])
            .WithCategory(S["URL Rewriting"])
            .Selectable();

        services.AddCoreAITool<DeleteUrlRewriteRuleTool>(DeleteUrlRewriteRuleTool.TheName)
            .WithTitle(S["Delete URL Rewrite Rule"])
            .WithDescription(S["Deletes one or more URL rewriting rules by id."])
            .WithCategory(S["URL Rewriting"])
            .RequireApproval()
            .Selectable();
    }
}
