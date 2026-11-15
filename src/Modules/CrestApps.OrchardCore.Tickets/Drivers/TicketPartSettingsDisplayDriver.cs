using CrestApps.Core.Services;
using CrestApps.OrchardCore.ServiceLevels.Services;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentTypes.Editors;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Tickets.Drivers;

/// <summary>
/// Edits what a new ticket of a ticket type starts with, on the type's Ticket part: the desk when none is given, the
/// team and priority, the service-level policy used before the desk's, and the skills an assignee needs (with Skills on).
/// </summary>
internal sealed class TicketPartSettingsDisplayDriver : ContentTypePartDefinitionDisplayDriver<TicketPart>
{
    private readonly ICatalog<TicketDesk> _desks;
    private readonly ICatalog<TicketPriority> _priorities;
    private readonly IServiceLevelPolicyManager _policies;
    private readonly IServiceProvider _services;

    public TicketPartSettingsDisplayDriver(
        ICatalog<TicketDesk> desks,
        ICatalog<TicketPriority> priorities,
        IServiceLevelPolicyManager policies,
        IServiceProvider services)
    {
        _desks = desks;
        _priorities = priorities;
        _policies = policies;
        _services = services;
    }

    public override IDisplayResult Edit(ContentTypePartDefinition contentTypePartDefinition, BuildEditorContext context)
        => Initialize<TicketPartSettingsViewModel>("TicketPartSettings_Edit", async model =>
        {
            var settings = contentTypePartDefinition.GetSettings<TicketPartSettings>();

            model.DefaultDeskId = settings.DefaultDeskId;
            model.DefaultTeamId = settings.DefaultTeamId;
            model.DefaultPriorityId = settings.DefaultPriorityId;
            model.ServiceLevelPolicyId = settings.ServiceLevelPolicyId;
            model.RequiredSkillIds = [.. settings.RequiredSkills.Select(skill => skill.SkillId)];
            model.CustomerSubmittable = settings.CustomerSubmittable;
            model.PortalDescription = settings.PortalDescription;
            model.Desks = [.. (await _desks.GetAllAsync()).OrderBy(desk => desk.Name, StringComparer.CurrentCultureIgnoreCase).Select(desk => new SelectListItem(desk.Name, desk.ItemId, desk.ItemId == settings.DefaultDeskId))];
            model.Priorities = [.. (await _priorities.GetAllAsync()).OrderBy(priority => priority.Rank).Select(priority => new SelectListItem(priority.Name, priority.ItemId, priority.ItemId == settings.DefaultPriorityId))];
            model.Policies = [.. (await _policies.ListByScopeAsync(TicketsConstants.ServiceLevelScope)).Select(policy => new SelectListItem(policy.Name, policy.ItemId, policy.ItemId == settings.ServiceLevelPolicyId))];

            if (_services.GetService<ISkillManager>() is { } skills)
            {
                model.Skills = [.. (await skills.GetEnabledAsync()).OrderBy(skill => skill.Name, StringComparer.CurrentCultureIgnoreCase).Select(skill => new SelectListItem(skill.Name, skill.ItemId, model.RequiredSkillIds.Contains(skill.ItemId)))];
            }
        }).Location("Content:5");

    public override async Task<IDisplayResult> UpdateAsync(ContentTypePartDefinition contentTypePartDefinition, UpdateTypePartEditorContext context)
    {
        var model = new TicketPartSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var current = contentTypePartDefinition.GetSettings<TicketPartSettings>();

        // A skill's minimum proficiency is kept when it stays required; a newly required skill needs the lowest level.
        var proficiency = current.RequiredSkills.Where(skill => !string.IsNullOrEmpty(skill.SkillId)).GroupBy(skill => skill.SkillId, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First().MinProficiency, StringComparer.Ordinal);
        var skillIds = _services.GetService<ISkillManager>() is null
            ? [.. current.RequiredSkills.Select(skill => skill.SkillId)]
            : (model.RequiredSkillIds ?? []).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList();

        context.Builder.WithSettings(new TicketPartSettings
        {
            DefaultDeskId = Blank(model.DefaultDeskId),
            DefaultTeamId = Blank(model.DefaultTeamId),
            DefaultPriorityId = Blank(model.DefaultPriorityId),
            ServiceLevelPolicyId = Blank(model.ServiceLevelPolicyId),
            RequiredSkills = [.. skillIds.Select(id => new SkillRequirement { SkillId = id, MinProficiency = proficiency.GetValueOrDefault(id, 1) })],
            CustomerSubmittable = model.CustomerSubmittable,
            PortalDescription = Blank(model.PortalDescription),
        });

        return Edit(contentTypePartDefinition, context);
    }

    private static string Blank(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
