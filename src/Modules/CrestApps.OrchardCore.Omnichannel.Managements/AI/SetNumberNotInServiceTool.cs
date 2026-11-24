using System.Security.Claims;
using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Omnichannel.Managements.AI;

/// <summary>
/// Marks numbers as not in service by hand, or allows dialing them again, as the Numbers Not In Service page does.
/// </summary>
internal sealed class SetNumberNotInServiceTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "setNumberNotInService";

    /// <summary>
    /// The most numbers one call changes.
    /// </summary>
    public const int MaxNumbers = 50;

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "phoneNumbers": {
          "type": "array",
          "items": { "type": "string" },
          "minItems": 1,
          "maxItems": 50,
          "description": "The phone numbers, each with its country code, for example +17025550123."
        },
        "notInService": {
          "type": "boolean",
          "description": "true marks the numbers as not in service so campaigns skip them; false clears the mark so they can be loaded and dialed again."
        },
        "reason": {
          "type": "string",
          "description": "Why the numbers are marked, when marking."
        }
      },
      "required": ["phoneNumbers", "notInService"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Marks one or more phone numbers as not in service so campaigns skip them, or clears the mark so they can be loaded and dialed again. Numbers must include the country code.";

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

        if (!await AIModuleTools.IsAuthorizedAsync(services, OmnichannelConstants.Permissions.ManageActivities))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var numbers = OmnichannelToolSupport.ReadStrings(arguments, "phoneNumbers");

        if (numbers is not { Length: > 0 })
        {
            return "Nothing was changed: give at least one phone number in phoneNumbers.";
        }

        if (numbers.Length > MaxNumbers)
        {
            return $"Nothing was changed: give at most {MaxNumbers} numbers in one call.";
        }

        if (!arguments.TryGetFirst<bool>("notInService", out var notInService))
        {
            return "Nothing was changed: say with notInService whether to mark the numbers (true) or allow dialing them again (false).";
        }

        var service = services.GetRequiredService<INotInServiceNumberService>();
        var caller = AIModuleTools.GetCaller(services);
        var changed = new List<string>();
        var unchanged = new List<string>();
        var unreadable = new List<string>();

        foreach (var number in numbers)
        {
            if (service.Normalize(number) is null)
            {
                unreadable.Add(number);

                continue;
            }

            if (notInService)
            {
                var marked = await service.MarkAsync(new NotInServiceMark
                {
                    PhoneNumber = number,
                    Source = OmnichannelConstants.NotInServiceSources.Manual,
                    Reason = AIModuleTools.ReadString(arguments, "reason"),
                    MarkedById = caller?.FindFirstValue(ClaimTypes.NameIdentifier),
                    MarkedByUsername = caller?.Identity?.Name,
                }, cancellationToken);

                if (marked is null)
                {
                    unreadable.Add(number);
                }
                else
                {
                    changed.Add(marked.PhoneNumber);
                }
            }
            else if (await service.ClearAsync(number, cancellationToken))
            {
                changed.Add(service.Normalize(number));
            }
            else
            {
                unchanged.Add(service.Normalize(number));
            }
        }

        return AIModuleTools.Json(new
        {
            action = notInService ? "marked as not in service" : "allowed to be dialed again",
            changed,
            notMarkedBefore = notInService ? null : unchanged,
            unreadable = unreadable.Count == 0
                ? null
                : new
                {
                    numbers = unreadable,
                    message = "These are not phone numbers that can be read. Give each with its country code, for example +17025550123.",
                },
        });
    }
}
