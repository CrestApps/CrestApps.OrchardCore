using System.Text.Json;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telnyx.Services;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.Telnyx.AI;

/// <summary>
/// What the Telnyx tools share: the base of every tool (the caller must hold <b>Manage telephony settings</b>, as every
/// Telnyx page checks), finding a number the account owns, and relaying a refusal from Telnyx.
/// </summary>
internal abstract class TelnyxToolBase : AIFunction
{
    private const int MaxErrorLength = 300;

    /// <inheritdoc/>
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>
    {
        ["Strict"] = false,
    };

    /// <inheritdoc/>
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        if (!await AIModuleTools.IsAuthorizedAsync(arguments.Services, TelephonyPermissions.ManageTelephonySettings))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        return await InvokeAsync(arguments, arguments.Services, cancellationToken);
    }

    /// <summary>
    /// Runs the tool for a signed-in caller who may manage the telephony settings.
    /// </summary>
    protected abstract Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, CancellationToken cancellationToken);

    /// <summary>
    /// Parses a JSON schema.
    /// </summary>
    protected static JsonElement Schema(string schema)
        => JsonSerializer.Deserialize<JsonElement>(schema);

    /// <summary>
    /// Finds a number the account owns, by its Telnyx id or by the number itself.
    /// </summary>
    /// <returns>The number, or an error sentence when Telnyx could not list the numbers or none matches.</returns>
    internal static async Task<(TelnyxOwnedNumber Number, string Error)> FindOwnedAsync(TelnyxApiClient client, string idOrNumber, CancellationToken cancellationToken)
    {
        var (result, numbers) = await client.ListOwnedNumbersAsync(1, 250, cancellationToken);

        if (!result.Succeeded)
        {
            return (null, $"Telnyx did not list the account's numbers ({Describe(result)}). Check the API key in the Telnyx settings.");
        }

        var digits = Digits(idOrNumber);
        var number = numbers.FirstOrDefault(candidate => string.Equals(candidate.Id, idOrNumber, StringComparison.Ordinal))
            ?? (digits.Length >= 7
                ? numbers.FirstOrDefault(candidate => Digits(candidate.PhoneNumber) == digits)
                : null);

        return number is null
            ? (null, $"the Telnyx account does not own a number with the id or number '{idOrNumber}'. Use listTelnyxNumbers to find it.")
            : (number, null);
    }

    /// <summary>
    /// Describes a refusal from Telnyx: its status code and the start of its answer.
    /// </summary>
    internal static string Describe(TelnyxApiResult result)
    {
        var body = result?.ErrorBody;
        var status = result?.StatusCode is { } code ? $"status {(int)code}" : "no answer";

        if (string.IsNullOrWhiteSpace(body))
        {
            return status;
        }

        body = body.Trim();

        return $"{status}: {(body.Length > MaxErrorLength ? body[..MaxErrorLength] + "…" : body)}";
    }

    /// <summary>
    /// Keeps the digits of a number.
    /// </summary>
    internal static string Digits(string number)
        => new((number ?? string.Empty).Where(char.IsAsciiDigit).ToArray());
}
