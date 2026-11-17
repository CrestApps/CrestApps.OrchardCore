using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Core.ModelBinding;

/// <summary>
/// Registers the handling that lets a number posted from a form bind the same in every culture.
/// </summary>
public static class InvariantNumberServiceCollectionExtensions
{
    /// <summary>
    /// Makes the tenant's forms bind and render numbers the same in every culture: a posted <see cref="decimal"/>,
    /// <see cref="double"/> or <see cref="float"/> action parameter, or a property of a CrestApps model, is read with the
    /// invariant culture first and the request culture second, never with a group separator, and an <c>asp-for</c> number
    /// input writes its value invariantly. Call it from the <c>Startup</c> of every module whose forms post a number with a
    /// fraction or bind one to a floating-point action parameter. Calling it more than once registers it once.
    /// </summary>
    /// <param name="services">The tenant's services.</param>
    /// <returns>The same <paramref name="services"/>.</returns>
    public static IServiceCollection AddInvariantNumberInputs(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Transient<IConfigureOptions<MvcOptions>, InvariantNumberMvcOptionsSetup>());
        services.TryAddEnumerable(ServiceDescriptor.Transient<IConfigureOptions<MvcViewOptions>, InvariantNumberMvcViewOptionsSetup>());

        return services;
    }
}
