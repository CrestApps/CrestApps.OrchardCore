using System.Text.Json;
using CrestApps.OrchardCore.PhoneNumbers.Core.Permissions;
using CrestApps.OrchardCore.PhoneNumbers.Core.Services;
using CrestApps.OrchardCore.PhoneNumbers.Verifications.Services;
using CrestApps.OrchardCore.PhoneNumbers.Verifications.ViewModels;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.PhoneNumbers.Verifications.AI;

/// <summary>
/// Lists the phone number verification queue with the search, status filter, counts and sort of the Phone Verifications
/// Queue page, through the same records service. Times are in the user's or the site's time zone.
/// </summary>
internal sealed class ListPhoneNumberVerificationsTool : PhoneNumberVerificationsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listPhoneNumberVerifications";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "number": {
          "type": "string",
          "description": "Digits in the phone number, as typed or in E.164 form."
        },
        "status": {
          "type": "string",
          "enum": ["All", "Verified", "Invalid", "Pending", "Failed", "NeedsAttention"],
          "description": "The status filter: Failed can still be retried automatically, NeedsAttention failed and is out of attempts, Pending waits for a verification. All by default."
        },
        "sort": {
          "type": "string",
          "enum": ["RecentlyAttempted", "LeastRecentlyAttempted", "RecentlyCreated", "OldestCreated"],
          "description": "The order. RecentlyAttempted by default."
        },
        "page": {
          "type": "integer",
          "description": "The page, starting at 1."
        },
        "pageSize": {
          "type": "integer",
          "description": "The records per page, 20 by default and at most 50."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists the phone numbers queued for or checked by verification, with status, provider, attempts, last error and next due date, and the count per status; filter by number, status and sort. Use it to find failed or stuck verifications.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => PhoneNumberVerificationsPermissions.RunPhoneNumberVerificationsReport;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, CancellationToken cancellationToken)
    {
        var statusText = ReadString(arguments, "status");
        var sortText = ReadString(arguments, "sort");
        var status = PhoneNumberVerificationRecordFilter.All;
        var sort = PhoneNumberVerificationRecordSort.RecentlyAttempted;

        if (statusText is not null && (!Enum.TryParse(statusText, ignoreCase: true, out status) || !Enum.IsDefined(status)))
        {
            return $"Nothing was listed: '{statusText}' is not a status. Use All, Verified, Invalid, Pending, Failed or NeedsAttention.";
        }

        if (sortText is not null && (!Enum.TryParse(sortText, ignoreCase: true, out sort) || !Enum.IsDefined(sort)))
        {
            return $"Nothing was listed: '{sortText}' is not a sort. Use RecentlyAttempted, LeastRecentlyAttempted, RecentlyCreated or OldestCreated.";
        }

        var term = ReadString(arguments, "number");
        var page = Math.Max(1, ReadInt(arguments, "page") ?? 1);
        var pageSize = Math.Clamp(ReadInt(arguments, "pageSize") ?? DefaultPageSize, 1, MaxPageSize);
        var recordsService = services.GetRequiredService<PhoneNumberVerificationRecordsService>();
        var maxAttempts = await recordsService.GetMaxAttemptsAsync();
        var (total, records) = await recordsService.ListAsync(term, status, sort, (page - 1) * pageSize, pageSize, maxAttempts);
        var counts = await recordsService.CountByStatusAsync(maxAttempts, term);
        var enabledProviders = await services.GetRequiredService<IPhoneNumberVerificationManager>().GetEnabledProvidersAsync(cancellationToken);

        var items = new List<object>();

        foreach (var record in records)
        {
            items.Add(new
            {
                contentItemId = record.ContentItemId,
                displayText = record.DisplayText,
                contentType = record.ContentType,
                phoneNumber = record.PhoneNumber,
                normalizedPhoneNumber = record.NormalizedPhoneNumber,
                status = record.VerificationStatus.ToString(),
                needsAttention = record.IsExhausted,
                provider = record.VerificationProvider,
                attempts = record.VerificationAttemptCount,
                failedAttempts = record.FailedAttemptCount,
                lastError = HideKeys(record.LastError),
                lastAttempt = await ToLocalAsync(services, record.LastAttemptUtc),
                lastVerified = await ToLocalAsync(services, record.LastVerifiedUtc),
                nextDue = await ToLocalAsync(services, record.NextVerificationDueUtc),
                lineStatus = record.LineStatus,
            });
        }

        return Json(new
        {
            items,
            total,
            page,
            pageSize,
            truncated = page * pageSize < total,
            counts = counts.ToDictionary(count => count.Key.ToString(), count => count.Value),
            maxFailedAttempts = maxAttempts,
            enabledProviders = enabledProviders.Select(provider => provider.Key).ToList(),
            message = enabledProviders.Count == 0
                ? "No verification provider is enabled, so nothing is being verified. Enable AbstractAPI, Veriphone or Twilio phone number verification and set it up under Settings, Phone Number Verifications."
                : "Records in NeedsAttention are no longer retried automatically; retryPhoneNumberVerifications queues them again.",
        });
    }
}
