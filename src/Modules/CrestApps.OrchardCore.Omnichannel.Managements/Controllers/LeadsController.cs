using System.Security.Claims;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Admin;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.DisplayManagement.Notify;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Controllers;

/// <summary>
/// Provides the screen that converts a lead into a contact.
/// </summary>
[Admin]
public sealed class LeadsController : Controller
{
    private static readonly ActivityStatus[] _finishedStatuses =
    [
        ActivityStatus.Completed,
        ActivityStatus.Cancelled,
        ActivityStatus.Purged,
    ];

    private readonly IContentManager _contentManager;
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly ILeadConversionService _conversionService;
    private readonly LeadMatchFinder _matchFinder;
    private readonly ISession _session;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="LeadsController"/> class.
    /// </summary>
    public LeadsController(
        IContentManager contentManager,
        IContentDefinitionManager contentDefinitionManager,
        IAuthorizationService authorizationService,
        ILeadConversionService conversionService,
        LeadMatchFinder matchFinder,
        ISession session,
        INotifier notifier,
        IHtmlLocalizer<LeadsController> htmlLocalizer)
    {
        _contentManager = contentManager;
        _contentDefinitionManager = contentDefinitionManager;
        _authorizationService = authorizationService;
        _conversionService = conversionService;
        _matchFinder = matchFinder;
        _session = session;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Shows the conversion screen of a lead.
    /// </summary>
    /// <param name="contentItemId">The lead content item id.</param>
    [Admin("omnichannel/leads/{contentItemId}/convert", "OmnichannelLeadConvert")]
    public async Task<IActionResult> Convert(string contentItemId)
    {
        var lead = await _contentManager.GetAsync(contentItemId, VersionOptions.Latest);

        if (lead is null || !lead.TryGet<LeadPart>(out var leadPart))
        {
            return NotFound();
        }

        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ConvertLead, lead))
        {
            return Forbid();
        }

        if (leadPart.IsConverted)
        {
            await _notifier.InformationAsync(H["This lead was already converted."]);

            return RedirectToLead(lead);
        }

        var model = new ConvertLeadViewModel();

        await PopulateAsync(model, lead, leadPart, isPost: false);

        return View(model);
    }

    /// <summary>
    /// Converts a lead.
    /// </summary>
    /// <param name="contentItemId">The lead content item id.</param>
    /// <param name="model">The conversion choices.</param>
    [HttpPost]
    [ActionName(nameof(Convert))]
    [Admin("omnichannel/leads/{contentItemId}/convert", "OmnichannelLeadConvert")]
    public async Task<IActionResult> ConvertPost(string contentItemId, ConvertLeadViewModel model)
    {
        var lead = await _contentManager.GetAsync(contentItemId, VersionOptions.Latest);

        if (lead is null || !lead.TryGet<LeadPart>(out var leadPart))
        {
            return NotFound();
        }

        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ConvertLead, lead))
        {
            return Forbid();
        }

        var result = await _conversionService.ConvertAsync(new LeadConversionRequest
        {
            LeadContentItemId = lead.ContentItemId,
            ExistingContactItemId = string.IsNullOrWhiteSpace(model.ExistingContactItemId) ? null : model.ExistingContactItemId,
            ContactContentType = string.IsNullOrWhiteSpace(model.ContactContentType) ? null : model.ContactContentType,
            AccountMode = model.AccountMode,
            AccountName = model.AccountName,
            ExistingAccountItemId = string.IsNullOrWhiteSpace(model.ExistingAccountItemId) ? null : model.ExistingAccountItemId,
            CreateOpportunity = model.CreateOpportunity,
            OpportunityContentType = string.IsNullOrWhiteSpace(model.OpportunityContentType) ? null : model.OpportunityContentType,
            OpportunityName = model.OpportunityName,
            OpportunityAmount = model.OpportunityAmount,
            OpportunityCloseDate = model.OpportunityCloseDate,
            OpenActivities = model.OpenActivities,
            UserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            UserName = User.Identity?.Name,
        });

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            await PopulateAsync(model, lead, leadPart, isPost: true);

            return View(model);
        }

        if (result.AlreadyConverted)
        {
            await _notifier.InformationAsync(H["This lead was already converted."]);
        }
        else
        {
            await _notifier.SuccessAsync(H["{0} was converted into the contact {1}.", lead.DisplayText, result.Contact?.DisplayText]);
        }

        return result.Contact is null
            ? RedirectToLead(lead)
            : RedirectToAction("Edit", "Admin", new { area = "OrchardCore.Contents", contentItemId = result.Contact.ContentItemId });
    }

    private async Task PopulateAsync(ConvertLeadViewModel model, ContentItem lead, LeadPart leadPart, bool isPost)
    {
        var definitions = await _contentDefinitionManager.ListTypeDefinitionsAsync();
        var leadDefinition = definitions.FirstOrDefault(definition => definition.Name == lead.ContentType);
        var settings = leadDefinition?.Parts
            .FirstOrDefault(part => part.PartDefinition?.Name == OmnichannelConstants.ContentParts.Lead)?
            .GetSettings<LeadPartSettings>() ?? new LeadPartSettings();

        model.Lead = lead;
        model.Company = leadPart.Company;
        model.MatchingContacts = (await _matchFinder.FindContactsAsync(lead)).ToList();
        model.MatchingAccounts = (await _matchFinder.FindAccountsAsync(leadPart.Company)).ToList();
        model.HasAccountTypes = definitions.Any(OmnichannelRecordKinds.IsAccount);

        if (!isPost)
        {
            model.ContactContentType = settings.TargetContactContentType;
            model.OpportunityContentType = settings.DefaultOpportunityContentType;
            model.ExistingContactItemId = model.MatchingContacts.Count == 1 ? model.MatchingContacts[0].ContentItemId : null;
            model.AccountName = leadPart.Company;

            if (!model.HasAccountTypes)
            {
                model.AccountMode = LeadConversionAccountMode.None;
            }
            else if (model.MatchingAccounts.Count == 1)
            {
                model.AccountMode = LeadConversionAccountMode.UseExisting;
                model.ExistingAccountItemId = model.MatchingAccounts[0].ContentItemId;
            }
            else
            {
                model.AccountMode = string.IsNullOrWhiteSpace(leadPart.Company)
                    ? LeadConversionAccountMode.None
                    : LeadConversionAccountMode.CreateNew;
            }
        }

        model.ContactContentTypes = definitions
            .Where(OmnichannelRecordKinds.IsContact)
            .OrderBy(definition => definition.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(definition => new SelectListItem(definition.DisplayName, definition.Name, definition.Name == model.ContactContentType))
            .ToList();

        model.OpportunityContentTypes = definitions
            .Where(OmnichannelRecordKinds.IsOpportunity)
            .OrderBy(definition => definition.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(definition => new SelectListItem(definition.DisplayName, definition.Name, definition.Name == model.OpportunityContentType))
            .ToList();

        var activities = await _session.QueryIndex<OmnichannelActivityIndex>(
                index => index.ContactContentItemId == lead.ContentItemId,
                collection: OmnichannelConstants.CollectionName)
            .ListAsync();

        foreach (var activity in activities)
        {
            if (_finishedStatuses.Contains(activity.Status))
            {
                model.FinishedActivityCount++;
            }
            else
            {
                model.OpenActivityCount++;
            }
        }
    }

    private RedirectToActionResult RedirectToLead(ContentItem lead)
        => RedirectToAction("Edit", "Admin", new { area = "OrchardCore.Contents", contentItemId = lead.ContentItemId });
}
