using CrestApps.OrchardCore.Core.TagHelpers;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Localization;
using Moq;

namespace CrestApps.OrchardCore.Tests.Core;

/// <summary>
/// SEC-FE4 d: secrets were typed into type="text" fields, shown while typed and kept in form history.
/// </summary>
public sealed class SecretInputTagHelperTests
{
    [Fact]
    public void Process_RendersABlankPasswordFieldNobodyFillsIn()
    {
        var output = Run(saved: false, new TagHelperAttribute("type", "text"), new TagHelperAttribute("value", "sk_live_123"), new TagHelperAttribute("secret-input"));

        Assert.Equal("password", output.Attributes["type"].Value);
        Assert.Equal("new-password", output.Attributes["autocomplete"].Value);
        Assert.False(output.Attributes.ContainsName("value"));
        Assert.False(output.Attributes.ContainsName("secret-input"));
        Assert.True(output.Attributes.ContainsName("data-1p-ignore"));
        Assert.True(output.Attributes.ContainsName("data-lpignore"));
        Assert.True(output.Attributes.ContainsName("data-bwignore"));
        Assert.False(output.Attributes.ContainsName("placeholder"));
    }

    [Fact]
    public void Process_WhenAValueIsSaved_SaysSo_UnlessThePageGaveAPlaceholder()
    {
        var saved = Run(saved: true);
        var custom = Run(saved: true, new TagHelperAttribute("placeholder", "A key is stored."));

        Assert.StartsWith("A value is saved.", saved.Attributes["placeholder"].Value.ToString());
        Assert.Equal("A key is stored.", custom.Attributes["placeholder"].Value);
    }

    private static TagHelperOutput Run(bool saved, params TagHelperAttribute[] attributes)
    {
        var localizer = new Mock<IStringLocalizer<SecretInputTagHelper>>();
        localizer.Setup(l => l[It.IsAny<string>()]).Returns((string name) => new LocalizedString(name, name));

        var context = new TagHelperContext(new TagHelperAttributeList(attributes), new Dictionary<object, object>(), "id");
        var output = new TagHelperOutput("input", new TagHelperAttributeList(attributes), (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));

        new SecretInputTagHelper(localizer.Object) { Saved = saved }.Process(context, output);

        return output;
    }
}
