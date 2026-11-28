using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using CrestApps.Core.Security;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Currencies;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;
using YesSql;
using YesSqlSession = YesSql.ISession;

namespace CrestApps.OrchardCore.Invoicing.AI;

/// <summary>
/// The base of the invoicing AI tools added after the first four: it acts as the person using the AI, read from
/// <see cref="IUserAccessor"/> (which the chat hubs set for the whole invocation), refuses a caller who is not signed in
/// and any argument the tool does not declare, finds invoices by number or id, and keeps the chat message's database
/// session safe: an invoice command that is refused discards the session's unsaved work, so the work of earlier tool
/// calls is written first, and nothing more is written in a message once the session discarded its work
/// (<see cref="InvoicingToolScope"/>).
/// </summary>
/// <remarks>
/// It derives from <see cref="InvoicingToolBase"/> to answer with the same invoice summary as <c>findInvoices</c>, but
/// never uses that base's permission check, which reads the caller from the HTTP request only.
/// </remarks>
internal abstract class InvoiceAIToolBase : InvoicingToolBase
{
    /// <summary>
    /// The longest reason, as the admin page's reason fields.
    /// </summary>
    protected const int MaxReasonLength = 500;

    private HashSet<string> _argumentNames;

    /// <inheritdoc/>
    protected sealed override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var services = arguments.Services;

        if (services is null || GetCaller(services) is not { } caller)
        {
            return NotAllowedMessage;
        }

        _argumentNames ??= GetArgumentNames(JsonSchema);

        if (arguments.Keys.FirstOrDefault(name => !_argumentNames.Contains(name)) is { } unknown)
        {
            return $"Nothing was done: '{unknown}' is not an argument of {Name}. Use only the arguments the tool declares.";
        }

        return await InvokeAsync(arguments, services, caller, cancellationToken);
    }

    /// <summary>
    /// Runs the tool for a signed-in caller. The tool checks the permission it needs.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="caller">The caller.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The tool's result.</returns>
    protected abstract Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the signed-in caller from <see cref="IUserAccessor"/>.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <returns>The caller, or <see langword="null"/> when nobody is signed in.</returns>
    protected static ClaimsPrincipal GetCaller(IServiceProvider services)
        => services.GetService<IUserAccessor>()?.User is { Identity.IsAuthenticated: true } user ? user : null;

    /// <summary>
    /// Whether the caller holds a permission.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="caller">The caller.</param>
    /// <param name="permission">The permission.</param>
    /// <returns><see langword="true"/> when the caller holds it.</returns>
    protected static async Task<bool> IsAuthorizedAsync(IServiceProvider services, ClaimsPrincipal caller, Permission permission)
        => caller is not null &&
            services.GetService<IAuthorizationService>() is { } authorizationService &&
            await authorizationService.AuthorizeAsync(caller, permission);

    /// <summary>
    /// Gets the name the invoice commands record for the caller, as the admin pages pass <c>User.Identity.Name</c>.
    /// </summary>
    /// <param name="caller">The caller.</param>
    /// <returns>The caller's user name.</returns>
    protected static string ActorName(ClaimsPrincipal caller)
        => caller?.Identity?.Name;

    /// <summary>
    /// Finds an invoice by its number, else by its id.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="key">The number or id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The invoice, or the sentence that says why there is none.</returns>
    protected static async Task<(Invoice Invoice, string Error)> FindInvoiceAsync(IServiceProvider services, string key, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return (null, "Give the invoice's number, such as INV-1001, or its id from findInvoices.");
        }

        var invoices = services.GetRequiredService<IInvoiceManager>();
        var invoice = await invoices.FindByNumberAsync(key, cancellationToken) ?? await invoices.FindByIdAsync(key, cancellationToken);

        return invoice is null
            ? (null, $"No invoice has the number or id '{key}'. Use findInvoices to find it.")
            : (invoice, null);
    }

    /// <summary>
    /// Writes the work of earlier tool calls of the chat message, so a command that is refused and discards the
    /// session's work cannot take it along, and refuses when an earlier change of the message was discarded.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="null"/> when the change can go ahead, or the sentence that says why not.</returns>
    protected static async Task<string> WriteEarlierWorkAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        if (services.GetService<InvoicingToolScope>()?.SessionClosed == true)
        {
            return "Nothing was changed: an earlier change in this message was refused, so nothing more can be saved in it. Ask again in your next message.";
        }

        try
        {
            await services.GetRequiredService<YesSqlSession>().SaveChangesAsync(cancellationToken);

            return null;
        }
        catch (ConcurrencyException)
        {
            MarkSessionClosed(services);

            return "Nothing was changed: an earlier change in this message conflicted with someone else's and was not saved. Ask again in your next message.";
        }
    }

    /// <summary>
    /// Commits what a tool queued in the session, such as an invoice brought in step with its ledger.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when it was saved.</returns>
    protected static async Task<bool> SaveAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var session = services.GetRequiredService<YesSqlSession>();

        try
        {
            await session.SaveChangesAsync(cancellationToken);

            return true;
        }
        catch (ConcurrencyException)
        {
            await session.CancelAsync();
            MarkSessionClosed(services);

            return false;
        }
    }

    /// <summary>
    /// Records that the session discarded its work in this message, as an invoice command does when it is refused.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    protected static void MarkSessionClosed(IServiceProvider services)
    {
        if (services.GetService<InvoicingToolScope>() is { } scope)
        {
            scope.SessionClosed = true;
        }
    }

    /// <summary>
    /// Records the outcome of an invoice command: a refused one discarded the session's work.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="result">The command's result.</param>
    protected static void NoteCommand(IServiceProvider services, InvoiceCommandResult result)
    {
        if (result is null || !result.Succeeded && !result.AlreadyDone)
        {
            MarkSessionClosed(services);
        }
    }

    /// <summary>
    /// Converts a stored UTC time to the user's or the site's time zone.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="utc">The UTC time.</param>
    /// <returns>The local time with its offset, or <see langword="null"/>.</returns>
    protected static Task<string> LocalTimeAsync(IServiceProvider services, DateTime? utc)
        => AIModuleTools.ToLocalAsync(services.GetService<ILocalClock>(), utc);

    /// <summary>
    /// Writes a pure date, such as an issue or due date, as it is stored.
    /// </summary>
    /// <param name="date">The date.</param>
    /// <returns>The date as yyyy-MM-dd, or <see langword="null"/>.</returns>
    protected static string Date(DateOnly? date)
        => date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>
    /// Gets the path of an invoice's admin page.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="invoiceId">The invoice's id.</param>
    /// <returns>The path, or <see langword="null"/> when there is no request to build it from.</returns>
    protected static string InvoiceAdminPath(IServiceProvider services, string invoiceId)
        => string.IsNullOrEmpty(invoiceId)
            ? null
            : AIModuleTools.GetAdminPath(services, InvoicingConstants.Features.Area, "InvoicesAdmin", "Detail", new RouteValueDictionary { ["itemId"] = invoiceId });

    /// <summary>
    /// Writes a result as JSON.
    /// </summary>
    /// <param name="value">The result, built from an explicit list of fields.</param>
    /// <returns>The JSON text.</returns>
    protected static string Json(object value)
        => AIModuleTools.Json(value);

    /// <summary>
    /// Writes an amount for a sentence, at the currency's precision, such as 20.00 USD.
    /// </summary>
    /// <param name="amount">The amount.</param>
    /// <param name="currency">The currency.</param>
    /// <returns>The amount and currency.</returns>
    protected static string Format(decimal amount, string currency)
        => $"{CurrencyScale.Format(amount, currency)} {currency}";

    /// <summary>
    /// Writes an enum value as the tools name it, starting in lower case.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The name.</returns>
    protected static string ToCamel(string value)
        => string.IsNullOrEmpty(value) ? value : char.ToLowerInvariant(value[0]) + value[1..];

    /// <summary>
    /// Reads a yes or no argument.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <returns>The value, or <see langword="null"/> when it is missing or not true or false.</returns>
    protected static bool? ReadBool(AIFunctionArguments arguments, string name)
        => ReadJson(arguments, name) is { } element
            ? element.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String when bool.TryParse(element.GetString(), out var parsed) => parsed,
                _ => null,
            }
            : null;

    /// <summary>
    /// Reads a decimal number argument.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <param name="value">The number, or <see langword="null"/> when it is missing.</param>
    /// <returns><see langword="false"/> when it is given but is not a number.</returns>
    protected static bool TryReadDecimal(AIFunctionArguments arguments, string name, out decimal? value)
    {
        value = null;

        if (ReadJson(arguments, name) is not { } element)
        {
            return true;
        }

        if ((element.ValueKind == JsonValueKind.Number && element.TryGetDecimal(out var number)) ||
            (element.ValueKind == JsonValueKind.String && decimal.TryParse(element.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out number)))
        {
            value = number;

            return true;
        }

        return false;
    }

    /// <summary>
    /// Reads a date argument written as yyyy-MM-dd.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <param name="value">The date, or <see langword="null"/> when it is missing.</param>
    /// <returns><see langword="false"/> when it is given but is not a date.</returns>
    protected static bool TryReadDate(AIFunctionArguments arguments, string name, out DateOnly? value)
    {
        value = null;

        if (Read(arguments, name) is not { } text)
        {
            return true;
        }

        if (DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            value = date;

            return true;
        }

        return false;
    }

    private static JsonElement? ReadJson(AIFunctionArguments arguments, string name)
    {
        if (arguments is null || !arguments.TryGetValue(name, out var value) || value is null)
        {
            return null;
        }

        var element = value is JsonElement json ? json : JsonSerializer.SerializeToElement(value);

        return element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? null : element;
    }

    private static HashSet<string> GetArgumentNames(JsonElement schema)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);

        if (schema.ValueKind == JsonValueKind.Object && schema.TryGetProperty("properties", out var properties) && properties.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in properties.EnumerateObject())
            {
                names.Add(property.Name);
            }
        }

        return names;
    }
}
