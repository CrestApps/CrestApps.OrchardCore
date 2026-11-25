using System.Globalization;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Core.ModelBinding;

/// <summary>
/// Binds an action's parameters from a form post the way MVC does in a running site: the request's culture is set first,
/// as the request localization middleware does, then MVC's own value providers, model binder factory and
/// <see cref="ParameterBinder"/> bind each parameter, with the services a module's <c>Startup</c> registers.
/// </summary>
public static class FormPostBinder
{
    /// <summary>
    /// The result of binding a post: the bound arguments, in parameter order, and the context the action runs in.
    /// </summary>
    /// <param name="Arguments">The bound arguments, in parameter order.</param>
    /// <param name="ControllerContext">The controller context, which holds the model state.</param>
    public sealed record Result(object[] Arguments, ControllerContext ControllerContext)
    {
        public ModelStateDictionary ModelState => ControllerContext.ModelState;
    }

    /// <summary>
    /// Binds a form post to <paramref name="method"/> under <paramref name="culture"/>.
    /// </summary>
    /// <param name="startup">The module's startup, whose services are registered next to MVC's; <see langword="null"/> for MVC alone.</param>
    /// <param name="method">The action.</param>
    /// <param name="culture">The request's culture.</param>
    /// <param name="form">The posted fields.</param>
    /// <param name="routeValues">The route values, if any.</param>
    /// <param name="controller">A controller to bind into, so its <see cref="ControllerBase.ModelState"/> is the bound one.</param>
    /// <param name="configureRequest">Changes the request after the form is set, such as to send a body, headers or a query string.</param>
    /// <param name="configureServices">Registers more services next to MVC's.</param>
    /// <returns>The bound arguments and the controller context.</returns>
    public static async Task<Result> BindAsync(
        IStartup startup,
        MethodInfo method,
        string culture,
        IDictionary<string, StringValues> form,
        IDictionary<string, object> routeValues = null,
        Controller controller = null,
        Action<HttpRequest> configureRequest = null,
        Action<IServiceCollection> configureServices = null)
    {
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUICulture = CultureInfo.CurrentUICulture;

        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);

        try
        {
            var services = new ServiceCollection().AddLogging().AddMvcCore().Services;
            startup?.ConfigureServices(services);
            configureServices?.Invoke(services);

            var mvc = services.BuildServiceProvider();

            var httpContext = new DefaultHttpContext { RequestServices = mvc };
            httpContext.Request.Method = HttpMethods.Post;
            httpContext.Request.ContentType = "application/x-www-form-urlencoded";
            httpContext.Request.Form = new FormCollection(new Dictionary<string, StringValues>(form));
            configureRequest?.Invoke(httpContext.Request);

            var routeData = new RouteData();

            foreach (var (key, value) in routeValues ?? new Dictionary<string, object>())
            {
                routeData.Values[key] = value;
            }

            var controllerContext = new ControllerContext(new ActionContext(httpContext, routeData, new ControllerActionDescriptor
            {
                MethodInfo = method,
                ControllerTypeInfo = method.DeclaringType.GetTypeInfo(),
            }))
            {
                ValueProviderFactories = [.. mvc.GetRequiredService<IOptions<MvcOptions>>().Value.ValueProviderFactories],
            };

            if (controller is not null)
            {
                controller.ControllerContext = controllerContext;
            }

            return new Result(await BindParametersAsync(mvc, controllerContext, method), controllerContext);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUICulture;
        }
    }

    private static async Task<object[]> BindParametersAsync(IServiceProvider mvc, ControllerContext controllerContext, MethodInfo method)
    {
        var metadataProvider = (ModelMetadataProvider)mvc.GetRequiredService<IModelMetadataProvider>();
        var binderFactory = mvc.GetRequiredService<IModelBinderFactory>();
        var parameterBinder = mvc.GetRequiredService<ParameterBinder>();
        var valueProvider = await CompositeValueProvider.CreateAsync(controllerContext);

        var parameters = method.GetParameters();
        var arguments = new object[parameters.Length];

        for (var i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];
            var metadata = metadataProvider.GetMetadataForParameter(parameter);
            var bindingInfo = BindingInfo.GetBindingInfo(parameter.GetCustomAttributes(), metadata);

            if (bindingInfo?.BindingSource == BindingSource.Services)
            {
                continue;
            }

            var binder = binderFactory.CreateBinder(new ModelBinderFactoryContext
            {
                Metadata = metadata,
                BindingInfo = bindingInfo,
                CacheToken = parameter,
            });

            var descriptor = new ControllerParameterDescriptor
            {
                Name = parameter.Name,
                ParameterType = parameter.ParameterType,
                BindingInfo = bindingInfo,
                ParameterInfo = parameter,
            };

            var bound = await parameterBinder.BindModelAsync(controllerContext, binder, valueProvider, descriptor, metadata, value: null, container: null);

            arguments[i] = bound.IsModelSet ? bound.Model : parameter.HasDefaultValue ? parameter.DefaultValue : null;
        }

        return arguments;
    }
}
