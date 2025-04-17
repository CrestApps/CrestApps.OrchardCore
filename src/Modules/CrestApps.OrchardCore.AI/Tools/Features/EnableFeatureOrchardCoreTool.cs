using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using OrchardCore.DisplayManagement.Extensions;
using OrchardCore.Environment.Shell;
using OrchardCore.Features;

namespace CrestApps.OrchardCore.AI.Tools.Features;

public sealed class EnableFeatureOrchardCoreTool : AIFunction
{
    public const string TheName = "enableSiteFeature";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IShellFeaturesManager _shellFeaturesManager;

    public EnableFeatureOrchardCoreTool(
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
                  "description": "A list of unique feature IDs to enable."
                }
              },
              "additionalProperties": false,
              "required": ["featureIds"]
            }
            """, JsonSerializerOptions);
    }

    public override string Name => TheName;

    public override string Description => "Enables feature site features";

    public override JsonElement JsonSchema { get; }

    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, Permissions.ManageFeatures))
        {
            return "The current user does not have permission to manage features.";
        }

        if (!arguments.TryGetValue("featureIds", out var featureIdsArg))
        {
            return "Unable to find a featureIds argument in the function arguments.";
        }

        var featureIds = ToolHelpers.GetStringValues(featureIdsArg);

        if (!featureIds.Any())
        {
            return "The featureIds argument is required.";
        }

        var ids = featureIds.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var features = (await _shellFeaturesManager.GetAvailableFeaturesAsync())
            .Where(feature => ids.Contains(feature.Id) && !feature.EnabledByDependencyOnly && !feature.IsTheme())
            .ToArray();

        if (features.Length == 0)
        {
            return "Invalid feature ids provided";
        }

        await _shellFeaturesManager.EnableFeaturesAsync(features, true);

        return $"The feature(s) were enabled successfully. {JsonSerializer.Serialize(features.Select(x => new { x.Name, x.Id, x.Dependencies, x.Category }))}";
    }
}
