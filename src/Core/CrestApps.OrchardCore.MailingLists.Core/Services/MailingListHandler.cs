using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Core.Handlers;
using CrestApps.OrchardCore.MailingLists.Models;
using CrestApps.OrchardCore.MailingLists.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.MailingLists.Core.Services;

/// <summary>
/// Stamps and checks mailing lists, for the editor and recipes alike.
/// </summary>
public sealed partial class MailingListHandler : AuditedCatalogEntryHandlerBase<MailingList>
{
    /// <summary>
    /// The shortest time a confirmation link may work.
    /// </summary>
    public static readonly TimeSpan MinLinkLifetime = TimeSpan.FromHours(1);

    /// <summary>
    /// The longest time a confirmation link may work.
    /// </summary>
    public static readonly TimeSpan MaxLinkLifetime = TimeSpan.FromDays(30);

    private readonly MailingListStore _store;
    private readonly IEnumerable<IMailingListSettingsValidator> _settingsValidators;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="MailingListHandler"/> class.
    /// </summary>
    /// <param name="store">The list store, to keep names unique.</param>
    /// <param name="settingsValidators">The checks of the settings other modules keep on a list.</param>
    /// <param name="httpContextAccessor">The accessor that gives the user making the change.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The localizer.</param>
    public MailingListHandler(
        MailingListStore store,
        IEnumerable<IMailingListSettingsValidator> settingsValidators,
        IHttpContextAccessor httpContextAccessor,
        IClock clock,
        IStringLocalizer<MailingListHandler> stringLocalizer)
        : base(httpContextAccessor, clock)
    {
        _store = store;
        _settingsValidators = settingsValidators;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override async Task ValidatingAsync(ValidatingContext<MailingList> context, CancellationToken cancellationToken = default)
    {
        var list = context.Model;

        list.Name = list.Name?.Trim();
        list.DisplayText = list.DisplayText?.Trim();

        if (string.IsNullOrWhiteSpace(list.Name))
        {
            context.Result.Fail(new ValidationResult(S["The technical name is required."], [nameof(MailingList.Name)]));
        }
        else if (!TechnicalName().IsMatch(list.Name))
        {
            context.Result.Fail(new ValidationResult(S["The technical name may hold letters, digits, dashes and underscores only, and starts with a letter."], [nameof(MailingList.Name)]));
        }
        else if (await _store.FindByNameAsync(list.Name, cancellationToken) is { } other && other.ItemId != list.ItemId)
        {
            context.Result.Fail(new ValidationResult(S["Another mailing list has this technical name."], [nameof(MailingList.Name)]));
        }

        if (string.IsNullOrWhiteSpace(list.DisplayText))
        {
            context.Result.Fail(new ValidationResult(S["The name people see is required."], [nameof(MailingList.DisplayText)]));
        }

        if (!Enum.IsDefined(list.Confirmation))
        {
            context.Result.Fail(new ValidationResult(S["Choose how a sign-up is confirmed."], [nameof(MailingList.Confirmation)]));
        }

        if (list.ConfirmationLinkLifetime < MinLinkLifetime || list.ConfirmationLinkLifetime > MaxLinkLifetime)
        {
            context.Result.Fail(new ValidationResult(S["A confirmation link works between one hour and 30 days."], [nameof(MailingList.ConfirmationLinkLifetime)]));
        }

        if (string.IsNullOrWhiteSpace(list.ProviderName))
        {
            list.ProviderName = MailingListsConstants.LocalProviderName;
        }

        foreach (var validator in _settingsValidators)
        {
            foreach (var error in await validator.ValidateAsync(list, cancellationToken) ?? [])
            {
                context.Result.Fail(error);
            }
        }
    }

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_-]{0,127}$")]
    private static partial Regex TechnicalName();
}
