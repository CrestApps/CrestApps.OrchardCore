using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using OrchardCore.DisplayManagement.Extensions;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.AI.Agents.Features;

internal sealed class DisableFeatureTool : AIFunction
{
    public const string TheName = "disableSiteFeature";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IShellFeaturesManager _shellFeaturesManager;

    public DisableFeatureTool(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IShellFeaturesManager shellFeaturesManager)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _shellFeaturesManager = shellFeaturesManager;
        JsonSchema = JsonSerializer.Deserialize<JsonElement>(
           """
            {
              "type": "object",
              "properties": {
                "featureIds": {
                  "type": "array",
                  "items": {
                    "type": "string"
                  },
                  "minItems": 1,
                  "description": "A list of unique feature IDs to disable."
                }
              },
              "additionalProperties": false,
              "required": ["featureIds"]
            }
            """, JsonSerializerOptions);
    }

    public override string Name => TheName;

    public override string Description => "Disable features site features";

    public override JsonElement JsonSchema { get; }

    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, OrchardCorePermissions.ManageFeatures))
        {
            return "The current user does not have permission to manage features.";
        }

        if (!arguments.TryGetFirst<HashSet<string>>("featureIds", out var featureIds))
        {
            return "Unable to find a featureIds argument in the function arguments.";
        }

        if (featureIds.Count == 0)
        {
            return "The featureIds argument is required.";
        }

        var features = (await _shellFeaturesManager.GetAvailableFeaturesAsync())
            .Where(feature => featureIds.Contains(feature.Id) && !feature.EnabledByDependencyOnly && !feature.IsTheme());

        if (!features.Any())
        {
            return "Invalid feature ids provided";
        }

        await _shellFeaturesManager.DisableFeaturesAsync(features, true);

        return $"The feature(s) were disabled successfully. {JsonSerializer.Serialize(features.Select(feature => new
        {
            feature.Name,
            feature.Id,
            feature.Category,
            IsEnabled = false,
            feature.IsAlwaysEnabled,
            feature.DefaultTenantOnly,
            feature.EnabledByDependencyOnly,
            feature.Dependencies
        }))}";
    }
}
