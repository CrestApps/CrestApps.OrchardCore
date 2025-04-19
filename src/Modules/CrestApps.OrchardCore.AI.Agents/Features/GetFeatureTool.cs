using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using OrchardCore.DisplayManagement.Extensions;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.AI.Agents.Features;

public sealed class GetFeatureTool : AIFunction
{
    public const string TheName = "getSiteFeature";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IShellFeaturesManager _shellFeaturesManager;

    public GetFeatureTool(
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
                "featureId": {
                  "type": "string",
                  "description": "A unique feature ID to get info for."
                }
              },
              "additionalProperties": false,
              "required": ["featureId"]
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
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, OrchardCorePermissions.ManageFeatures))
        {
            return "The current user does not have permission to manage features.";
        }

        if (!arguments.TryGetFirstString("featureId", out var featureId))
        {
            return "Unable to find a featureId argument in the function arguments.";
        }

        var feature = (await _shellFeaturesManager.GetAvailableFeaturesAsync())
            .FirstOrDefault(feature => !feature.IsTheme() && feature.Id.Equals(featureId, StringComparison.OrdinalIgnoreCase));

        if (feature is null)
        {
            return $"Unable to find a feature with the ID: {featureId}.";
        }

        var isEnabled = await _shellFeaturesManager.IsFeatureEnabledAsync(feature.Id);

        return JsonSerializer.Serialize(feature.AsAIObject(isEnabled));
    }
}
