using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrchardCore.Abstractions.Setup;
using OrchardCore.Data;
using OrchardCore.Environment.Shell;
using OrchardCore.Recipes.Services;
using OrchardCore.Setup.Services;

namespace CrestApps.OrchardCore.Commerce.IntegrationTests;

/// <summary>
/// One real Orchard Core host for the commerce API suite: the Default tenant set up blank, given the online store
/// sample (products with stock, shipping zones, coupons) and the commerce API features, and driven over HTTP on a
/// loopback port the way a phone app or headless storefront would.
/// </summary>
public sealed class CommerceApiFixture : IAsyncLifetime
{
    private static readonly string[] _features =
    [
        "OrchardCore.Recipes",
        "CrestApps.OrchardCore.Checkout.TestProvider",
        "CrestApps.OrchardCore.Products.Api",
        "CrestApps.OrchardCore.Carts.Api",
        "CrestApps.OrchardCore.Inventory.Api",
        "CrestApps.OrchardCore.Checkout.Api",
        "CrestApps.OrchardCore.Orders.Api",
        "CrestApps.OrchardCore.Customers.Api",
        "CrestApps.OrchardCore.Shipping.Api",
        "CrestApps.OrchardCore.Commerce.Webhooks",
    ];

    private WebApplication _application;
    private string _applicationDataPath;

    public IShellHost ShellHost { get; private set; }

    public Uri BaseAddress { get; private set; }

    /// <summary>
    /// Gets the password of the tenant's administrator. It is generated for this run.
    /// </summary>
    public string Password { get; } = "Aa1!" + Guid.NewGuid().ToString("N");

    public async ValueTask InitializeAsync()
    {
        _applicationDataPath = Path.Combine(Path.GetTempPath(), $"crestapps-commerce-{Guid.NewGuid():N}");
        var webRootPath = Path.Combine(_applicationDataPath, "wwwroot");
        Directory.CreateDirectory(webRootPath);

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(CommerceApiFixture).Assembly.FullName,
            ContentRootPath = _applicationDataPath,
            EnvironmentName = Environments.Development,
            WebRootPath = webRootPath,
        });

        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration["OrchardCore:OrchardCore_Documents:CheckConcurrency"] = bool.FalseString;
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.Services.AddOrchardCms();
        builder.Services.Configure<ShellOptions>(options => options.ShellsApplicationDataPath = _applicationDataPath);

        _application = builder.Build();
        _application.UseOrchardCore();
        await _application.StartAsync();

        ShellHost = _application.Services.GetRequiredService<IShellHost>();
        await ShellHost.InitializeAsync();

        var port = new Uri(_application.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>().Addresses.First()).Port;
        BaseAddress = new Uri($"http://127.0.0.1:{port}/");

        await SetupAsync();
        await InTenantAsync(async services =>
        {
            var features = services.GetRequiredService<IShellFeaturesManager>();
            var available = await features.GetAvailableFeaturesAsync();

            await features.EnableFeaturesAsync(available.Where(feature => _features.Contains(feature.Id)), force: true);
        });

        await InTenantAsync(RunRecipeAsync("ProductListingStore"));
    }

    /// <summary>
    /// Runs an operation in the tenant, with an HTTP context, as one of its background jobs would.
    /// </summary>
    public async Task<T> InTenantAsync<T>(Func<IServiceProvider, Task<T>> operation)
    {
        var settings = ShellHost.TryGetSettings(ShellSettings.DefaultShellName, out var found) ? found : throw new InvalidOperationException("No Default tenant.");
        var result = default(T);

        await using var scope = await ShellHost.GetScopeAsync(settings);
        await scope.UsingAsync(async shellScope =>
        {
            var services = shellScope.ServiceProvider;
            var accessor = services.GetRequiredService<IHttpContextAccessor>();
            accessor.HttpContext = new DefaultHttpContext { RequestServices = services };

            try
            {
                result = await operation(services);
            }
            finally
            {
                accessor.HttpContext = null;
            }
        });

        return result;
    }

    public Task InTenantAsync(Func<IServiceProvider, Task> operation)
        => InTenantAsync<bool>(async services =>
        {
            await operation(services);

            return true;
        });

    /// <summary>
    /// Creates a client of the commerce API: a guest until it's given a bearer token. It keeps the guest tokens the API
    /// hands back, the way an app keeps them between calls.
    /// </summary>
    public ApiClient CreateClient()
        => new(new HttpClient { BaseAddress = new Uri(BaseAddress, "api/commerce/v1/") });

    public async ValueTask DisposeAsync()
    {
        if (_application is not null)
        {
            await _application.StopAsync();
            await _application.DisposeAsync();
        }

        Type.GetType("Microsoft.Data.Sqlite.SqliteConnection, Microsoft.Data.Sqlite")
            ?.GetMethod("ClearAllPools", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            ?.Invoke(null, null);

        for (var attempt = 0; attempt < 10 && Directory.Exists(_applicationDataPath); attempt++)
        {
            try
            {
                Directory.Delete(_applicationDataPath, recursive: true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                await Task.Delay(200);
            }
        }
    }

    private async Task SetupAsync()
    {
        var settings = ShellHost.TryGetSettings(ShellSettings.DefaultShellName, out var found) ? found : null;
        var errors = new Dictionary<string, string>();

        await using var scope = await ShellHost.GetScopeAsync(settings);
        await scope.UsingAsync(async shellScope =>
        {
            var services = shellScope.ServiceProvider;
            var accessor = services.GetRequiredService<IHttpContextAccessor>();
            accessor.HttpContext = new DefaultHttpContext { RequestServices = services };

            var setup = services.GetRequiredService<ISetupService>();
            var recipe = (await setup.GetSetupRecipesAsync()).First(candidate => candidate.Name == "Blank");

            await setup.SetupAsync(new SetupContext
            {
                ShellSettings = settings,
                EnabledFeatures = [],
                Errors = errors,
                Recipe = recipe,
                Properties =
                {
                    [SetupConstants.SiteName] = "Commerce API tests",
                    [SetupConstants.AdminUsername] = "admin",
                    [SetupConstants.AdminEmail] = "admin@example.invalid",
                    [SetupConstants.AdminPassword] = Password,
                    [SetupConstants.DatabaseProvider] = DatabaseProviderValue.Sqlite,
                    [SetupConstants.DatabaseName] = "OrchardCore.db",
                    [SetupConstants.DatabaseTablePrefix] = string.Empty,
                },
            });

            accessor.HttpContext = null;
        });

        Assert.Empty(errors);
    }

    private static Func<IServiceProvider, Task> RunRecipeAsync(string name)
        => async services =>
        {
            var recipes = new List<global::OrchardCore.Recipes.Models.RecipeDescriptor>();

            foreach (var harvester in services.GetServices<IRecipeHarvester>())
            {
                recipes.AddRange(await harvester.HarvestRecipesAsync());
            }

            var recipe = recipes.FirstOrDefault(candidate => candidate.Name == name)
                ?? throw new InvalidOperationException($"No recipe '{name}'.");

            await services.GetRequiredService<IRecipeExecutor>().ExecuteAsync(Guid.NewGuid().ToString("n"), recipe, new Dictionary<string, object>(), CancellationToken.None);
        };
}

/// <summary>
/// A client of the commerce API that keeps the guest cart and checkout tokens it's handed.
/// </summary>
public sealed class ApiClient : IDisposable
{
    private readonly HttpClient _http;

    public ApiClient(HttpClient http)
    {
        _http = http;
    }

    public string CartToken { get; set; }

    public string CheckoutToken { get; set; }

    public string Bearer { get; set; }

    public Task<ApiResponse> GetAsync(string path)
        => SendAsync(HttpMethod.Get, path, body: null);

    public Task<ApiResponse> PostAsync(string path, object body = null, string idempotencyKey = null)
        => SendAsync(HttpMethod.Post, path, body, idempotencyKey);

    public Task<ApiResponse> PutAsync(string path, object body)
        => SendAsync(HttpMethod.Put, path, body);

    public Task<ApiResponse> PatchAsync(string path, object body)
        => SendAsync(HttpMethod.Patch, path, body);

    public Task<ApiResponse> DeleteAsync(string path)
        => SendAsync(HttpMethod.Delete, path, body: null);

    public async Task<ApiResponse> SendAsync(HttpMethod method, string path, object body, string idempotencyKey = null)
    {
        using var request = new HttpRequestMessage(method, path);

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        if (CartToken is not null)
        {
            request.Headers.Add("X-Cart-Token", CartToken);
        }

        if (CheckoutToken is not null)
        {
            request.Headers.Add("X-Checkout-Token", CheckoutToken);
        }

        if (idempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        }

        if (Bearer is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Bearer);
        }

        using var response = await _http.SendAsync(request, TestContext.Current.CancellationToken);

        if (response.Headers.TryGetValues("X-Cart-Token", out var cart))
        {
            CartToken = cart.First();
        }

        if (response.Headers.TryGetValues("X-Checkout-Token", out var checkout))
        {
            CheckoutToken = checkout.First();
        }

        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        return new ApiResponse((int)response.StatusCode, string.IsNullOrEmpty(text) ? null : JsonNode.Parse(text), text);
    }

    public void Dispose()
        => _http.Dispose();
}

/// <summary>
/// An answer of the API: its status and its JSON.
/// </summary>
public sealed record ApiResponse(int Status, JsonNode Json, string Text)
{
    public override string ToString()
        => $"{Status} {Text}";
}

[CollectionDefinition(Name)]
public sealed class CommerceApiCollection : ICollectionFixture<CommerceApiFixture>
{
    public const string Name = "Commerce API";
}
