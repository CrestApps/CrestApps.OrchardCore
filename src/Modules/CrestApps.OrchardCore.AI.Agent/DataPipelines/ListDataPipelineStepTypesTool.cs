using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DataPipelines.Models;
using OrchardCore.DataPipelines.Steps;

namespace CrestApps.OrchardCore.AI.Agent.DataPipelines;

/// <summary>
/// Lists the step types of the data pipeline designer's toolbox by category, with their input and output ports, as the
/// designer's step library does. The settings schema of each step type is not offered: nothing in Orchard Core maps a
/// step type to its settings.
/// </summary>
public sealed class ListDataPipelineStepTypesTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listDataPipelineStepTypes";

    private static readonly DataPipelineStepCategory[] _categories =
    [
        DataPipelineStepCategory.Source,
        DataPipelineStepCategory.Transform,
        DataPipelineStepCategory.File,
        DataPipelineStepCategory.Destination,
    ];

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "category": {
          "type": "string",
          "enum": [
            "Source",
            "Transform",
            "File",
            "Destination"
          ],
          "description": "Only list the step types of this category. Leave it out to list every step type."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists the data pipeline step types available on this site by category (source, transform, file, destination), with their name, display name, description and input and output ports (records or files, required, accepts many). The settings of each step are edited in the pipeline designer.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    /// <inheritdoc/>
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var services = arguments.Services;

        if (!await AIToolCallerAuthorization.IsAuthorizedAsync(services, OrchardCorePermissions.ViewDataPipelines))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var categoryName = arguments.GetFirstValueOrDefault<string>("category");
        DataPipelineStepCategory? onlyCategory = null;

        if (!string.IsNullOrWhiteSpace(categoryName))
        {
            if (!Enum.TryParse<DataPipelineStepCategory>(categoryName, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
            {
                return $"There is no step category '{categoryName}'. Use Source, Transform, File or Destination.";
            }

            onlyCategory = parsed;
        }

        var descriptors = services.GetService<IOptions<DataPipelineStepTypeOptions>>()?.Value.StepTypes ?? [];
        var localizerFactory = services.GetRequiredService<IStringLocalizerFactory>();
        var categories = new List<object>();
        var total = 0;

        foreach (var category in _categories.Where(category => onlyCategory is null || category == onlyCategory))
        {
            var steps = new List<(string DisplayName, object Step)>();

            foreach (var descriptor in descriptors.Where(descriptor => descriptor.Category == category))
            {
                // The step types are registered as services under their own class, as the step type manager resolves them.
                if (services.GetService(descriptor.Type) is not IDataPipelineStepType stepType)
                {
                    continue;
                }

                var step = new DataPipelineStep { Type = descriptor.Name };
                var displayName = localizerFactory.Localize(descriptor.DisplayName)?.Value ?? descriptor.Name;

                steps.Add((displayName, new
                {
                    name = descriptor.Name,
                    displayName,
                    description = localizerFactory.Localize(descriptor.Description)?.Value,
                    inputs = stepType.GetInputs(step).Select(DescribePort).ToArray(),
                    outputs = stepType.GetOutputs(step).Select(DescribePort).ToArray(),
                }));
            }

            if (steps.Count == 0)
            {
                continue;
            }

            total += steps.Count;

            categories.Add(new
            {
                category = category.ToString(),
                stepTypes = steps
                    .OrderBy(entry => entry.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                    .Select(entry => entry.Step)
                    .ToArray(),
            });
        }

        return AgentToolResults.Json(new
        {
            categories,
            total,
            note = "A step's settings are kept under its Properties, edited in the pipeline designer. Media, email, content items, FTP and SFTP steps are listed only when those features are enabled.",
        });
    }

    private static object DescribePort(DataPipelinePort port)
    {
        return new
        {
            name = port.Name,
            displayName = port.DisplayName,
            kind = port.Kind.ToString(),
            isRequired = port.IsRequired,
            allowsMany = port.AllowsMany,
        };
    }
}
