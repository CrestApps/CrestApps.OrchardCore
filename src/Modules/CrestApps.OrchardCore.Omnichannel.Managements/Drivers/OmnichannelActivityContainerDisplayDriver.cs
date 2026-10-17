using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using CrestApps.OrchardCore.Users;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

internal sealed class OmnichannelActivityContainerDisplayDriver : DisplayDriver<OmnichannelActivityContainer>
{
    /// <summary>
    /// The display group of the rows of the scheduled activities of a contact or an account.
    /// </summary>
    public const string ScheduledActivityGroup = "ScheduledActivity";

    /// <summary>
    /// The display group of the rows of the completed activities of a contact or an account.
    /// </summary>
    public const string CompletedActivityGroup = "CompletedActivity";

    private readonly ActivityHandlerDescriber _handlerDescriber;
    private readonly IDisplayNameProvider _displayNameProvider;

    public OmnichannelActivityContainerDisplayDriver(
        ActivityHandlerDescriber handlerDescriber,
        IDisplayNameProvider displayNameProvider)
    {
        _handlerDescriber = handlerDescriber;
        _displayNameProvider = displayNameProvider;
    }

    public override Task<IDisplayResult> DisplayAsync(OmnichannelActivityContainer container, BuildDisplayContext context)
    {
        // The container is only displayed as a row of the admin lists of activities.
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        // The row's classes and attributes are rendered on its row element, in both admin list layouts.
        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = container.SubjectContentTypeDefinition?.DisplayName?.ToLowerInvariant() ?? string.Empty;
        }

        // The shapes without a group are the rows of the lists that are not about one contact; the others are the rows
        // of the scheduled and the completed activities of a contact or an account. The Grid layout renders the Subject,
        // Details and Handler zones in the columns of the same names.
        return CombineAsync(
            View("OmnichannelActivityContainer_Fields_SummaryAdmin", container)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Subject:1"),
            View("OmnichannelActivityContainer_Description_SummaryAdmin", container)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Subject:5")
                .RenderWhen(() => Task.FromResult(!string.IsNullOrEmpty(container.Activity.Instructions))),
            View("OmnichannelActivityContainer_Buttons_SummaryAdmin", container)
                .Location("Actions:5"),
            View("OmnichannelActivityContainer_DefaultMeta_SummaryAdmin", container)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Details:5"),
            Initialize<ActivityHandlerViewModel>("OmnichannelActivityContainer_Handler_SummaryAdmin", async model =>
                await _handlerDescriber.DescribeHandlerAsync(model, container.Activity, await GetUserNameAsync(container, container.Activity.AssignedToId)))
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:6")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Handler:6"),
            View("OmnichannelActivityContainerScheduledActivity_Fields_SummaryAdmin", container)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Subject:1")
                .OnGroup(ScheduledActivityGroup),
            View("OmnichannelActivityContainerScheduledActivity_Description_SummaryAdmin", container)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Subject:5")
                .OnGroup(ScheduledActivityGroup)
                .RenderWhen(() => Task.FromResult(!string.IsNullOrEmpty(container.Activity.Instructions))),
            View("OmnichannelActivityContainerScheduledActivity_Buttons_SummaryAdmin", container)
                .Location("Actions:5")
                .OnGroup(ScheduledActivityGroup),
            View("OmnichannelActivityContainerScheduledActivity_DefaultMeta_SummaryAdmin", container)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Details:5")
                .OnGroup(ScheduledActivityGroup),
            Initialize<ActivityHandlerViewModel>("OmnichannelActivityContainer_Handler_SummaryAdmin", async model =>
                await _handlerDescriber.DescribeHandlerAsync(model, container.Activity, await GetUserNameAsync(container, container.Activity.AssignedToId)))
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:6")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Handler:6")
                .OnGroup(ScheduledActivityGroup),
            View("OmnichannelActivityContainerCompletedActivity_Fields_SummaryAdmin", container)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Subject:1")
                .OnGroup(CompletedActivityGroup),
            View("OmnichannelActivityContainerCompletedActivity_Buttons_SummaryAdmin", container)
                .Location("Actions:5")
                .OnGroup(CompletedActivityGroup),
            View("OmnichannelActivityContainerCompletedActivity_DefaultMeta_SummaryAdmin", container)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Details:5")
                .OnGroup(CompletedActivityGroup),
            Initialize<ActivityHandlerViewModel>("OmnichannelActivityContainerCompletedActivity_DispositionedBy_SummaryAdmin", async model =>
                await _handlerDescriber.DescribeDispositionAsync(model, container.Activity, await GetUserNameAsync(container, container.Activity.CompletedById)))
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:6")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Handler:6")
                .OnGroup(CompletedActivityGroup),
            View("OmnichannelActivityContainerCompletedActivity_Description_SummaryAdmin", container)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Subject:5")
                .OnGroup(CompletedActivityGroup)
                .RenderWhen(() => Task.FromResult(!string.IsNullOrEmpty(container.Activity.Notes)))
        );
    }

    // The lists load the row's user in one query for the whole page (the assignee of open work, the completing user of
    // completed work), so the name is only read from the container when it is the user asked about.
    private async Task<string> GetUserNameAsync(OmnichannelActivityContainer container, string userId)
    {
        if (container.User is null ||
            string.IsNullOrEmpty(userId) ||
            !string.Equals(container.User.UserId, userId, StringComparison.Ordinal))
        {
            return null;
        }

        return await _displayNameProvider.GetAsync(container.User);
    }
}
