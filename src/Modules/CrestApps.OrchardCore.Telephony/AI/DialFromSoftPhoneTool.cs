using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Telephony.AI;

/// <summary>
/// Asks the calling user's own soft phone to dial a number, as the call button beside a phone field does
/// (<c>SoftPhoneDialerEndpoints</c>). The call is not placed here: the soft phone places it, through the telephony
/// service that screens every outbound call.
/// </summary>
internal sealed class DialFromSoftPhoneTool : TelephonyToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "dialFromSoftPhone";

    private const int MaxNumberLength = 64;

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "number": {
          "type": "string",
          "description": "The number or extension to call, for example +15550103344 or 204."
        }
      },
      "required": ["number"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Asks the calling user's own soft phone to dial a number. Nothing happens when their soft phone is not open; outbound screening (do-not-call, short codes) still applies in the phone.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => TelephonyPermissions.UseSoftPhone;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(Arguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
        {
            return NotAllowedMessage;
        }

        var number = arguments.GetString("number");

        if (number is null)
        {
            return "Nothing was dialed: give the number to call.";
        }

        if (number.Length > MaxNumberLength || number.Any(char.IsControl))
        {
            return "Nothing was dialed: that is not a phone number or extension.";
        }

        // Only the caller's own soft phones receive the request; with none open it reaches nobody.
        await services.GetRequiredService<ISoftPhoneClientNotifier>().DialRequestedAsync(userId, new TelephonyDialRequest
        {
            Number = number,
        });

        return Json(new
        {
            requested = number,
            note = "The request was sent to your own soft phone, which places the call if it is open; nothing happens otherwise. The phone applies outbound screening, so a blocked number is refused there.",
        });
    }
}
