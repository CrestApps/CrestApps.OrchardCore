using CrestApps.Core.Models;
using CrestApps.OrchardCore.AI.Records;
using CrestApps.OrchardCore.Transactions.Core.Services;
using CrestApps.OrchardCore.Transactions.Models;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Transactions.AI;

/// <summary>
/// The record kinds Transactions gives the generic record tools: the payment terms (<see cref="PaymentTermKind"/>), whose
/// editor is a display driver, and the parties' collection settings (<see cref="PartyCollectionSettingsKind"/>), read-only
/// because their card posts to a controller, written with <c>setPartyCollectionSettings</c>.
/// </summary>
internal static class TransactionRecordKinds
{
    /// <summary>
    /// The name of the payment term kind.
    /// </summary>
    public const string PaymentTermKind = "paymentTerm";

    /// <summary>
    /// The name of the collection settings kind.
    /// </summary>
    public const string PartyCollectionSettingsKind = "partyCollectionSettings";

    /// <summary>
    /// The allow-listed fields of a payment term. The kind is written as the editor takes it, such as <c>NetDays</c>.
    /// </summary>
    /// <param name="term">The term.</param>
    /// <returns>The fields.</returns>
    public static object ProjectPaymentTerm(PaymentTerm term)
        => new
        {
            id = term.ItemId,
            name = term.Name,
            description = term.Description,
            kind = term.Kind.ToString(),
            days = term.Days,
            dayOfMonth = term.DayOfMonth,
            discountPercent = term.DiscountPercent,
            discountDays = term.DiscountDays,
            enabled = term.Enabled,
        };

    /// <summary>
    /// The allow-listed fields of a party's collection settings. When they were changed is left out: a projection can't
    /// turn it into local time.
    /// </summary>
    /// <param name="settings">The settings.</param>
    /// <returns>The fields.</returns>
    public static object ProjectPartyCollectionSettings(PartyCollectionSettings settings)
        => new
        {
            id = settings.ItemId,
            party = settings.Party.ToString(),
            partyKind = settings.Party.Kind,
            partyId = settings.Party.Id,
            remindersPaused = settings.RemindersPaused,
            lateFeesExempt = settings.LateFeesExempt,
        };

    /// <summary>
    /// Finds a party's collection settings by their id.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="id">The settings' id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The settings, or <see langword="null"/>.</returns>
    public static async ValueTask<PartyCollectionSettings> FindPartyCollectionSettingsAsync(IServiceProvider services, string id, CancellationToken cancellationToken)
        => string.IsNullOrEmpty(id) ? null : await services.GetRequiredService<PartyCollectionSettingsStore>().FindByIdAsync(id, cancellationToken);

    /// <summary>
    /// Lists the parties kept out of the reminders or the late fees, which are the ones with settings that matter.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="query">The text and the page.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The page.</returns>
    public static async ValueTask<PageResult<PartyCollectionSettings>> ListPartyCollectionSettingsAsync(IServiceProvider services, AIRecordQuery query, CancellationToken cancellationToken)
        => Page(await services.GetRequiredService<PartyCollectionSettingsStore>().ListExcludedAsync(cancellationToken), query);

    /// <summary>
    /// Filters and pages the parties' settings: the text matches a party's id, its kind or the whole <c>Kind:Id</c>.
    /// </summary>
    /// <param name="settings">The settings.</param>
    /// <param name="query">The text and the page.</param>
    /// <returns>The page.</returns>
    internal static PageResult<PartyCollectionSettings> Page(IEnumerable<PartyCollectionSettings> settings, AIRecordQuery query)
    {
        var text = query?.Text?.Trim();
        var matches = (settings ?? [])
            .Where(item => item is not null && !item.Party.IsEmpty && (string.IsNullOrEmpty(text) ||
                item.Party.Id.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.Party.Kind, text, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.Party.ToString(), text, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(item => item.Party.Kind, StringComparer.Ordinal)
            .ThenBy(item => item.Party.Id, StringComparer.Ordinal)
            .ToList();
        var page = Math.Max(1, query?.Page ?? 1);
        var pageSize = Math.Max(1, query?.PageSize ?? 20);

        return new PageResult<PartyCollectionSettings>
        {
            Count = matches.Count,
            Entries = matches.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
        };
    }
}
