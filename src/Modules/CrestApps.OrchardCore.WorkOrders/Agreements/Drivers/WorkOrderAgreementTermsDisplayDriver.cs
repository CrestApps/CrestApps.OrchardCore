using CrestApps.Core;
using CrestApps.OrchardCore.Agreements.Models;
using CrestApps.OrchardCore.WorkOrders.Agreements.ViewModels;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Models;
using CrestApps.OrchardCore.WorkOrders.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.WorkOrders.Agreements.Drivers;

/// <summary>
/// The visits of an agreement on its editor: the recurring visit schedules, where they are, who to contact, the assets
/// covered and how visits are paid for, kept as the agreement's Work Orders terms. The rules are the terms validator's,
/// which the agreement's own rules call.
/// </summary>
internal sealed class WorkOrderAgreementTermsDisplayDriver : DisplayDriver<Agreement>
{
    private readonly WorkOrderSettingManager<WorkOrderType> _types;
    private readonly WorkOrderLookups _lookups;
    private readonly IHttpContextAccessor _httpContextAccessor;

    internal readonly IStringLocalizer S;

    public WorkOrderAgreementTermsDisplayDriver(
        WorkOrderSettingManager<WorkOrderType> types,
        WorkOrderLookups lookups,
        IHttpContextAccessor httpContextAccessor,
        IStringLocalizer<WorkOrderAgreementTermsDisplayDriver> stringLocalizer)
    {
        _types = types;
        _lookups = lookups;
        _httpContextAccessor = httpContextAccessor;
        S = stringLocalizer;
    }

    /// <summary>
    /// The repeats a schedule offers, as rules and words.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Presets(IStringLocalizer S)
        => new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["FREQ=WEEKLY"] = S["Every week"],
            ["FREQ=WEEKLY;INTERVAL=2"] = S["Every 2 weeks"],
            ["FREQ=MONTHLY"] = S["Every month"],
            ["FREQ=MONTHLY;INTERVAL=3"] = S["Every 3 months"],
            ["FREQ=MONTHLY;INTERVAL=6"] = S["Every 6 months"],
            ["FREQ=YEARLY"] = S["Every year"],
        };

    /// <inheritdoc/>
    public override IDisplayResult Edit(Agreement agreement, BuildEditorContext context)
        => Initialize<WorkOrderAgreementTermsViewModel>("WorkOrderAgreementTerms_Edit", async model =>
        {
            var terms = agreement.TryGet<WorkOrderAgreementTerms>(out var kept) && kept is not null ? kept : new WorkOrderAgreementTerms();
            var presets = Presets(S);
            var address = terms.ServiceAddress;

            model.AgreementId = context.IsNew ? null : agreement.ItemId;
            model.CustomerId = terms.CustomerId;
            model.ContactName = terms.Contact?.Name;
            model.ContactPhone = terms.Contact?.Phone;
            model.ContactEmail = terms.Contact?.Email;
            model.AddressLine1 = address?.AddressLine1;
            model.City = address?.City;
            model.Region = address?.Region;
            model.PostalCode = address?.PostalCode;
            model.Country = address?.Country;
            model.AccessNotes = terms.AccessNotes;
            model.AssetIds = [.. terms.AssetIds];
            model.Pricing = terms.Pricing;
            model.PricePerVisit = terms.PricePerVisit;
            model.Schedules = [.. terms.Schedules.Select(schedule => new ScheduleEditModel
            {
                Id = schedule.Id,
                Name = schedule.Name,
                TypeId = schedule.TypeId,
                Rule = presets.ContainsKey(schedule.Rule ?? string.Empty) ? schedule.Rule : string.Empty,
                CustomRule = presets.ContainsKey(schedule.Rule ?? string.Empty) ? null : schedule.Rule,
                LeadDays = schedule.LeadDays,
                AssetIds = [.. schedule.AssetIds],
            })];

            // One empty row to add a schedule.
            model.Schedules.Add(new ScheduleEditModel { Rule = "FREQ=MONTHLY;INTERVAL=6" });

            var cancellationToken = _httpContextAccessor.HttpContext?.RequestAborted ?? default;
            var customers = _httpContextAccessor.HttpContext?.RequestServices.GetService<Integrations.WorkOrderCustomers>();

            if (customers is not null)
            {
                var names = await customers.NamesAsync(terms.CustomerId, null, null, pickers: true, cancellationToken);

                model.CanChooseCustomer = names.ContainsKey("Customer");
                model.CustomerName = names.GetValueOrDefault("Customer");
            }

            model.TypeOptions = [.. (await _types.GetAllAsync()).OrderBy(type => type.Name, StringComparer.CurrentCultureIgnoreCase).Select(type => new SelectListItem(type.Name, type.ItemId))];
            model.AssetOptions = [.. (await _lookups.AssetsAsync(cancellationToken)).Select(option => new SelectListItem(option.Text, option.Value, terms.AssetIds.Contains(option.Value)))];
            model.RuleOptions = [.. presets.Select(preset => new SelectListItem(preset.Value, preset.Key)), new SelectListItem(S["Custom rule"], string.Empty)];
        }).Location("Content:5");

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(Agreement agreement, UpdateEditorContext context)
    {
        var model = new WorkOrderAgreementTermsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        // The terms' rules, which the service agreement AI tools apply too. The account's location fills an address that
        // isn't typed when the customers can be read.
        var httpContext = _httpContextAccessor.HttpContext;

        await WorkOrderAgreementTermsRules.ApplyAsync(
            agreement,
            model,
            httpContext?.RequestServices.GetService<Integrations.WorkOrderCustomers>(),
            httpContext?.RequestAborted ?? default);

        return Edit(agreement, context);
    }
}
