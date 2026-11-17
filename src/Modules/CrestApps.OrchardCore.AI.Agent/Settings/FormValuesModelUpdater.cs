using System.Globalization;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using OrchardCore.DisplayManagement.ModelBinding;

namespace CrestApps.OrchardCore.AI.Agent.Settings;

/// <summary>
/// Binds editor view models from a set of form values, the way a settings page post does: the controller model
/// binding the settings page's updater uses, with the same <c>[BindNever]</c> and validation attributes, and a model
/// state the display drivers add their errors to. The values come from the tool instead of the request body.
/// </summary>
internal sealed class FormValuesModelUpdater : IUpdateModel
{
    private readonly BindingController _controller;
    private readonly IValueProvider _valueProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="FormValuesModelUpdater"/> class.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="httpContext">The request of the caller.</param>
    /// <param name="values">The form values, keyed by the full field name such as <c>ISite.AdminSettings.DisplayMenuFilter</c>.</param>
    public FormValuesModelUpdater(IServiceProvider services, HttpContext httpContext, IDictionary<string, StringValues> values)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(httpContext);

        // The settings page binds through its controller; this controller is never routed to and only binds.
        _controller = new BindingController
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext,
                RouteData = httpContext.GetRouteData() ?? new RouteData(),
                ActionDescriptor = new ControllerActionDescriptor(),
            },
            MetadataProvider = services.GetRequiredService<IModelMetadataProvider>(),
            ModelBinderFactory = services.GetRequiredService<IModelBinderFactory>(),
            ObjectValidator = services.GetRequiredService<IObjectModelValidator>(),
        };

        // JSON numbers and dates are culture invariant, so they are parsed that way.
        _valueProvider = new FormValueProvider(
            BindingSource.Form,
            new FormCollection(new Dictionary<string, StringValues>(values ?? new Dictionary<string, StringValues>(), StringComparer.OrdinalIgnoreCase)),
            CultureInfo.InvariantCulture);
    }

    /// <inheritdoc/>
    public ModelStateDictionary ModelState
        => _controller.ModelState;

    /// <inheritdoc/>
    public Task<bool> TryUpdateModelAsync<TModel>(TModel model)
        where TModel : class
        => TryUpdateModelAsync(model, string.Empty);

    /// <inheritdoc/>
    public Task<bool> TryUpdateModelAsync<TModel>(TModel model, string prefix)
        where TModel : class
        => _controller.TryUpdateModelAsync(model, prefix ?? string.Empty, _valueProvider);

    /// <inheritdoc/>
    public Task<bool> TryUpdateModelAsync<TModel>(TModel model, string prefix, params Expression<Func<TModel, object>>[] includeExpressions)
        where TModel : class
        => _controller.TryUpdateModelAsync(model, prefix ?? string.Empty, _valueProvider, includeExpressions);

    /// <inheritdoc/>
    public bool TryValidateModel(object model)
        => _controller.TryValidateModel(model);

    /// <inheritdoc/>
    public bool TryValidateModel(object model, string prefix)
        => _controller.TryValidateModel(model, prefix);

    [NonController]
    private sealed class BindingController : ControllerBase
    {
    }
}
