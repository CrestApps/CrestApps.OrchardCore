using CrestApps.Core;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

/// <summary>
/// Adds the lead filters to the inventory load editor. They show when the chosen contact type is a lead type and are
/// stored on the load, where the loader applies them.
/// </summary>
internal sealed class LeadBatchFilterDisplayDriver : DisplayDriver<OmnichannelActivityBatch>
{
    private readonly INamedCatalog<LeadStatus> _statuses;
    private readonly OmnichannelContentTypeProvider _contentTypeProvider;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="LeadBatchFilterDisplayDriver"/> class.
    /// </summary>
    /// <param name="statuses">The lead status catalog.</param>
    /// <param name="contentTypeProvider">The CRM content type provider.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public LeadBatchFilterDisplayDriver(
        INamedCatalog<LeadStatus> statuses,
        OmnichannelContentTypeProvider contentTypeProvider,
        IStringLocalizer<LeadBatchFilterDisplayDriver> stringLocalizer)
    {
        _statuses = statuses;
        _contentTypeProvider = contentTypeProvider;
        S = stringLocalizer;
    }

    /// <summary>
    /// Keeps the filter fields apart from the load's own fields, such as its <c>Source</c>.
    /// </summary>
    protected override void BuildPrefix(OmnichannelActivityBatch model, string htmlFieldPrefix)
    {
        Prefix = string.IsNullOrEmpty(htmlFieldPrefix) ? nameof(LeadBatchFilter) : $"{htmlFieldPrefix}.{nameof(LeadBatchFilter)}";
    }

    public override IDisplayResult Edit(OmnichannelActivityBatch batch, BuildEditorContext context)
    {
        return Initialize<LeadBatchFilterViewModel>("LeadBatchFilter_Edit", async model =>
        {
            var filter = batch.TryGet<LeadBatchFilter>(out var stored) ? stored : new LeadBatchFilter();
            var selected = (filter.StatusIds ?? []).ToHashSet(StringComparer.Ordinal);
            var ratings = (filter.Ratings ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase);

            model.LeadContentTypes = (await _contentTypeProvider.GetLeadContentTypesAsync()).ToArray();
            model.IncludeClosedLeads = filter.IncludeClosedLeads;
            model.ListName = filter.ListName;
            model.Source = filter.Source;
            model.OwnerId = filter.OwnerId;
            model.SkipLeadsThatAreContacts = filter.SkipLeadsThatAreContacts;
            model.Statuses = (await _statuses.GetAllAsync())
                .Where(status => !status.IsConverted)
                .OrderBy(status => status.Order)
                .Select(status => new SelectListItem(status.Name, status.ItemId, selected.Contains(status.ItemId)))
                .ToList();
            model.Ratings = LeadRatings.All
                .Select(rating => new SelectListItem(S[rating], rating, ratings.Contains(rating)))
                .ToList();
        }).Location("Content:2");
    }

    public override async Task<IDisplayResult> UpdateAsync(OmnichannelActivityBatch batch, UpdateEditorContext context)
    {
        var model = new LeadBatchFilterViewModel();

        if (await context.Updater.TryUpdateModelAsync(model, Prefix) && model.Rendered)
        {
            var known = (await _statuses.GetAllAsync()).Select(status => status.ItemId).ToHashSet(StringComparer.Ordinal);

            batch.Put(new LeadBatchFilter
            {
                StatusIds = (model.StatusIds ?? []).Where(known.Contains).Distinct(StringComparer.Ordinal).ToArray(),
                IncludeClosedLeads = model.IncludeClosedLeads,
                ListName = Trim(model.ListName),
                Source = Trim(model.Source),
                OwnerId = Trim(model.OwnerId),
                Ratings = (model.SelectedRatings ?? []).Select(LeadRatings.Normalize).Where(rating => rating is not null).Distinct().ToArray(),
                SkipLeadsThatAreContacts = model.SkipLeadsThatAreContacts,
            });
        }

        return Edit(batch, context);
    }

    private static string Trim(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
