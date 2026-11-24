using System.Text.Json;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Telnyx.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Telnyx.AI;

/// <summary>
/// Sets the caller name an owned number shows, as the caller name form of the Telnyx numbers page does. Only a number
/// the account owns is changed.
/// </summary>
internal sealed class SetTelnyxNumberCallerNameTool : TelnyxToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "setTelnyxNumberCallerName";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "number": {
          "type": "string",
          "description": "The owned number, by its Telnyx id or the number itself, for example +17025550144."
        },
        "callerName": {
          "type": "string",
          "description": "The caller name, at most 15 letters, digits and spaces; other characters are dropped. Empty stops showing one."
        }
      },
      "required": ["number", "callerName"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Sets the caller name shown for an owned number. Carriers can take days to show it.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, CancellationToken cancellationToken)
    {
        var numberText = AIModuleTools.ReadString(arguments, "number");

        if (numberText is null || !arguments.ContainsKey("callerName"))
        {
            return "Nothing was changed: give the number and the caller name.";
        }

        var callerName = arguments["callerName"]?.ToString() ?? string.Empty;
        var client = services.GetRequiredService<TelnyxApiClient>();
        var (number, error) = await FindOwnedAsync(client, numberText, cancellationToken);

        if (number is null)
        {
            return $"Nothing was changed: {error}";
        }

        var result = await client.SetCallerNameAsync(number.Id, callerName, cancellationToken);

        if (!result.Succeeded)
        {
            return $"Nothing was changed: Telnyx did not save the caller name ({Describe(result)}).";
        }

        var saved = TelnyxApiClient.NormalizeCallerName(callerName);

        return AIModuleTools.Json(new
        {
            id = number.Id,
            phoneNumber = number.PhoneNumber,
            callerName = saved,
            note = saved.Length == 0
                ? "The number no longer lists a caller name."
                : "The caller name was saved. Carriers can take a few days to show it.",
        });
    }
}
