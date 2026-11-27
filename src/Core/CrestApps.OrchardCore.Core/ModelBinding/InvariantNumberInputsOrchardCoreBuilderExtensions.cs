using CrestApps.OrchardCore.Core.ModelBinding;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Makes the numbers in forms post, bind and render the same in every culture, in every tenant.
/// </summary>
public static class InvariantNumberInputsOrchardCoreBuilderExtensions
{
    /// <summary>
    /// Makes every tenant read and write form numbers invariantly, so a decimal-comma culture such as de-DE no longer
    /// binds "12.50" as 1250 or shows an empty number box. It uses ASP.NET Core's own <c>__Invariant</c> form field:
    /// <list type="bullet">
    /// <item>it turns on <see cref="Microsoft.AspNetCore.Mvc.Rendering.FormInputRenderMode.DetectCultureFromInputType"/>,
    /// so an <c>asp-for</c> number, range, date or time input writes its value invariantly and adds the
    /// <c>__Invariant</c> field that names it;</item>
    /// <item>it writes a short script on every page that names each number input of a form in an <c>__Invariant</c>
    /// field as the form is sent, so a hand-written input (<c>name="..."</c> without <c>asp-for</c>) and the rows a script
    /// adds or renumbers are read invariantly too.</item>
    /// </list>
    /// A hand-written number input still writes its value with <see cref="System.Globalization.CultureInfo.InvariantCulture"/>.
    /// Call it once, from the host's <c>AddOrchardCms</c>. Calling it more than once registers it once.
    /// </summary>
    /// <param name="builder">The Orchard Core builder.</param>
    /// <returns>The same <paramref name="builder"/>.</returns>
    public static OrchardCoreBuilder AddInvariantNumberInputs(this OrchardCoreBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.ConfigureServices(services =>
        {
            services.TryAddEnumerable(ServiceDescriptor.Transient<IConfigureOptions<MvcViewOptions>, InvariantNumberInputsSetup>());
            services.TryAddEnumerable(ServiceDescriptor.Transient<IConfigureOptions<MvcOptions>, InvariantNumberInputsSetup>());
        });
    }
}
