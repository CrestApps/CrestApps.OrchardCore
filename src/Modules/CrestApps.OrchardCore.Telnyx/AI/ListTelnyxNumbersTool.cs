using System.Text.Json;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Telnyx.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Telnyx.AI;

/// <summary>
/// Lists the phone numbers the Telnyx account owns, with the carriers' spam rejections of each, as the Telnyx numbers
/// page does.
/// </summary>
internal sealed class ListTelnyxNumbersTool : TelnyxToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listTelnyxNumbers";

    /// <summary>
    /// The most numbers read from Telnyx, as the page reads them.
    /// </summary>
    public const int MaxNumbersRead = 250;

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "contains": {
          "type": "string",
          "description": "Only numbers whose digits contain these digits, for example 0144."
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
          "description": "Numbers per page, 20 when not given, at most 50."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists the phone numbers the Telnyx account owns, with status and reputation (carrier spam rejections over the last 30 days). Telnyx does not return the caller name here.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, CancellationToken cancellationToken)
    {
        var (result, numbers) = await services.GetRequiredService<TelnyxApiClient>().ListOwnedNumbersAsync(1, MaxNumbersRead, cancellationToken);

        if (!result.Succeeded)
        {
            return $"Telnyx did not list the numbers ({Describe(result)}). Check the API key in the Telnyx settings.";
        }

        await services.GetRequiredService<TelnyxCallerNumberReputation>().ApplyToAsync(numbers);

        var contains = Digits(AIModuleTools.ReadString(arguments, "contains"));
        var rows = numbers
            .Where(number => contains.Length == 0 || Digits(number.PhoneNumber).Contains(contains, StringComparison.Ordinal))
            .OrderBy(number => number.PhoneNumber, StringComparer.Ordinal)
            .ToList();

        var (page, pageSize) = AIModuleTools.ReadPaging(arguments);
        var localClock = services.GetService<ILocalClock>();
        var items = new List<object>();

        foreach (var number in rows.Skip((page - 1) * pageSize).Take(pageSize))
        {
            items.Add(new
            {
                id = number.Id,
                phoneNumber = number.PhoneNumber,
                status = number.Status,
                hasVoiceConnection = !string.IsNullOrWhiteSpace(number.ConnectionId),
                hasMessagingProfile = !string.IsNullOrWhiteSpace(number.MessagingProfileId),
                spamRejections = number.SpamRejections,
                lastSpamRejection = await AIModuleTools.ToLocalAsync(localClock, number.LastSpamRejectionUtc),
            });
        }

        return AIModuleTools.Json(new
        {
            items,
            total = rows.Count,
            page,
            pageSize,
            truncated = page * pageSize < rows.Count,
            note = numbers.Count >= MaxNumbersRead
                ? $"Only the first {MaxNumbersRead} numbers of the account were read, as on the Telnyx numbers page."
                : null,
        });
    }
}
