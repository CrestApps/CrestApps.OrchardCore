using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace CrestApps.OrchardCore.Surveys.TagHelpers;

/// <summary>
/// The <c>&lt;survey-layout&gt;</c> tag helper: shows the header and the side menu of a survey around the page it
/// holds, so every page of one survey looks and navigates the same.
/// </summary>
/// <example>
/// <code>
/// &lt;survey-layout survey="Model.Survey"&gt;...&lt;/survey-layout&gt;
/// </code>
/// </example>
[HtmlTargetElement("survey-layout")]
public sealed class SurveyLayoutTagHelper : TagHelper
{
    private readonly IHtmlHelper _htmlHelper;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyLayoutTagHelper"/> class.
    /// </summary>
    /// <param name="htmlHelper">The HTML helper that renders the layout partial.</param>
    public SurveyLayoutTagHelper(IHtmlHelper htmlHelper)
    {
        _htmlHelper = htmlHelper;
    }

    /// <summary>
    /// Gets or sets the survey the page belongs to.
    /// </summary>
    public Survey Survey { get; set; }

    /// <summary>
    /// Gets or sets the view context.
    /// </summary>
    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; }

    /// <inheritdoc/>
    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);

        var body = await output.GetChildContentAsync();

        if (Survey is null)
        {
            output.TagName = null;
            output.Content.SetHtmlContent(body);

            return;
        }

        (_htmlHelper as IViewContextAware)?.Contextualize(ViewContext);

        var layout = await _htmlHelper.PartialAsync("_SurveyLayout", new SurveyLayoutViewModel
        {
            Survey = Survey,
            Body = body,
        });

        output.TagName = null;
        output.Content.SetHtmlContent(layout);
    }
}
