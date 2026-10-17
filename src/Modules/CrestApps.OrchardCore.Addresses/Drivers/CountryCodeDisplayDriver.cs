using CrestApps.OrchardCore.Addresses.Services;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Addresses.Drivers;

/// <summary>
/// Refuses a Country whose ISO code is missing, malformed or already taken when it is saved in the editor.
/// </summary>
/// <remarks>
/// The admin editor saves content without calling the content handlers' validation, so the editor checks the code
/// itself; <see cref="Handlers.CountryContentHandler"/> covers recipes and the content API.
/// </remarks>
public sealed class CountryCodeDisplayDriver : ContentDisplayDriver
{
    // The name the code's text field posts under.
    private const string CodeFieldPrefix = AddressConstants.CountryPart + "." + AddressConstants.CodeField;

    private readonly CountryCodeValidator _validator;

    public CountryCodeDisplayDriver(CountryCodeValidator validator)
    {
        _validator = validator;
    }

    public override async Task<IDisplayResult> UpdateAsync(ContentItem contentItem, UpdateEditorContext context)
    {
        if (!string.Equals(contentItem.ContentType, AddressConstants.Country, StringComparison.Ordinal))
        {
            return null;
        }

        var posted = new PostedText();

        await context.Updater.TryUpdateModelAsync(posted, CodeFieldPrefix);

        var error = await _validator.GetErrorAsync(posted.Text, contentItem.ContentItemId);

        if (error is not null)
        {
            context.Updater.ModelState.AddModelError(CodeFieldPrefix + "." + nameof(PostedText.Text), error);
        }

        return null;
    }

    private sealed class PostedText
    {
        public string Text { get; set; }
    }
}
