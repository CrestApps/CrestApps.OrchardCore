using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Extensions;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.AI.Agent.Features;

public sealed class FeaturesSearchTool : AIFunction
{
    public const string TheName = "searchSiteFeature";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
       """
        {
          "type": "object",
          "properties": {
            "name": {
              "type": "string",
              "description": "A term used to search for relevant features."
            }
          },
          "additionalProperties": false,
          "required": ["name"]
        }
        """);

    public override string Name => TheName;

    public override string Description => "Search for a feature on the site";

    public override JsonElement JsonSchema => _jsonSchema;

    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var httpContextAccessor = arguments.Services.GetRequiredService<IHttpContextAccessor>();
        var authorizationService = arguments.Services.GetRequiredService<IAuthorizationService>();
        var shellFeaturesManager = arguments.Services.GetRequiredService<IShellFeaturesManager>();

        if (!await authorizationService.AuthorizeAsync(httpContextAccessor.HttpContext.User, OrchardCorePermissions.ManageFeatures))
        {
            return "The current user does not have permission to manage features.";
        }

        if (!arguments.TryGetFirstString("name", out var name))
        {
            return "Unable to find a name argument in the function arguments.";
        }

        var features = (await shellFeaturesManager.GetAvailableFeaturesAsync())
            .Where(feature => !feature.EnabledByDependencyOnly && !feature.IsTheme() && (feature.Name.Contains(name, StringComparison.OrdinalIgnoreCase) || feature.Id.Contains(name, StringComparison.OrdinalIgnoreCase)));

        var enabledFeatureIds = (await shellFeaturesManager.GetEnabledFeaturesAsync())
            .Select(x => x.Id)
            .ToHashSet();

        return JsonSerializer.Serialize(features.Select(feature => feature.AsAIObject(enabledFeatureIds.Contains(feature.Id))));
    }
}
