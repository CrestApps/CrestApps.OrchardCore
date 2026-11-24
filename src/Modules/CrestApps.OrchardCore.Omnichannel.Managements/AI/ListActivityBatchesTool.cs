using System.Text.Json;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Managements.AI;

/// <summary>
/// Lists the activity loads, as the Load Activities page does, or returns the full definition of one.
/// </summary>
public sealed class ListActivityBatchesTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listActivityBatches";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "The ID of one load to return in full. Leave it out to list loads."
        },
        "search": {
          "type": "string",
          "description": "Text the load's title contains."
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
          "description": "How many loads a page holds, at most 50. Defaults to 20."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists activity loads with their source, campaign, channel, status (New, Started, Loading, Loaded), totals loaded/matched/skipped and any load error. Pass an id to get the full definition of one load.";

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

        if (!await AIModuleTools.IsAuthorizedAsync(services, OmnichannelConstants.Permissions.ManageActivityBatches))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var manager = services.GetRequiredService<ICatalogManager<OmnichannelActivityBatch>>();
        var id = AIModuleTools.ReadString(arguments, "id");

        if (id is not null)
        {
            var batch = await manager.FindByIdAsync(id, cancellationToken);

            if (batch is null)
            {
                return $"No activity load has the ID '{id}'. Call {TheName} without an id to see the loads.";
            }

            return AIModuleTools.Json(await DescribeAsync(services, batch, full: true));
        }

        var (page, pageSize) = AIModuleTools.ReadPaging(arguments);
        var result = await manager.PageAsync(page, pageSize, new QueryContext
        {
            Name = AIModuleTools.ReadString(arguments, "search"),
        }, cancellationToken);
        var items = new List<object>();

        foreach (var batch in result.Entries)
        {
            items.Add(await DescribeAsync(services, batch, full: false));
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

    internal static async Task<object> DescribeAsync(IServiceProvider services, OmnichannelActivityBatch batch, bool full)
    {
        var localClock = services.GetService<ILocalClock>();

        return new
        {
            id = batch.ItemId,
            displayText = batch.DisplayText,
            source = batch.Source,
            campaignId = batch.CampaignId,
            channel = batch.Channel,
            subjectContentType = batch.SubjectContentType,
            contactContentType = batch.ContactContentType,
            status = batch.Status.ToString(),
            totals = new
            {
                loaded = batch.TotalLoaded,
                matched = batch.TotalMatched,
                skippedAsDuplicate = batch.TotalSkippedAsDuplicate,
                skippedAsOptedOut = batch.TotalSkippedAsOptedOut,
                skippedAsSharedNumberOptedOut = batch.TotalSkippedAsSharedNumberOptedOut,
                skippedForNoDestination = batch.TotalSkippedForNoDestination,
                skippedAsNotInService = batch.TotalSkippedAsNotInService,
                skippedByLimit = batch.TotalSkippedByLimit,
                skippedAsConverted = batch.TotalSkippedAsConverted,
                skippedAsExistingContact = batch.TotalSkippedAsExistingContact,
            },
            loadError = batch.LoadError,
            loadStarted = await AIModuleTools.ToLocalAsync(localClock, batch.LoadStartedUtc),
            loadStartedBy = batch.LoadStartedByUsername,
            created = await AIModuleTools.ToLocalAsync(localClock, batch.CreatedUtc),
            definition = full
                ? new
                {
                    channelEndpointId = batch.ChannelEndpointId,
                    aiProfileId = batch.AIProfileId,
                    cadenceId = batch.CadenceId,
                    dialerProfileId = batch.DialerProfileId,
                    businessHoursCalendarId = batch.BusinessHoursCalendarId,
                    assignedUserIds = batch.UserIds,
                    scheduleAt = await AIModuleTools.ToLocalAsync(localClock, batch.ScheduleAt),
                    urgencyLevel = batch.UrgencyLevel.ToString(),
                    instructions = batch.Instructions,
                    preventDuplicates = batch.PreventDuplicates,
                    includeDoNotCall = batch.IncludeDoNoCalls,
                    includeDoNotSms = batch.IncludeDoNoSms,
                    includeDoNotEmail = batch.IncludeDoNoEmail,
                    limit = batch.Limit,
                    timeZoneIds = batch.TimeZoneIds,
                    phoneNumberFilter = batch.PhoneNumber,
                    lastActivitySubjectContentType = batch.LastActivitySubjectContentType,
                    lastActivityDispositionId = batch.LastActivityDispositionId,
                    lastActivityAttempts = batch.LastActivityAttempts,
                }
                : null,
            editUrl = OmnichannelToolSupport.GetPath(services, "ActivityBatches", "Edit", batch.ItemId),
        };
    }
}
