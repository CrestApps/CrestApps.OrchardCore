using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Core.ModelBinding;

/// <summary>
/// Puts <see cref="InvariantNumberModelBinderProvider"/> ahead of the framework's floating-point binder provider.
/// </summary>
internal sealed class InvariantNumberMvcOptionsSetup : IConfigureOptions<MvcOptions>
{
    public void Configure(MvcOptions options)
    {
        if (options.ModelBinderProviders.OfType<InvariantNumberModelBinderProvider>().Any())
        {
            return;
        }

        options.ModelBinderProviders.Insert(0, new InvariantNumberModelBinderProvider());
    }
}
