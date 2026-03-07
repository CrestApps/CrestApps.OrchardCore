using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.DisplayManagement.Extensions;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.AI.Agent.Features;

public sealed class ListFeaturesTool : AIFunction
{
    public const string TheName = "listSiteFeature";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
        """
        {
          "type": "object",
          "properties": {},
          "additionalProperties": false
        }
        """);

    public override string Name => TheName;

    public override string Description => "List features on the site";

    public override JsonElement JsonSchema => _jsonSchema;

    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var logger = arguments.Services.GetRequiredService<ILogger<ListFeaturesTool>>();

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("AI tool '{ToolName}' invoked.", Name);
        }

        var shellFeaturesManager = arguments.Services.GetRequiredService<IShellFeaturesManager>();

        var features = (await shellFeaturesManager.GetAvailableFeaturesAsync())
            .Where(feature => !feature.EnabledByDependencyOnly && !feature.IsTheme());

        var enabledFeatureIds = (await shellFeaturesManager.GetEnabledFeaturesAsync())
            .Select(x => x.Id)
            .ToHashSet();

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("AI tool '{ToolName}' completed.", Name);
        }

        return JsonSerializer.Serialize(features.Select(feature => feature.AsAIObject(enabledFeatureIds.Contains(feature.Id))));
    }
}
