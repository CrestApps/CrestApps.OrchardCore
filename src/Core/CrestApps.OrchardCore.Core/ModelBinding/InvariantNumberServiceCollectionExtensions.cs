using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Core.ModelBinding;

/// <summary>
/// Turns on ASP.NET Core's own handling of number inputs in every culture.
/// </summary>
public static class InvariantNumberServiceCollectionExtensions
{
    /// <summary>
    /// Turns on <see cref="Microsoft.AspNetCore.Mvc.Rendering.FormInputRenderMode.DetectCultureFromInputType"/>: an
    /// <c>asp-for</c> number input writes its value invariantly and adds a hidden <c>__Invariant</c> field naming it, so
    /// the framework reads the posted value invariantly too, in every culture (a decimal-comma one such as de-DE included).
    /// A hand-written number input (<c>name="..."</c> without <c>asp-for</c>) writes its value with
    /// <see cref="System.Globalization.CultureInfo.InvariantCulture"/> and adds the <c>__Invariant</c> field itself, and so
    /// does a script that posts a number. Call it from the <c>Startup</c> of every module whose forms post a number with a
    /// fraction. Calling it more than once registers it once.
    /// </summary>
    /// <param name="services">The tenant's services.</param>
    /// <returns>The same <paramref name="services"/>.</returns>
    public static IServiceCollection AddInvariantNumberInputs(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Transient<IConfigureOptions<MvcViewOptions>, InvariantNumberMvcViewOptionsSetup>());

        return services;
    }
}
