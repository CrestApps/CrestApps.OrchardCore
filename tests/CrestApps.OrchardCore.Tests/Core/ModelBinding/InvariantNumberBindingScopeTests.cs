using System.Reflection;
using System.Text;
using CrestApps.OrchardCore.Core.ModelBinding;
using CrestApps.OrchardCore.Workforce.Controllers;
using CrestApps.OrchardCore.Workforce.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using OrchardCore.Modules;
using WorkforceStartup = CrestApps.OrchardCore.Workforce.Startup;

namespace CrestApps.OrchardCore.Tests.Core.ModelBinding;

/// <summary>
/// G11 and G12 (fourth fix verification): which numbers the invariant binder reads. The elements of a collection on a
/// CrestApps model are read invariantly too (G11), and a parameter bound from the body, a header, or by its own binder is
/// left to the binder the framework picks for it (G12). Each case posts through MVC's own value providers and binders.
/// </summary>
public sealed class InvariantNumberBindingScopeTests
{
    // The time clock policy's points are a dictionary of numbers, posted as Points[Late]=0.5 by a number input.
    [Theory]
    [InlineData("en-US", "0.5")]
    [InlineData("de-DE", "0.5")]
    [InlineData("fr-FR", "0.5")]
    [InlineData("nl-NL", "0.5")]
    [InlineData("de-DE", "0,5")]
    public async Task TimeClockPolicyPoints_BindTheDictionaryValuesTheFormSent(string culture, string posted)
    {
        var method = typeof(TimeClockPoliciesController).GetMethod(nameof(TimeClockPoliciesController.EditPost));

        var result = await FormPostBinder.BindAsync(new WorkforceStartup(), method, culture, new Dictionary<string, StringValues>
        {
            ["Points[Late]"] = posted,
            ["Points[EarlyOut]"] = "1.5",
        });

        var model = Assert.IsType<TimeClockPolicyEditViewModel>(result.Arguments[0]);

        Assert.Equal(0.5m, model.Points["Late"]);
        Assert.Equal(1.5m, model.Points["EarlyOut"]);
    }

    [Theory]
    [InlineData("de-DE")]
    [InlineData("fr-FR")]
    public async Task ArrayAndListParameters_BindTheElementsTheFormSent(string culture)
    {
        var result = await BindAsync(nameof(ScopeController.Elements), culture, new()
        {
            ["amounts[0]"] = "12.50",
            ["amounts[1]"] = "0.25",
            ["rates"] = new StringValues(["1.5", "2.75"]),
        });

        Assert.Equal([12.50m, 0.25m], Assert.IsType<decimal[]>(result.Arguments[0]));
        Assert.Equal([1.5d, 2.75d], Assert.IsType<List<double>>(result.Arguments[1]));
    }

    [Fact]
    public async Task ABodyParameter_IsReadByTheBodyBinder()
    {
        var result = await BindAsync(nameof(ScopeController.Body), "de-DE", [], request =>
        {
            request.ContentType = "application/json";
            request.Body = new MemoryStream(Encoding.UTF8.GetBytes("12.5"));
        });

        Assert.Equal(12.5m, result.Arguments[0]);
    }

    [Fact]
    public async Task AHeaderParameter_IsReadByTheHeaderBinder()
    {
        var result = await BindAsync(nameof(ScopeController.Header), "de-DE", [], request => request.Headers["X-Amount"] = "12.50");

        Assert.Equal(12.50m, result.Arguments[0]);
    }

    [Fact]
    public async Task AParameterWithItsOwnBinder_IsReadByThatBinder()
    {
        var result = await BindAsync(nameof(ScopeController.Custom), "de-DE", new() { ["amount"] = "12.50" });

        Assert.Equal(42m, result.Arguments[0]);
    }

    // The query string and the route hand values over tagged invariant, so they read the same with or without the binder.
    [Theory]
    [InlineData("en-US")]
    [InlineData("de-DE")]
    [InlineData("fr-FR")]
    public async Task QueryAndRouteNumbers_ReadInvariantly(string culture)
    {
        var result = await BindAsync(nameof(ScopeController.QueryAndRoute), culture, [], request => request.QueryString = new QueryString("?fromQuery=12.50&plain=0.25"), new Dictionary<string, object> { ["fromRoute"] = "3.75" });

        Assert.Equal(12.50m, result.Arguments[0]);
        Assert.Equal(3.75m, result.Arguments[1]);
        Assert.Equal(0.25m, result.Arguments[2]);
    }

    [Fact]
    public async Task AFromFormParameter_IsReadInvariantly()
    {
        var result = await BindAsync(nameof(ScopeController.FromForm), "de-DE", new() { ["amount"] = "12.50" });

        Assert.Equal(12.50m, result.Arguments[0]);
    }

    // What MVC hands the provider: a plain parameter has no binding source, so the binder takes it.
    [Fact]
    public void APlainParameter_HasNoBindingSource_AndIsReadByTheInvariantBinder()
    {
        var parameter = typeof(ScopeController).GetMethod(nameof(ScopeController.QueryAndRoute)).GetParameters()[2];
        using var services = CreateServices();
        var metadata = services.GetRequiredService<IModelMetadataProvider>().GetMetadataForType(parameter.ParameterType);

        Assert.Null(BindingInfo.GetBindingInfo(parameter.GetCustomAttributes(), metadata)?.BindingSource);
        Assert.Contains("InvariantNumberModelBinder", Describe(CreateBinder(parameter)));
    }

    private static Task<FormPostBinder.Result> BindAsync(
        string action,
        string culture,
        Dictionary<string, StringValues> form,
        Action<HttpRequest> configureRequest = null,
        IDictionary<string, object> routeValues = null)
        => FormPostBinder.BindAsync(startup: null, typeof(ScopeController).GetMethod(action), culture, form, routeValues, configureRequest: configureRequest, configureServices: services => services.AddInvariantNumberInputs());

    private static ServiceProvider CreateServices()
        => new ServiceCollection().AddLogging().AddMvcCore().Services.AddInvariantNumberInputs().BuildServiceProvider();

    private static IModelBinder CreateBinder(ParameterInfo parameter)
    {
        using var services = CreateServices();
        var metadataProvider = (ModelMetadataProvider)services.GetRequiredService<IModelMetadataProvider>();
        var metadata = metadataProvider.GetMetadataForParameter(parameter);

        return services.GetRequiredService<IModelBinderFactory>().CreateBinder(new ModelBinderFactoryContext
        {
            Metadata = metadata,
            BindingInfo = BindingInfo.GetBindingInfo(parameter.GetCustomAttributes(), metadata),
            CacheToken = parameter,
        });
    }

    private static string Describe(IModelBinder binder)
        => binder.GetType().Name;

    public sealed class ScopeController : Controller
    {
        [HttpPost]
        public IActionResult Elements(decimal[] amounts, List<double> rates) => Ok();

        [HttpPost]
        public IActionResult Body([FromBody] decimal amount) => Ok();

        [HttpPost]
        public IActionResult Header([FromHeader(Name = "X-Amount")] decimal? amount) => Ok();

        [HttpPost]
        public IActionResult Custom([ModelBinder(typeof(FortyTwoBinder))] decimal amount) => Ok();

        [HttpPost]
        public IActionResult QueryAndRoute([FromQuery] decimal? fromQuery, [FromRoute] decimal? fromRoute, decimal? plain) => Ok();

        [HttpPost]
        public IActionResult FromForm([FromForm] decimal? amount) => Ok();
    }

    private sealed class FortyTwoBinder : IModelBinder
    {
        public Task BindModelAsync(ModelBindingContext bindingContext)
        {
            bindingContext.Result = ModelBindingResult.Success(42m);

            return Task.CompletedTask;
        }
    }
}
