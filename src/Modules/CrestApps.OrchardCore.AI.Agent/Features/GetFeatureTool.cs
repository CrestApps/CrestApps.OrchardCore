using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Extensions;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.AI.Agent.Features;

public sealed class GetFeatureTool : AIFunction
{
    public const string TheName = "getSiteFeature";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
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
        """);

    public override string Name => TheName;

    public override string Description => "Enables feature site features";

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

        if (!arguments.TryGetFirstString("featureId", out var featureId))
        {
            return "Unable to find a featureId argument in the function arguments.";
        }

        var feature = (await shellFeaturesManager.GetAvailableFeaturesAsync())
            .FirstOrDefault(feature => !feature.IsTheme() && feature.Id.Equals(featureId, StringComparison.OrdinalIgnoreCase));

        if (feature is null)
        {
            return $"Unable to find a feature with the ID: {featureId}.";
        }

        var isEnabled = await shellFeaturesManager.IsFeatureEnabledAsync(feature.Id);

        return JsonSerializer.Serialize(feature.AsAIObject(isEnabled));
    }
}
