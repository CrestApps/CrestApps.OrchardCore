using System.Globalization;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Moq;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Core.ModelBinding;

/// <summary>
/// The updater an admin editor's display driver binds from, binding the posted fields the way a running site does: under
/// the request's culture, with MVC's own value providers, model binder factory and validator, and the services a module's
/// <c>Startup</c> registers. What the driver then stores, or refuses, is its own doing.
/// </summary>
internal sealed class FormPostUpdateModel : IUpdateModel
{
    private readonly CultureInfo _culture;
    private readonly ActionContext _actionContext;
    private readonly IServiceProvider _mvc;

    /// <summary>
    /// Initializes a new instance of the <see cref="FormPostUpdateModel"/> class.
    /// </summary>
    /// <param name="startup">The module's startup, whose services are registered next to MVC's; <see langword="null"/> for MVC alone.</param>
    /// <param name="culture">The request's culture.</param>
    /// <param name="form">The posted fields, with their prefixes.</param>
    public FormPostUpdateModel(IStartup startup, string culture, IDictionary<string, StringValues> form)
    {
        _culture = CultureInfo.GetCultureInfo(culture);

        var services = new ServiceCollection().AddLogging().AddMvcCore().Services;
        startup?.ConfigureServices(services);
        _mvc = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = _mvc };
        httpContext.Request.Method = HttpMethods.Post;
        httpContext.Request.ContentType = "application/x-www-form-urlencoded";
        httpContext.Request.Form = new FormCollection(new Dictionary<string, StringValues>(form));

        _actionContext = new ActionContext(httpContext, new RouteData(), new ControllerActionDescriptor());
    }

    public ModelStateDictionary ModelState => _actionContext.ModelState;

    /// <summary>
    /// Builds the context a driver's <c>UpdateAsync</c> runs in.
    /// </summary>
    /// <param name="isNew">Whether the editor is creating the record.</param>
    /// <returns>The update context.</returns>
    public UpdateEditorContext CreateContext(bool isNew = false)
        => new(Mock.Of<IShape>(), groupId: string.Empty, isNew, htmlFieldPrefix: string.Empty, Mock.Of<IShapeFactory>(), layout: null, this);

    /// <summary>
    /// Builds the context a content part driver's <c>UpdateAsync</c> runs in.
    /// </summary>
    /// <param name="partName">The part's name.</param>
    /// <returns>The update context.</returns>
    public UpdatePartEditorContext CreatePartContext(string partName)
        => new(new ContentTypePartDefinition(partName, new ContentPartDefinition(partName), []), CreateContext());

    public Task<bool> TryUpdateModelAsync<TModel>(TModel model)
        where TModel : class
        => TryUpdateModelAsync(model, string.Empty);

    public async Task<bool> TryUpdateModelAsync<TModel>(TModel model, string prefix)
        where TModel : class
    {
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUICulture = CultureInfo.CurrentUICulture;

        CultureInfo.CurrentCulture = _culture;
        CultureInfo.CurrentUICulture = _culture;

        try
        {
            var factories = _mvc.GetRequiredService<IOptions<MvcOptions>>().Value.ValueProviderFactories;
            var valueProvider = await CompositeValueProvider.CreateAsync(_actionContext, factories);

            // A controller binds through MVC's own metadata provider, binder factory and validator, from the request's services.
            var controller = new BindingController { ControllerContext = new ControllerContext(_actionContext) };

            return await controller.TryUpdateModelAsync(model, prefix ?? string.Empty, valueProvider);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUICulture;
        }
    }

    public Task<bool> TryUpdateModelAsync<TModel>(TModel model, string prefix, params Expression<Func<TModel, object>>[] includeExpressions)
        where TModel : class
        => TryUpdateModelAsync(model, prefix);

    public bool TryValidateModel(object model)
        => true;

    public bool TryValidateModel(object model, string prefix)
        => true;

    private sealed class BindingController : Controller
    {
    }
}
