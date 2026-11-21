using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Surveys.ViewModels;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Identity;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Surveys.Drivers;

/// <summary>
/// Renders a version as a row of the versions list, with the number of responses pinned to it.
/// </summary>
internal sealed class SurveyVersionDisplayDriver : DisplayDriver<SurveyVersion>
{
    private readonly ISurveyResponseQueries _responseQueries;
    private readonly SurveyStore _surveyStore;
    private readonly UserManager<IUser> _userManager;
    private readonly IDisplayNameProvider _displayNameProvider;

    public SurveyVersionDisplayDriver(
        ISurveyResponseQueries responseQueries,
        SurveyStore surveyStore,
        UserManager<IUser> userManager,
        IDisplayNameProvider displayNameProvider)
    {
        _responseQueries = responseQueries;
        _surveyStore = surveyStore;
        _userManager = userManager;
        _displayNameProvider = displayNameProvider;
    }

    /// <inheritdoc />
    public override async Task<IDisplayResult> DisplayAsync(SurveyVersion version, BuildDisplayContext context)
    {
        if (context.DisplayType is not OrchardCoreConstants.DisplayType.SummaryAdmin and not OrchardCoreConstants.DisplayType.SummaryAdminGrid)
        {
            return null;
        }

        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
        }

        var (responses, _) = await _responseQueries.ListAsync(new SurveyResponseQuery
        {
            SurveyIds = [version.SurveyId],
            VersionNumber = version.Number,
            Take = 1,
        });
        var survey = await _surveyStore.FindByIdAsync(version.SurveyId);
        var user = string.IsNullOrEmpty(version.PublishedBy) ? null : await _userManager.FindByIdAsync(version.PublishedBy);
        var row = new SurveyVersionRowViewModel
        {
            Version = version,
            Responses = responses,
            PublishedBy = user is null ? null : await _displayNameProvider.GetAsync(user),
            IsCurrent = survey?.PublishedVersionId == version.ItemId,
        };

        return Combine(
            View("SurveyVersion_Fields_SummaryAdmin", row)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("SurveyVersion_Meta_SummaryAdmin", row)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Published:5"),
            View("SurveyVersion_Description_SummaryAdmin", row)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:5"),
            View("SurveyVersion_Buttons_SummaryAdmin", row).Location("Actions:5"));
    }
}
