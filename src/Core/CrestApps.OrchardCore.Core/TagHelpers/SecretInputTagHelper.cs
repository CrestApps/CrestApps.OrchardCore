using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Core.TagHelpers;

/// <summary>
/// Renders an input that holds a secret, such as an API key: <c>&lt;input asp-for="ApiKey" secret-input
/// secret-saved="@Model.HasApiKey" /&gt;</c>. It is a password field the browser never fills in or offers to remember
/// (<c>autocomplete="new-password"</c> and the password managers' ignore attributes), it is always blank, since a stored
/// secret is never sent back to the browser, and it says so in its placeholder when a value is saved and none was given.
/// Add <c>@addTagHelper *, CrestApps.OrchardCore.Core</c> to the module's <c>_ViewImports.cshtml</c>.
/// </summary>
[HtmlTargetElement("input", Attributes = SecretInputAttributeName)]
public sealed class SecretInputTagHelper : TagHelper
{
    private const string SecretInputAttributeName = "secret-input";
    private const string SecretSavedAttributeName = "secret-saved";

    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SecretInputTagHelper"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SecretInputTagHelper(IStringLocalizer<SecretInputTagHelper> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets or sets a value indicating whether a secret is already saved, which the placeholder then says.
    /// </summary>
    [HtmlAttributeName(SecretSavedAttributeName)]
    public bool Saved { get; set; }

    /// <summary>
    /// Runs after the input tag helper, so the type and value it set from <c>asp-for</c> are replaced.
    /// </summary>
    public override int Order => 1000;

    /// <inheritdoc/>
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);

        output.Attributes.RemoveAll(SecretInputAttributeName);
        output.Attributes.RemoveAll("value");
        output.Attributes.SetAttribute("type", "password");
        output.Attributes.SetAttribute("autocomplete", "new-password");
        output.Attributes.SetAttribute("spellcheck", "false");
        output.Attributes.SetAttribute("data-1p-ignore", "true");
        output.Attributes.SetAttribute("data-lpignore", "true");
        output.Attributes.SetAttribute("data-bwignore", "true");

        if (Saved && !output.Attributes.ContainsName("placeholder"))
        {
            output.Attributes.SetAttribute("placeholder", S["A value is saved. Leave blank to keep it, or enter a new one to replace it."].Value);
        }
    }
}
