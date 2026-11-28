using CrestApps.Core;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Agreements.Models;
using CrestApps.OrchardCore.WorkOrders.Agreements.ViewModels;
using CrestApps.OrchardCore.WorkOrders.Integrations;
using CrestApps.OrchardCore.WorkOrders.Models;

namespace CrestApps.OrchardCore.WorkOrders.Agreements;

/// <summary>
/// How the field-service terms posted for an agreement are applied to it, in one place for the agreement editor's
/// Work Orders part (<see cref="Drivers.WorkOrderAgreementTermsDisplayDriver"/>) and the service agreement AI tools. What
/// the terms may hold is checked by the terms validator, which the agreement's own rules call.
/// </summary>
internal static class WorkOrderAgreementTermsRules
{
    /// <summary>
    /// Applies the posted terms to an agreement: the texts are trimmed and an empty one is none, the address is kept only
    /// when its street, city or postal code is given, the assets are kept once each, the price is kept only when visits
    /// are billed one by one, schedule rows with neither a type nor a name are dropped and the others keep their ID or get
    /// a new one, a schedule's preset repeat is used unless the custom rule is chosen, with visits and no address the
    /// visits are at the account's location, and an agreement without field-service terms (a support contract, say)
    /// keeps none.
    /// </summary>
    /// <param name="agreement">The agreement, with its account already applied.</param>
    /// <param name="model">The terms as posted.</param>
    /// <param name="customers">The customers, accounts and contacts, for the account's location; <see langword="null"/>
    /// when they can't be read, and then an address that isn't typed stays empty.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that completes when the terms are applied.</returns>
    public static async Task ApplyAsync(Agreement agreement, WorkOrderAgreementTermsViewModel model, WorkOrderCustomers customers, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agreement);

        model ??= new WorkOrderAgreementTermsViewModel();

        var current = agreement.TryGet<WorkOrderAgreementTerms>(out var kept) && kept is not null ? kept : null;
        var address = Clean(model.AddressLine1) is null && Clean(model.City) is null && Clean(model.PostalCode) is null
            ? null
            : new Address { AddressLine1 = Clean(model.AddressLine1), City = Clean(model.City), Region = Clean(model.Region), PostalCode = Clean(model.PostalCode), Country = Clean(model.Country) };
        var terms = new WorkOrderAgreementTerms
        {
            CustomerId = Clean(model.CustomerId),
            Contact = new WorkOrderContact { Name = Clean(model.ContactName), Phone = Clean(model.ContactPhone), Email = Clean(model.ContactEmail) },
            ServiceAddress = address,
            AccessNotes = Clean(model.AccessNotes),
            AssetIds = [.. (model.AssetIds ?? []).Where(assetId => !string.IsNullOrEmpty(assetId)).Distinct(StringComparer.Ordinal)],
            Pricing = model.Pricing,
            PricePerVisit = model.Pricing == AgreementPricing.PerVisit ? model.PricePerVisit : 0,
            Schedules = [.. (model.Schedules ?? []).Where(row => !string.IsNullOrEmpty(row.TypeId) || !string.IsNullOrWhiteSpace(row.Name)).Select(row => new AgreementSchedule
            {
                Id = string.IsNullOrEmpty(row.Id) ? UniqueId.GenerateId() : row.Id,
                Name = Clean(row.Name),
                TypeId = row.TypeId,
                Rule = string.IsNullOrEmpty(row.Rule) ? Clean(row.CustomRule) : row.Rule,
                LeadDays = row.LeadDays,
                AssetIds = [.. (row.AssetIds ?? []).Where(assetId => !string.IsNullOrEmpty(assetId)).Distinct(StringComparer.Ordinal)],
            })],
        };

        // With visits and no address typed, the visits are at the account's location.
        if (terms.Schedules.Count > 0 && terms.ServiceAddress is null && customers is not null)
        {
            terms.ServiceAddress = await customers.AccountAddressAsync(agreement.AccountId, cancellationToken);
        }

        // An agreement without field-service terms (a support contract, say) keeps none.
        if (terms.HasAny || current is not null)
        {
            agreement.Put(terms);
        }
    }

    private static string Clean(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
