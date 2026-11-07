using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Chat.Reports;
using CrestApps.OrchardCore.AI.Chat.ViewModels;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Reports.Services;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Chat.Drivers;

/// <summary>
/// The AI profile filter of every report that lists <see cref="AIChatReportFilters.Profile"/> or
/// <see cref="AIChatReportFilters.RequiredProfile"/> in its filter names. It stores the chosen profile under
/// <see cref="AIChatReportFilters.ProfileId"/>.
/// </summary>
public sealed class AIChatProfileReportFilterDisplayDriver : DisplayDriver<ReportFilter>
{
    private readonly IReportManager _reportManager;
    private readonly IAIProfileStore _profileStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIChatProfileReportFilterDisplayDriver"/> class.
    /// </summary>
    /// <param name="reportManager">The report manager, which tells which reports take the filter.</param>
    /// <param name="profileStore">The AI profile store, for the profiles to choose from.</param>
    public AIChatProfileReportFilterDisplayDriver(IReportManager reportManager, IAIProfileStore profileStore)
    {
        _reportManager = reportManager;
        _profileStore = profileStore;
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(ReportFilter filter, BuildEditorContext context)
    {
        var mode = GetMode(filter.ReportName);

        if (mode is null)
        {
            return null;
        }

        return Initialize<AIChatProfileReportFilterViewModel>("AIChatProfileReportFilter_Edit", async model =>
        {
            model.ProfileId = filter.GetOrDefault<string>(AIChatReportFilters.ProfileId);
            model.Required = mode == AIChatReportFilters.RequiredProfile;
            model.Profiles = (await AIChatReportProfiles.GetNamesAsync(_profileStore))
                .OrderBy(profile => profile.Value, StringComparer.CurrentCultureIgnoreCase)
                .Select(profile => new SelectListItem(profile.Value, profile.Key, profile.Key == model.ProfileId))
                .ToList();
        }).Location("Content:2");
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(ReportFilter filter, UpdateEditorContext context)
    {
        if (GetMode(filter.ReportName) is null)
        {
            return null;
        }

        var model = new AIChatProfileReportFilterViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        filter.Set(AIChatReportFilters.ProfileId, model.ProfileId?.Trim());

        return Edit(filter, context);
    }

    private string GetMode(string reportName)
    {
        if (string.IsNullOrEmpty(reportName) || _reportManager.GetReport(reportName) is not IReportFilterMetadata metadata)
        {
            return null;
        }

        if (metadata.FilterNames.Contains(AIChatReportFilters.RequiredProfile, StringComparer.Ordinal))
        {
            return AIChatReportFilters.RequiredProfile;
        }

        return metadata.FilterNames.Contains(AIChatReportFilters.Profile, StringComparer.Ordinal)
            ? AIChatReportFilters.Profile
            : null;
    }
}
