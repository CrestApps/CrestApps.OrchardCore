using System.Globalization;
using System.Linq.Expressions;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using OrchardCore.DisplayManagement.ModelBinding;

namespace CrestApps.OrchardCore.AI.Agent.Editors;

/// <summary>
/// Binds editor view models from a set of form values, the way an admin form post does: the controller model binding
/// the page's updater uses, with the same <c>[BindNever]</c> and validation attributes, and a model state the display
/// drivers add their errors to. The values come from the tool instead of the request body.
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

        // The admin page binds through its controller; this controller is never routed to and only binds.
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

    /// <summary>
    /// Gets the request the editor is built and posted with: the caller's request, or, when the tool runs where there
    /// is none (a hub call on some transports), a request made for the caller, as some drivers read the request.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="caller">The signed-in caller.</param>
    /// <returns>The request.</returns>
    public static HttpContext GetHttpContext(IServiceProvider services, ClaimsPrincipal caller)
    {
        ArgumentNullException.ThrowIfNull(services);

        var httpContext = services.GetService<IHttpContextAccessor>()?.HttpContext;

        if (httpContext is not null)
        {
            return httpContext;
        }

        return new DefaultHttpContext
        {
            RequestServices = services,
            User = caller,
        };
    }

    [NonController]
    private sealed class BindingController : ControllerBase
    {
    }
}
