using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using OrchardCore.DisplayManagement.Extensions;
using OrchardCore.Environment.Shell;
using OrchardCore.Features;

namespace CrestApps.OrchardCore.AI.Tools.Features;

public sealed class FeaturesSearchOrchardCoreTool : AIFunction
{
    public const string TheName = "searchSiteFeature";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IShellFeaturesManager _shellFeaturesManager;

    public FeaturesSearchOrchardCoreTool(
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
                "name": {
                  "type": "string",
                  "description": "A term used to search for relevant features."
                }
              },
              "additionalProperties": false,
              "required": ["name"]
            }
            """, JsonSerializerOptions);
    }

    public override string Name => TheName;

    public override string Description => "Search for a feature on the site";

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

        if (!arguments.TryGetValue("name", out var nameArg))
        {
            return "Unable to find a name argument in the function arguments.";
        }

        var name = ToolHelpers.GetStringValue(nameArg);

        if (string.IsNullOrEmpty(name))
        {
            return "The name is argument is required.";
        }

        var features = (await _shellFeaturesManager.GetAvailableFeaturesAsync())
            .Where(feature => !feature.EnabledByDependencyOnly && !feature.IsTheme() && (feature.Name.Contains(name, StringComparison.OrdinalIgnoreCase) || feature.Id.Contains(name, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        return JsonSerializer.Serialize(features.Select(x => new { x.Name, x.Id, x.Dependencies, x.Category }));
    }
}
