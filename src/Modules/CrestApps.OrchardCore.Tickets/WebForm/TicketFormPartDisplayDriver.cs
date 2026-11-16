using CrestApps.Core.Services;
using CrestApps.OrchardCore.Tickets.Categories;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Portal;
using CrestApps.OrchardCore.Tickets.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Title.Models;

namespace CrestApps.OrchardCore.Tickets.WebForm;

/// <summary>
/// Shows the Ticket Form widget's form, or what happened to the last post from it, and edits which type and desk it
/// opens tickets in.
/// </summary>
internal sealed class TicketFormPartDisplayDriver : ContentPartDisplayDriver<TicketFormPart>
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly ICatalog<TicketDesk> _desks;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IEnumerable<ITicketFormChallenge> _challenges;
    private readonly IContentManager _contentManager;
    private readonly IContentItemDisplayManager _contentItemDisplayManager;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly ITicketCategoryService _categories;

    internal readonly IStringLocalizer S;

    public TicketFormPartDisplayDriver(
        IContentDefinitionManager contentDefinitionManager,
        ICatalog<TicketDesk> desks,
        IHttpContextAccessor httpContextAccessor,
        IEnumerable<ITicketFormChallenge> challenges,
        IContentManager contentManager,
        IContentItemDisplayManager contentItemDisplayManager,
        IUpdateModelAccessor updateModelAccessor,
        ITicketCategoryService categories,
        IStringLocalizer<TicketFormPartDisplayDriver> stringLocalizer)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _desks = desks;
        _httpContextAccessor = httpContextAccessor;
        _challenges = challenges;
        _contentManager = contentManager;
        _contentItemDisplayManager = contentItemDisplayManager;
        _updateModelAccessor = updateModelAccessor;
        _categories = categories;
        S = stringLocalizer;
    }

    public override IDisplayResult Display(TicketFormPart part, BuildPartDisplayContext context)
        => Initialize<TicketFormViewModel>("TicketFormPart", async model =>
        {
            var request = _httpContextAccessor.HttpContext?.Request;
            var forThisForm = string.Equals(request?.Query[TicketFormController.FormQueryName], part.ContentItem.ContentItemId, StringComparison.Ordinal);

            model.WidgetId = part.ContentItem.ContentItemId;
            model.Ready = !string.IsNullOrEmpty(part.TicketType) && !string.IsNullOrEmpty(part.DeskId);
            model.SignedIn = _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true;
            model.ShowChallenge = _challenges.FirstOrDefault()?.IsConfigured == true;
            model.SentNumber = forThisForm ? request.Query[TicketFormController.SentQueryName].ToString() : null;
            model.SuccessMessage = !string.IsNullOrWhiteSpace(part.SuccessMessage)
                ? part.SuccessMessage
                : model.SentNumber is { Length: > 1 }
                    ? S["Thank you. We received your request {0} and will get back to you.", model.SentNumber].Value
                    : S["Thank you. We received your request and will get back to you."].Value;
            model.Error = forThisForm ? TicketFormController.Describe(request.Query[TicketFormController.ErrorQueryName].ToString(), S) : null;

            if (model.Ready && string.IsNullOrEmpty(model.SentNumber)
                && await _contentDefinitionManager.GetTypeDefinitionAsync(part.TicketType) is { } type
                && type.Parts.FirstOrDefault(item => item.PartDefinition.Name == TicketsConstants.PartName) is { } ticketPart)
            {
                var settings = ticketPart.GetSettings<TicketPartSettings>();

                // The type's own form as customers fill it in: the subject (its title) and the fields customers fill in.
                TicketCustomerEditor.Begin(_httpContextAccessor.HttpContext);
                model.Editor = await _contentItemDisplayManager.BuildEditorAsync(await _contentManager.NewAsync(part.TicketType), _updateModelAccessor.ModelUpdater, isNew: true);
                model.AsksForSubject = !type.Parts.Any(item => item.PartDefinition.Name == nameof(TitlePart));
                model.Categories = TicketCategoryForm.Items(await _categories.ListAsync(part.TicketType, customersOnly: true), settings.CategoryLeavesOnly);
                model.CategoryRequired = settings.CategoryRequired && model.Categories.Count > 0;
            }
        }).Location("Detail", "Content:5");

    public override IDisplayResult Edit(TicketFormPart part, BuildPartEditorContext context)
        => Initialize<TicketFormPartViewModel>("TicketFormPart_Edit", async model =>
        {
            model.TicketType = part.TicketType;
            model.DeskId = part.DeskId;
            model.SuccessMessage = part.SuccessMessage;
            model.ChallengeConfigured = _challenges.FirstOrDefault()?.IsConfigured == true;
            model.Types = [.. (await _contentDefinitionManager.ListTypeDefinitionsAsync())
                .Where(type => type.Parts.Any(p => p.PartDefinition.Name == TicketsConstants.PartName))
                .Select(type => new SelectListItem(type.DisplayName, type.Name, type.Name == part.TicketType))];
            model.Desks = [.. (await _desks.GetAllAsync()).OrderBy(desk => desk.Name, StringComparer.CurrentCultureIgnoreCase).Select(desk => new SelectListItem(desk.Name, desk.ItemId, desk.ItemId == part.DeskId))];
        });

    public override async Task<IDisplayResult> UpdateAsync(TicketFormPart part, UpdatePartEditorContext context)
    {
        var model = new TicketFormPartViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.TicketType))
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.TicketType), S["Choose the ticket type the form opens."]);
        }

        if (string.IsNullOrWhiteSpace(model.DeskId))
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.DeskId), S["Choose the desk the tickets go to."]);
        }

        part.TicketType = model.TicketType;
        part.DeskId = model.DeskId;
        part.SuccessMessage = string.IsNullOrWhiteSpace(model.SuccessMessage) ? null : model.SuccessMessage.Trim();

        return Edit(part, context);
    }
}
