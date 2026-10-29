using CrestApps.Core.AI.Chat;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// Finishes a secure keypad entry: exchanges the digits for a token through secure data capture, keeps the token and
/// the masked value as conversation variables, and writes the turn the AI hears in place of the digits.
/// </summary>
internal static class SecureKeypadEntries
{
    /// <summary>
    /// Finishes an entry.
    /// </summary>
    /// <param name="services">The request's services.</param>
    /// <param name="activityId">The call's activity.</param>
    /// <param name="entry">The finished or cancelled entry.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The turn the AI answers, which never holds the digits.</returns>
    public static async Task<string> CompleteAsync(IServiceProvider services, string activityId, SecureKeypadEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var label = Describe(entry.Field);

        if (entry.State == SecureKeypadEntryState.Cancelled)
        {
            return $"[The caller cancelled typing the {label} on the keypad. Nothing was saved.]";
        }

        var sink = services?.GetService<ISecureCaptureTokenSink>();
        var logger = services?.GetService<ILoggerFactory>()?.CreateLogger(typeof(SecureKeypadEntries));

        if (sink is null)
        {
            return $"[The caller typed the {label}, but secure data capture is not available, so it was not saved. Do not ask them to read it aloud; offer another way.]";
        }

        SecureCaptureTokenResult result;

        try
        {
            result = await sink.TokenizeAsync(entry.Field, entry.Value, $"ai-{activityId}-{entry.Field}", cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger?.LogWarning(ex, "A secure keypad entry on AI call activity '{ActivityId}' could not be tokenized.", activityId.SanitizeLogValue());

            return $"[The {label} the caller typed could not be saved. Ask them to try typing it again, followed by the pound key.]";
        }

        if (result is null || !result.Succeeded)
        {
            return $"[The {label} the caller typed was not accepted: {result?.ErrorMessage ?? "it is not valid"}. Ask them to type it again, followed by the pound key.]";
        }

        await SaveVariablesAsync(services, activityId, entry.Field, result, cancellationToken);

        if (logger is not null && logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("A secure keypad entry ({Field}) on AI call activity '{ActivityId}' was saved as a token.", entry.Field, activityId.SanitizeLogValue());
        }

        var saved = string.IsNullOrEmpty(result.MaskedValue) ? "accepted" : $"saved securely as {result.MaskedValue}";

        return $"[The caller typed the {label} on the keypad and it was {saved}. You do not see the digits and must never ask for them aloud.]";
    }

    /// <summary>
    /// The conversation variable a field's token is kept in, without the <c>_token</c> or <c>_masked</c> suffix.
    /// </summary>
    /// <param name="field">The field.</param>
    /// <returns>The name.</returns>
    public static string VariableName(SecureCaptureField field)
        => "secure_" + field switch
        {
            SecureCaptureField.CreditCardNumber => "card_number",
            SecureCaptureField.CardExpiry => "card_expiry",
            SecureCaptureField.CardSecurityCode => "card_security_code",
            SecureCaptureField.BankAccountNumber => "bank_account",
            SecureCaptureField.NationalId => "national_id",
            _ => "entry",
        };

    /// <summary>
    /// The field in words, for the AI.
    /// </summary>
    /// <param name="field">The field.</param>
    /// <returns>The words.</returns>
    public static string Describe(SecureCaptureField field)
        => field switch
        {
            SecureCaptureField.CreditCardNumber => "card number",
            SecureCaptureField.CardExpiry => "card expiry date",
            SecureCaptureField.CardSecurityCode => "card security code",
            SecureCaptureField.BankAccountNumber => "bank account number",
            SecureCaptureField.NationalId => "identity number",
            _ => "number",
        };

    private static async Task SaveVariablesAsync(IServiceProvider services, string activityId, SecureCaptureField field, SecureCaptureTokenResult result, CancellationToken cancellationToken)
    {
        var name = VariableName(field);
        var values = new Dictionary<string, string>(StringComparer.Ordinal);

        if (!string.IsNullOrEmpty(result.MaskedValue))
        {
            values[name + "_masked"] = result.MaskedValue;
        }

        // A value the sink says must not be kept (a security code) leaves no token behind.
        if (!string.IsNullOrEmpty(result.Token))
        {
            values[name + "_token"] = result.Token;
        }

        if (values.Count == 0)
        {
            return;
        }

        // The live conversation sees the values at once; the session keeps them for later turns and workflows.
        AIConversationScope.Current?.SetVariables(values);

        var activityStore = services.GetService<IOmnichannelActivityStore>();
        var sessionManager = services.GetService<IAIChatSessionManager>();
        var activity = activityStore is null ? null : await activityStore.FindByIdAsync(activityId, cancellationToken);

        if (sessionManager is null || string.IsNullOrEmpty(activity?.AISessionId))
        {
            return;
        }

        var session = await sessionManager.FindByIdAsync(activity.AISessionId, cancellationToken);

        if (session is not null && session.SetConversationVariables(values))
        {
            await sessionManager.SaveAsync(session, cancellationToken);
        }
    }
}
