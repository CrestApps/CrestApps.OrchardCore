using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using CrestApps.Core.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.PhoneNumbers.AI;

/// <summary>
/// Parses a phone number with <see cref="IPhoneNumberService"/>, the one way the platform turns a raw number into its
/// E.164 form, and returns the value to store as <see cref="PhoneNumberNormalization.ToE164OrTrimmed"/> computes it. Pure
/// computation; it needs a signed-in caller and no permission.
/// </summary>
internal sealed class NormalizePhoneNumberTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "normalizePhoneNumber";

    /// <summary>
    /// The answer to a caller who is not signed in.
    /// </summary>
    public const string NotAllowedMessage = "You are not allowed to use this function.";

    private static readonly HashSet<string> _argumentNames = new(StringComparer.Ordinal) { "phoneNumber", "regionCode" };

    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "phoneNumber": {
          "type": "string",
          "description": "The phone number as written, such as (555) 010-9999 or +44 20 7946 0958."
        },
        "regionCode": {
          "type": "string",
          "description": "The two-letter ISO country the number is read in when it has no country code, such as US or GB. Not needed for a number that starts with +."
        }
      },
      "required": ["phoneNumber"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Parses a phone number for a region and returns whether it is valid, its E.164 form, display form, region and time zones. Call it before storing a phone number so it can match CRM, caller-ID and do-not-call records.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>
    {
        ["Strict"] = false,
    };

    /// <inheritdoc/>
    protected override ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var services = arguments.Services;

        if (GetCaller(services) is null)
        {
            return ValueTask.FromResult<object>(NotAllowedMessage);
        }

        if (arguments.Keys.FirstOrDefault(name => !_argumentNames.Contains(name)) is { } unknown)
        {
            return ValueTask.FromResult<object>($"Nothing was checked: '{unknown}' is not an argument of {Name}. Use phoneNumber and regionCode.");
        }

        var raw = ReadString(arguments, "phoneNumber");
        var regionCode = ReadString(arguments, "regionCode")?.ToUpperInvariant();

        if (raw is null)
        {
            return ValueTask.FromResult<object>("Nothing was checked: give the number in phoneNumber.");
        }

        var service = services.GetRequiredService<IPhoneNumberService>();

        if (regionCode is not null && !service.GetSupportedRegions().Contains(regionCode, StringComparer.OrdinalIgnoreCase))
        {
            return ValueTask.FromResult<object>($"Nothing was checked: '{regionCode}' is not a two-letter country code phone numbers are known for, such as US, GB or DE.");
        }

        var valueToStore = PhoneNumberNormalization.ToE164OrTrimmed(raw, service, regionCode);

        if (!service.TryParse(raw, regionCode, out var phoneNumber))
        {
            return ValueTask.FromResult<object>(JsonSerializer.Serialize(new
            {
                input = raw,
                regionCode,
                valid = false,
                valueToStore,
                message = regionCode is null && !raw.TrimStart().StartsWith('+')
                    ? "The number is not valid as an international number. Give regionCode (the country it is dialed in) or a number that starts with +."
                    : "The number is not a valid phone number for its country. It would be stored as typed, so it cannot be matched to caller ID or do-not-call records.",
            }, _jsonOptions));
        }

        var region = service.GetRegionCode(phoneNumber);

        return ValueTask.FromResult<object>(JsonSerializer.Serialize(new
        {
            input = raw,
            regionCode,
            valid = true,
            e164 = phoneNumber.Value,
            display = service.FormatForDisplay(phoneNumber.Value, regionCode ?? region),
            international = service.FormatForDisplay(phoneNumber.Value),
            region,
            countryCallingCode = region is null ? null : "+" + service.GetCountryCode(region).ToString(CultureInfo.InvariantCulture),
            timeZones = service.GetTimeZones(phoneNumber.Value),
            valueToStore,
            message = string.Equals(valueToStore, phoneNumber.Value, StringComparison.Ordinal)
                ? "Store the number as valueToStore (its E.164 form)."
                : "The number is valid, but what was typed carries more than the number (such as an extension), so it is stored as typed in valueToStore.",
        }, _jsonOptions));
    }

    private static string ReadString(AIFunctionArguments arguments, string name)
    {
        if (!arguments.TryGetValue(name, out var value) || value is null)
        {
            return null;
        }

        var text = value is JsonElement element
            ? element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Null or JsonValueKind.Undefined => null,
                _ => element.ToString(),
            }
            : Convert.ToString(value, CultureInfo.InvariantCulture);

        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    private static ClaimsPrincipal GetCaller(IServiceProvider services)
    {
        var user = services.GetService<IUserAccessor>()?.User
            ?? services.GetService<IHttpContextAccessor>()?.HttpContext?.User;

        return user?.Identity?.IsAuthenticated == true
            ? user
            : null;
    }
}
