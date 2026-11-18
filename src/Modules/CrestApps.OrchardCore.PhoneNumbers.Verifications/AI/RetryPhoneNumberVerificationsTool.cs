using System.Text.Json;
using CrestApps.OrchardCore.PhoneNumbers.Core.Permissions;
using CrestApps.OrchardCore.PhoneNumbers.Core.Services;
using CrestApps.OrchardCore.PhoneNumbers.Verifications.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.PhoneNumbers.Verifications.AI;

/// <summary>
/// Re-queues phone number verifications as the queue page's Retry, Retry selected and Retry all failed actions do,
/// through the same records service, and refuses when no provider is enabled. The page also verifies the records at the
/// end of its request; a chat call would wait for every billable lookup there, so the tool leaves them to the Phone
/// Number Revalidation background task, which picks re-queued records up at its next run with the configured delay.
/// </summary>
internal sealed class RetryPhoneNumberVerificationsTool : PhoneNumberVerificationsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "retryPhoneNumberVerifications";

    /// <summary>
    /// The most content items one call re-queues by id.
    /// </summary>
    public const int MaxContentItemIds = 100;

    private const int MaxListedIds = 50;

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "contentItemIds": {
          "type": "array",
          "items": { "type": "string" },
          "description": "The content item ids of the records to verify again, from listPhoneNumberVerifications (at most 100)."
        },
        "allFailed": {
          "type": "boolean",
          "description": "true to re-queue every record whose last verification failed, including those out of attempts, instead of giving ids."
        },
        "number": {
          "type": "string",
          "description": "With allFailed, only the failed records whose phone number contains these digits."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Re-queues phone verifications for the given content item ids, or for all failed ones, and runs them against the enabled lookup provider. Each lookup is a billable provider call; fails when no provider is enabled.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => PhoneNumberVerificationsPermissions.VerifyPhoneNumbers;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, CancellationToken cancellationToken)
    {
        var ids = ReadStringList(arguments, "contentItemIds")?.Distinct(StringComparer.Ordinal).ToList() ?? [];
        var allFailed = bool.TryParse(ReadString(arguments, "allFailed"), out var all) && all;
        var term = ReadString(arguments, "number");

        if (allFailed == (ids.Count > 0))
        {
            return "Nothing was queued: give the records in contentItemIds, or pass allFailed: true, not both.";
        }

        if (term is not null && !allFailed)
        {
            return "Nothing was queued: number narrows allFailed only. Give the records in contentItemIds without it.";
        }

        if (ids.Count > MaxContentItemIds)
        {
            return $"Nothing was queued: give at most {MaxContentItemIds} content item ids at once, or use allFailed.";
        }

        if ((await services.GetRequiredService<IPhoneNumberVerificationManager>().GetEnabledProvidersAsync(cancellationToken)).Count == 0)
        {
            return "Nothing was queued: no phone number verification provider is enabled. Enable AbstractAPI, Veriphone or Twilio phone number verification and set it up under Settings, Phone Number Verifications first.";
        }

        var recordsService = services.GetRequiredService<PhoneNumberVerificationRecordsService>();

        if (allFailed)
        {
            ids = [.. await recordsService.GetFailedContentItemIdsAsync(term)];

            if (ids.Count == 0)
            {
                return Json(new
                {
                    queued = 0,
                    message = term is null ? "There are no failed records to retry." : $"There are no failed records whose number contains '{term}'.",
                });
            }
        }

        var requeued = await recordsService.RequeueAsync(ids, processAfterRequest: false);
        var notQueued = allFailed ? new List<string>() : ids.Except(requeued, StringComparer.Ordinal).ToList();

        return Json(new
        {
            queued = requeued.Count,
            contentItemIds = requeued.Take(MaxListedIds).ToList(),
            contentItemIdsTruncated = requeued.Count > MaxListedIds,
            notFound = notQueued.Count == 0 ? null : notQueued,
            cappedAt = allFailed && ids.Count >= PhoneNumberVerificationRecordsService.MaxRetryAllFailedRecords ? PhoneNumberVerificationRecordsService.MaxRetryAllFailedRecords : (int?)null,
            message = requeued.Count == 0
                ? "Nothing was queued: none of the ids is a record with phone number verification. Use listPhoneNumberVerifications for the ids."
                : $"{requeued.Count} record(s) were marked pending with their failure counts cleared. The Phone Number Revalidation background task verifies them with the enabled provider at its next run (every five minutes), one billable lookup each, with the configured delay between requests. Check the results with listPhoneNumberVerifications.",
        });
    }
}
