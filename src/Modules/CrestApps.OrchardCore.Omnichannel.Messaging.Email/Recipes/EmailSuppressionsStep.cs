using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Core.Recipes;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Deliverability;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Recipes;

/// <summary>
/// Adds addresses to the email suppression list from an <c>EmailSuppressions</c> recipe step, so a list kept by another
/// site or exported from the sending provider can be brought in. An address is matched by itself, and one already on the
/// list keeps its first reason, so running the recipe twice adds nothing.
/// </summary>
/// <remarks>
/// <code>
/// { "name": "EmailSuppressions", "Suppressions": [ { "Address": "ann@example.com", "Reason": "HardBounce",
///   "Detail": "5.1.1 User unknown" } ] }
/// </code>
/// The reason is <c>HardBounce</c>, <c>RepeatedSoftBounces</c>, <c>Complaint</c> or <c>Manual</c>; <c>Manual</c> when
/// it is left out. The addresses are personal data.
/// </remarks>
internal sealed class EmailSuppressionsStep : NamedRecipeStepHandler
{
    public const string StepKey = "EmailSuppressions";

    public const string EntriesName = "Suppressions";

    private readonly IEmailSuppressionList _suppressions;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailSuppressionsStep"/> class.
    /// </summary>
    /// <param name="suppressions">The suppression list.</param>
    public EmailSuppressionsStep(IEmailSuppressionList suppressions)
        : base(StepKey)
    {
        _suppressions = suppressions;
    }

    /// <inheritdoc/>
    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        // An address listed twice in the step is added once; the list itself keeps an address once too.
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var token in CatalogRecipeSections.Section(context.Step, EntriesName))
        {
            var address = OmnichannelEmailAddress.Normalize(GetString(token, nameof(EmailSuppression.Address)));

            if (!OmnichannelEmailAddress.IsValid(address))
            {
                context.Errors.Add($"An entry of the {StepKey} step has no valid email address.");

                continue;
            }

            if (!TryReadReason(token, out var reason))
            {
                context.Errors.Add($"The {StepKey} step gives '{address}' a reason that is not HardBounce, RepeatedSoftBounces, Complaint or Manual.");

                continue;
            }

            if (!seen.Add(address))
            {
                continue;
            }

            await _suppressions.SuppressAsync(address, reason, GetString(token, nameof(EmailSuppression.Detail)));
        }
    }

    // The reason is read by name, as the deployment step writes it, or by number; a reason left out is a manual one.
    private static bool TryReadReason(JsonObject token, out EmailSuppressionReason reason)
    {
        reason = EmailSuppressionReason.Manual;

        if (token[nameof(EmailSuppression.Reason)] is not JsonValue value)
        {
            return true;
        }

        if (value.TryGetValue<string>(out var text))
        {
            return string.IsNullOrWhiteSpace(text) ||
                (Enum.TryParse(text.Trim(), ignoreCase: true, out reason) && Enum.IsDefined(reason));
        }

        if (value.TryGetValue<int>(out var number) && Enum.IsDefined((EmailSuppressionReason)number))
        {
            reason = (EmailSuppressionReason)number;

            return true;
        }

        return false;
    }

    private static string GetString(JsonObject token, string name)
        => token[name] is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text)
            ? text.Trim()
            : null;
}
