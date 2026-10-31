using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Skills.Core.Services;
using CrestApps.OrchardCore.Skills.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Skills.Core.Handlers;

/// <summary>
/// Stamps certifications, trims their names, keeps the names unique and checks the validity and reminder days.
/// </summary>
internal sealed class CertificationCatalogHandler : CatalogEntryHandlerBase<Certification>
{
    private readonly CertificationStore _store;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public CertificationCatalogHandler(
        CertificationStore store,
        IClock clock,
        IStringLocalizer<CertificationCatalogHandler> stringLocalizer)
    {
        _store = store;
        _clock = clock;
        S = stringLocalizer;
    }

    public override Task InitializedAsync(InitializedContext<Certification> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    public override Task CreatingAsync(CreatingContext<Certification> context, CancellationToken cancellationToken = default)
    {
        Tidy(context.Model);

        return Task.CompletedTask;
    }

    public override Task UpdatingAsync(UpdatingContext<Certification> context, CancellationToken cancellationToken = default)
    {
        Tidy(context.Model);
        context.Model.ModifiedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    public override async Task ValidatingAsync(ValidatingContext<Certification> context, CancellationToken cancellationToken = default)
    {
        var certification = context.Model;

        Tidy(certification);

        if (certification.ValidityMonths is < 1)
        {
            context.Result.Fail(new ValidationResult(S["A certificate must be valid for at least one month, or leave the validity empty when it never expires."], [nameof(Certification.ValidityMonths)]));
        }

        if (certification.ReminderDays.Any(days => days < 1))
        {
            context.Result.Fail(new ValidationResult(S["Reminders are days before expiry, so each must be 1 or more."], [nameof(Certification.ReminderDays)]));
        }

        if (string.IsNullOrEmpty(certification.Name))
        {
            context.Result.Fail(new ValidationResult(S["The name is required."], [nameof(Certification.Name)]));

            return;
        }

        var existing = await _store.FindByNameAsync(certification.Name, cancellationToken);

        if (existing is not null && existing.ItemId != certification.ItemId)
        {
            context.Result.Fail(new ValidationResult(S["Another certification is already named '{0}'.", certification.Name], [nameof(Certification.Name)]));
        }
    }

    private static void Tidy(Certification certification)
    {
        certification.Name = certification.Name?.Trim();
        certification.Issuer = string.IsNullOrWhiteSpace(certification.Issuer) ? null : certification.Issuer.Trim();
        certification.ReminderDays = (certification.ReminderDays ?? [])
            .Distinct()
            .OrderDescending()
            .ToList();
    }
}
