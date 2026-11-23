using System.Security.Claims;
using CrestApps.OrchardCore.MailingLists.Services;
using CrestApps.OrchardCore.PublicForms;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.MailingLists.SignupWidget;

/// <summary>
/// Shows the sign-up widget's form, or what happened to the last post from it when the visitor has no script, and edits
/// which lists it offers and what it says.
/// </summary>
internal sealed class MailingListSignupPartDisplayDriver : ContentPartDisplayDriver<MailingListSignupPart>
{
    private readonly IMailingListManager _lists;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IEnumerable<IPublicFormChallenge> _challenges;

    internal readonly IStringLocalizer S;

    public MailingListSignupPartDisplayDriver(
        IMailingListManager lists,
        IHttpContextAccessor httpContextAccessor,
        IEnumerable<IPublicFormChallenge> challenges,
        IStringLocalizer<MailingListSignupPartDisplayDriver> stringLocalizer)
    {
        _lists = lists;
        _httpContextAccessor = httpContextAccessor;
        _challenges = challenges;
        S = stringLocalizer;
    }

    public override IDisplayResult Display(MailingListSignupPart part, BuildPartDisplayContext context)
        => Initialize<MailingListSignupViewModel>("MailingListSignupPart", async model =>
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var request = httpContext?.Request;
            var forThisForm = string.Equals(request?.Query[MailingListSignupController.FormQueryName], part.ContentItem.ContentItemId, StringComparison.Ordinal);

            model.WidgetId = part.ContentItem.ContentItemId;
            model.Heading = part.Heading;
            model.Text = part.Text;
            model.ButtonLabel = string.IsNullOrWhiteSpace(part.ButtonLabel) ? S["Sign up"] : part.ButtonLabel;
            model.AskForName = part.AskForName;
            model.Email = httpContext?.User?.Identity?.IsAuthenticated == true ? httpContext.User.FindFirstValue(ClaimTypes.Email) : null;

            foreach (var id in part.ListIds ?? [])
            {
                if (!string.IsNullOrEmpty(id) && await _lists.FindByIdAsync(id) is { IsEnabled: true } list)
                {
                    model.Lists.Add(new MailingListSignupOption
                    {
                        Id = list.ItemId,
                        Name = list.DisplayText,
                        Description = list.Description,
                        ConsentText = list.ConsentText,
                    });
                }
            }

            if (forThisForm)
            {
                var code = request.Query[MailingListSignupController.ResultQueryName].ToString();

                model.Succeeded = string.Equals(code, MailingListSignupController.SucceededCode, StringComparison.Ordinal);
                model.Message = model.Succeeded
                    ? MailingListSignupController.SuccessMessage(part, S)
                    : MailingListSignupController.Describe(code, S);
            }
        }).Location("Detail", "Content:5");

    public override IDisplayResult Edit(MailingListSignupPart part, BuildPartEditorContext context)
        => Initialize<MailingListSignupPartViewModel>("MailingListSignupPart_Edit", async model =>
        {
            var selected = new HashSet<string>(part.ListIds ?? [], StringComparer.Ordinal);

            model.ListIds = part.ListIds ?? [];
            model.Heading = part.Heading;
            model.Text = part.Text;
            model.ButtonLabel = part.ButtonLabel;
            model.AskForName = part.AskForName;
            model.SuccessMessage = part.SuccessMessage;
            model.ChallengeConfigured = _challenges.Any(challenge => challenge.IsConfigured);
            model.AvailableLists = [.. (await _lists.GetAllAsync())
                .OrderBy(list => list.DisplayText, StringComparer.CurrentCultureIgnoreCase)
                .Select(list => new SelectListItem(list.IsEnabled ? list.DisplayText : S["{0} (off)", list.DisplayText], list.ItemId, selected.Contains(list.ItemId)))];
        });

    public override async Task<IDisplayResult> UpdateAsync(MailingListSignupPart part, UpdatePartEditorContext context)
    {
        var model = new MailingListSignupPartViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var listIds = (model.ListIds ?? []).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToArray();

        if (listIds.Length == 0)
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.ListIds), S["Choose at least one mailing list the form offers."]);
        }

        part.ListIds = listIds;
        part.Heading = Clean(model.Heading);
        part.Text = Clean(model.Text);
        part.ButtonLabel = Clean(model.ButtonLabel);
        part.AskForName = model.AskForName;
        part.SuccessMessage = Clean(model.SuccessMessage);

        return Edit(part, context);
    }

    private static string Clean(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
