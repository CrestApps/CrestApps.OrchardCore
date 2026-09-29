using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

internal sealed class LeadPartDisplayDriver : ContentPartDisplayDriver<LeadPart>
{
    private readonly INamedCatalog<LeadStatus> _statuses;
    private readonly IContentManager _contentManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="LeadPartDisplayDriver"/> class.
    /// </summary>
    /// <param name="statuses">The lead status catalog.</param>
    /// <param name="contentManager">The content manager.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public LeadPartDisplayDriver(
        INamedCatalog<LeadStatus> statuses,
        IContentManager contentManager,
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor,
        IStringLocalizer<LeadPartDisplayDriver> stringLocalizer)
    {
        _statuses = statuses;
        _contentManager = contentManager;
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
        S = stringLocalizer;
    }

    public override IDisplayResult Display(LeadPart part, BuildPartDisplayContext context)
    {
        return Combine(
            Initialize<LeadPartViewModel>("LeadPart_SummaryAdmin", async model => await PopulateDisplayAsync(model, part))
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:4"),
            Initialize<LeadPartViewModel>("LeadPart_Converted", async model => await PopulateDisplayAsync(model, part))
                .Location(OrchardCoreConstants.DisplayType.DetailAdmin, "Content:0")
                .RenderWhen(() => Task.FromResult(part.IsConverted)));
    }

    public override IDisplayResult Edit(LeadPart part, BuildPartEditorContext context)
    {
        if (part.IsConverted)
        {
            return Initialize<LeadPartViewModel>("LeadPart_Converted", async model => await PopulateDisplayAsync(model, part))
                .Location("Parts:0");
        }

        return Initialize<LeadPartViewModel>(GetEditorShapeType(context), async model =>
        {
            model.StatusId = part.StatusId;
            model.Company = part.Company;
            model.Source = part.Source;
            model.ListName = part.ListName;
            model.Rating = part.Rating;
            model.OwnerId = part.OwnerId;
            model.Statuses = await GetStatusOptionsAsync(part.StatusId);
            model.Ratings = LeadRatings.All
                .Select(rating => new SelectListItem(S[rating], rating, rating == part.Rating))
                .ToList();
        }).Location("Parts:1");
    }

    public override async Task<IDisplayResult> UpdateAsync(LeadPart part, UpdatePartEditorContext context)
    {
        if (part.IsConverted)
        {
            // A converted lead is the record of what happened before it became a contact. Only someone allowed to
            // correct that record may save it, and even then the lead fields stay as they were.
            var user = _httpContextAccessor.HttpContext?.User;

            if (user is null || !await _authorizationService.AuthorizeAsync(user, OmnichannelConstants.Permissions.EditConvertedLead))
            {
                context.Updater.ModelState.AddModelError(Prefix, S["This lead was converted, so it can no longer be edited. Edit the contact it became instead."]);
            }

            return Edit(part, context);
        }

        var model = new LeadPartViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var statuses = await _statuses.GetAllAsync();
        var status = statuses.FirstOrDefault(entry => entry.ItemId == model.StatusId);

        if (!string.IsNullOrEmpty(model.StatusId) && (status is null || status.IsConverted))
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.StatusId), S["Choose one of the listed statuses. A lead takes the converted status only when it is converted."]);
        }
        else
        {
            part.StatusId = model.StatusId;
        }

        part.Company = Trim(model.Company);
        part.Source = Trim(model.Source);
        part.ListName = Trim(model.ListName);
        part.Rating = LeadRatings.Normalize(model.Rating);
        part.OwnerId = Trim(model.OwnerId);

        return Edit(part, context);
    }

    private async Task PopulateDisplayAsync(LeadPartViewModel model, LeadPart part)
    {
        model.Part = part;
        model.ContentItem = part.ContentItem;
        model.IsConverted = part.IsConverted;
        model.StatusId = part.StatusId;
        model.Rating = part.Rating;
        model.Source = part.Source;
        model.ListName = part.ListName;
        model.Company = part.Company;

        var status = string.IsNullOrEmpty(part.StatusId)
            ? null
            : (await _statuses.GetAllAsync()).FirstOrDefault(entry => entry.ItemId == part.StatusId);

        if (status is not null)
        {
            model.Statuses = [new SelectListItem(status.Name, status.ItemId, true)];
        }

        if (part.IsConverted && !string.IsNullOrEmpty(part.ConvertedContactItemId))
        {
            model.ConvertedContact = await _contentManager.GetAsync(part.ConvertedContactItemId, VersionOptions.Latest);
        }
    }

    private async Task<IList<SelectListItem>> GetStatusOptionsAsync(string selectedId)
    {
        return (await _statuses.GetAllAsync())
            .Where(status => !status.IsConverted)
            .OrderBy(status => status.Order)
            .ThenBy(status => status.Name, StringComparer.OrdinalIgnoreCase)
            .Select(status => new SelectListItem(status.Name, status.ItemId, status.ItemId == selectedId))
            .ToList();
    }

    private static string Trim(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
