using CrestApps.Core;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Agreements.Models;
using CrestApps.OrchardCore.ServiceLevels.Services;
using CrestApps.OrchardCore.Tickets.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Tickets.Agreements;

/// <summary>
/// The ticket terms of an agreement on its editor: whether it covers tickets, on which desks and ticket types, and the
/// service level of those tickets. The rules are the terms validator's, which the agreement's own rules call.
/// </summary>
internal sealed class TicketAgreementTermsDisplayDriver : DisplayDriver<Agreement>
{
    private readonly ICatalog<TicketDesk> _desks;
    private readonly IServiceLevelPolicyManager _policies;
    private readonly IContentDefinitionManager _contentDefinitionManager;

    internal readonly IStringLocalizer S;

    public TicketAgreementTermsDisplayDriver(
        ICatalog<TicketDesk> desks,
        IServiceLevelPolicyManager policies,
        IContentDefinitionManager contentDefinitionManager,
        IStringLocalizer<TicketAgreementTermsDisplayDriver> stringLocalizer)
    {
        _desks = desks;
        _policies = policies;
        _contentDefinitionManager = contentDefinitionManager;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(Agreement agreement, BuildEditorContext context)
        => Initialize<TicketAgreementTermsViewModel>("TicketAgreementTerms_Edit", async model =>
        {
            var terms = agreement.TryGet<TicketAgreementTerms>(out var kept) ? kept : null;

            model.CoversTickets = terms is not null;
            model.DeskIds = [.. terms?.DeskIds ?? []];
            model.TicketTypes = [.. terms?.TicketTypes ?? []];
            model.ServiceLevelPolicyId = terms?.ServiceLevelPolicyId;
            model.Desks = [.. (await _desks.GetAllAsync())
                .OrderBy(desk => desk.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(desk => new SelectListItem(desk.Name, desk.ItemId, model.DeskIds.Contains(desk.ItemId)))];
            model.Types = [.. (await _contentDefinitionManager.ListTypeDefinitionsAsync())
                .Where(type => type.Parts.Any(part => part.PartDefinition.Name == TicketsConstants.PartName))
                .Select(type => new SelectListItem(type.DisplayName, type.Name, model.TicketTypes.Contains(type.Name, StringComparer.OrdinalIgnoreCase)))];
            model.Policies = [.. (await _policies.ListByScopeAsync(TicketsConstants.ServiceLevelScope))
                .Select(policy => new SelectListItem(policy.Name, policy.ItemId, policy.ItemId == model.ServiceLevelPolicyId))];
        }).Location(l => l.Zone("Content", "4").Card(S["Tickets"], "4"));

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(Agreement agreement, UpdateEditorContext context)
    {
        var model = new TicketAgreementTermsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (model.CoversTickets)
        {
            agreement.Put(new TicketAgreementTerms
            {
                DeskIds = [.. (model.DeskIds ?? []).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal)],
                TicketTypes = [.. (model.TicketTypes ?? []).Where(type => !string.IsNullOrEmpty(type)).Distinct(StringComparer.OrdinalIgnoreCase)],
                ServiceLevelPolicyId = string.IsNullOrWhiteSpace(model.ServiceLevelPolicyId) ? null : model.ServiceLevelPolicyId,
            });
        }
        else if (agreement.Properties?.ContainsKey(nameof(TicketAgreementTerms)) == true)
        {
            agreement.Properties.Remove(nameof(TicketAgreementTerms));
        }

        return Edit(agreement, context);
    }
}
