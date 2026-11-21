using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Core.ModelBinding;

/// <summary>
/// Turns on the framework's own culture handling for number, range and date inputs.
/// </summary>
/// <remarks>
/// By default the input tag helper writes an <c>asp-for</c> number input's value in the request culture, so in de-DE
/// 1500.75 is written as <c>value="1500,75"</c>, which the browser drops, and the next save clears the field. With
/// <see cref="FormInputRenderMode.DetectCultureFromInputType"/> the tag helper writes the value invariantly and adds a
/// hidden <c>__Invariant</c> field naming the input, and the form value provider then reads that input invariantly too.
/// </remarks>
internal sealed class InvariantNumberMvcViewOptionsSetup : IConfigureOptions<MvcViewOptions>
{
    public void Configure(MvcViewOptions options)
        => options.HtmlHelperOptions.FormInputRenderMode = FormInputRenderMode.DetectCultureFromInputType;
}
