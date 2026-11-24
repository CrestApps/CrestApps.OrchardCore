using System.Text.Json;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Managements.AI;

/// <summary>
/// Lists or searches the numbers marked as not in service, as the Numbers Not In Service page does.
/// </summary>
internal sealed class ListNotInServiceNumbersTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listNotInServiceNumbers";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "search": {
          "type": "string",
          "description": "Digits the phone number contains, such as 7025550123 or a whole number with its country code."
        },
        "page": {
          "type": "integer",
          "minimum": 1,
          "description": "The page to return, from 1."
        },
        "pageSize": {
          "type": "integer",
          "minimum": 1,
          "maximum": 50,
          "description": "How many numbers a page holds, at most 50. Defaults to 20."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists or searches phone numbers marked as not in service, with who or what marked them and when.";

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

        var (page, pageSize) = AIModuleTools.ReadPaging(arguments);
        var result = await services.GetRequiredService<INotInServiceNumberService>()
            .PageAsync(page, pageSize, AIModuleTools.ReadString(arguments, "search"), cancellationToken);

        var campaignIds = result.Entries
            .Select(entry => entry.CampaignId)
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var campaignCatalog = services.GetService<ICatalog<OmnichannelCampaign>>();
        var campaignNames = campaignIds.Length == 0 || campaignCatalog is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : (await campaignCatalog.GetAsync(campaignIds, cancellationToken)).ToDictionary(campaign => campaign.ItemId, campaign => campaign.DisplayText, StringComparer.Ordinal);

        var localClock = services.GetService<ILocalClock>();
        var items = new List<object>();

        foreach (var number in result.Entries)
        {
            items.Add(new
            {
                phoneNumber = number.PhoneNumber,
                source = number.Source,
                reason = number.Reason,
                markedByUsername = number.MarkedByUsername,
                campaignId = number.CampaignId,
                campaignName = number.CampaignId is not null ? campaignNames.GetValueOrDefault(number.CampaignId) : null,
                activityId = number.ActivityId,
                contactId = number.ContactContentItemId,
                firstDetected = await AIModuleTools.ToLocalAsync(localClock, number.FirstDetectedUtc),
                lastDetected = await AIModuleTools.ToLocalAsync(localClock, number.LastDetectedUtc),
                detectionCount = number.DetectionCount,
            });
        }

        return AIModuleTools.Json(new
        {
            items,
            total = result.Count,
            page,
            pageSize,
            truncated = page * pageSize < result.Count,
        });
    }
}
