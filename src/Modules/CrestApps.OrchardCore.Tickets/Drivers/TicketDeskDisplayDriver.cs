using CrestApps.OrchardCore.ServiceLevels.Services;
using CrestApps.OrchardCore.Teams.Services;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Tickets.Drivers;

/// <summary>
/// The row and the editor of a desk.
/// </summary>
internal sealed class TicketDeskDisplayDriver : TicketSettingsDisplayDriverBase<TicketDesk>
{
    private readonly IServiceLevelPolicyManager _policies;
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly IEnumerable<ITeamMemberAvailabilitySource> _availabilitySources;
    private readonly IStringLocalizerFactory _localizerFactory;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TicketDeskDisplayDriver"/> class.
    /// </summary>
    /// <param name="policies">The service-level policies.</param>
    /// <param name="contentDefinitionManager">The content definition manager, for the ticket types.</param>
    /// <param name="availabilitySources">The team availability sources a desk may honor.</param>
    /// <param name="localizerFactory">The localizer factory, for the sources' names.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TicketDeskDisplayDriver(
        IServiceLevelPolicyManager policies,
        IContentDefinitionManager contentDefinitionManager,
        IEnumerable<ITeamMemberAvailabilitySource> availabilitySources,
        IStringLocalizerFactory localizerFactory,
        IStringLocalizer<TicketDeskDisplayDriver> stringLocalizer)
    {
        _availabilitySources = availabilitySources;
        _localizerFactory = localizerFactory;
        _policies = policies;
        _contentDefinitionManager = contentDefinitionManager;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override string Column => "Audience";

    /// <inheritdoc/>
    public override async Task<IDisplayResult> EditAsync(TicketDesk desk, BuildEditorContext context)
    {
        var model = new TicketDeskViewModel();

        model.Name = desk.Name;
        model.Description = desk.Description;
        model.Audience = desk.Audience;
        model.DefaultTeamId = desk.DefaultTeamId;
        model.HoursScheduleId = desk.HoursScheduleId;
        model.ServiceLevelPolicyId = desk.ServiceLevelPolicyId;
        model.TicketTypes = [.. desk.TicketTypes];
        model.ReopenWindowDays = desk.ReopenWindowDays;
        model.AutoCloseDays = desk.AutoCloseDays;
        model.DeleteProtectedInformationDays = desk.DeleteProtectedInformationDays;
        model.AssignmentStrategy = desk.AssignmentStrategy;
        model.NotifyRequester = desk.NotifyRequester;
        model.NotifyAssignee = desk.NotifyAssignee;
        model.RatingRequestDelayHours = desk.RatingRequestDelayHours;
        model.AvailabilitySourceOptions = [.. _availabilitySources.Select(source => new SelectListItem(
            source.DisplayName is null ? source.Name : _localizerFactory.Localize(source.DisplayName).Value,
            source.Name,
            desk.AvailabilitySources?.Contains(source.Name, StringComparer.OrdinalIgnoreCase) ?? source.HonoredByDefault))];
        model.Policies = [.. (await _policies.ListByScopeAsync(TicketsConstants.ServiceLevelScope)).Select(policy => new SelectListItem(policy.Name, policy.ItemId, policy.ItemId == desk.ServiceLevelPolicyId))];
        model.TicketTypeOptions = [.. (await _contentDefinitionManager.ListTypeDefinitionsAsync())
            .Where(type => type.Parts.Any(part => part.PartDefinition.Name == TicketsConstants.PartName))
            .Select(type => new SelectListItem(type.DisplayName, type.Name, desk.TicketTypes.Contains(type.Name, StringComparer.OrdinalIgnoreCase)))];

        // Every card edits a copy of the same model under the same prefix, so UpdateAsync still binds it once.
        void Populate(TicketDeskViewModel target)
        {
            target.Name = model.Name;
            target.Description = model.Description;
            target.Audience = model.Audience;
            target.DefaultTeamId = model.DefaultTeamId;
            target.HoursScheduleId = model.HoursScheduleId;
            target.ServiceLevelPolicyId = model.ServiceLevelPolicyId;
            target.TicketTypes = model.TicketTypes;
            target.ReopenWindowDays = model.ReopenWindowDays;
            target.AutoCloseDays = model.AutoCloseDays;
            target.DeleteProtectedInformationDays = model.DeleteProtectedInformationDays;
            target.AssignmentStrategy = model.AssignmentStrategy;
            target.NotifyRequester = model.NotifyRequester;
            target.NotifyAssignee = model.NotifyAssignee;
            target.Policies = model.Policies;
            target.TicketTypeOptions = model.TicketTypeOptions;
            target.AvailabilitySources = model.AvailabilitySources;
            target.RatingRequestDelayHours = model.RatingRequestDelayHours;
            target.AvailabilitySourceOptions = model.AvailabilitySourceOptions;
        }

        return Combine(
            Initialize<TicketDeskViewModel>("TicketDeskFields_Edit", Populate)
                .Location(l => l.Zone("Content", "1").Card(S["Desk"], "10")),
            Initialize<TicketDeskViewModel>("TicketDeskRouting_Edit", Populate)
                .Location(l => l.Zone("Content", "1").Card(S["Routing"], "20")),
            Initialize<TicketDeskViewModel>("TicketDeskHours_Edit", Populate)
                .Location(l => l.Zone("Content", "1").Card(S["Business Hours and Service Level"], "30")),
            Initialize<TicketDeskViewModel>("TicketDeskSolved_Edit", Populate)
                .Location(l => l.Zone("Content", "1").Card(S["Solved Tickets"], "40")),
            Initialize<TicketDeskViewModel>("TicketDeskPeople_Edit", Populate)
                .Location(l => l.Zone("Content", "1").Card(S["Requesters and Assignees"], "50")));
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(TicketDesk desk, UpdateEditorContext context)
    {
        var model = new TicketDeskViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        desk.Name = model.Name?.Trim();
        desk.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
        desk.Audience = model.Audience;
        desk.DefaultTeamId = string.IsNullOrWhiteSpace(model.DefaultTeamId) ? null : model.DefaultTeamId;
        desk.HoursScheduleId = string.IsNullOrWhiteSpace(model.HoursScheduleId) ? null : model.HoursScheduleId;
        desk.ServiceLevelPolicyId = string.IsNullOrWhiteSpace(model.ServiceLevelPolicyId) ? null : model.ServiceLevelPolicyId;
        desk.TicketTypes = [.. (model.TicketTypes ?? []).Where(type => !string.IsNullOrWhiteSpace(type)).Distinct(StringComparer.OrdinalIgnoreCase)];
        desk.ReopenWindowDays = model.ReopenWindowDays;
        desk.AutoCloseDays = model.AutoCloseDays;
        desk.DeleteProtectedInformationDays = model.DeleteProtectedInformationDays;
        desk.AssignmentStrategy = string.IsNullOrWhiteSpace(model.AssignmentStrategy) ? null : model.AssignmentStrategy;
        desk.NotifyRequester = model.NotifyRequester;
        desk.NotifyAssignee = model.NotifyAssignee;
        desk.RatingRequestDelayHours = Math.Max(0, model.RatingRequestDelayHours);

        // Saved only when there are sources to choose from, so a desk keeps following the defaults until someone chooses.
        if (_availabilitySources.Any())
        {
            var known = _availabilitySources.Select(source => source.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            desk.AvailabilitySources = [.. (model.AvailabilitySources ?? []).Where(known.Contains).Distinct(StringComparer.OrdinalIgnoreCase)];
        }

        return await EditAsync(desk, context);
    }
}
