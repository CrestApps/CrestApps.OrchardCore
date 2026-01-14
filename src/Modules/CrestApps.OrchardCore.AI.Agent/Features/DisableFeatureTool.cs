using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Extensions;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.AI.Agent.Features;

internal sealed class DisableFeatureTool : AIFunction
{
    public const string TheName = "disableSiteFeature";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
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
        """);

    public override string Name => TheName;

    public override string Description => "Disable features site features";

    public override JsonElement JsonSchema => _jsonSchema;

    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var httpContextAccessor = arguments.Services.GetRequiredService<IHttpContextAccessor>();
        var authorizationService = arguments.Services.GetRequiredService<IAuthorizationService>();
        var shellFeaturesManager = arguments.Services.GetRequiredService<IShellFeaturesManager>();

        if (!await authorizationService.AuthorizeAsync(httpContextAccessor.HttpContext.User, OrchardCorePermissions.ManageFeatures))
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

        var features = (await shellFeaturesManager.GetAvailableFeaturesAsync())
            .Where(feature => featureIds.Contains(feature.Id) && !feature.EnabledByDependencyOnly && !feature.IsTheme());

        if (!features.Any())
        {
            return "Invalid feature ids provided";
        }

        await shellFeaturesManager.DisableFeaturesAsync(features, true);

        return $"The feature(s) were disabled successfully. {JsonSerializer.Serialize(features.Select(feature => feature.AsAIObject(false)))}";
    }
}
