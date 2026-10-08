using CrestApps.OrchardCore.TenantHierarchy.Core.Guards;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrchardCore.Abstractions.Setup;
using OrchardCore.Data;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Models;
using OrchardCore.Setup.Services;

namespace CrestApps.OrchardCore.TenantHierarchy.IntegrationTests;

/// <summary>
/// Starts a real Orchard Core host with the tenant hierarchy guards, on a loopback port, so tests can create tenants,
/// run code inside them and drive them over HTTP.
/// </summary>
public sealed class TenantHierarchyTestHost : IAsyncDisposable
{
    private readonly WebApplication _application;
    private readonly string _applicationDataPath;

    private TenantHierarchyTestHost(WebApplication application, string applicationDataPath)
    {
        _application = application;
        _applicationDataPath = applicationDataPath;
        ShellHost = application.Services.GetRequiredService<IShellHost>();
        ShellSettingsManager = application.Services.GetRequiredService<IShellSettingsManager>();
    }

    /// <summary>
    /// Gets the host's shell host. It is the tenant hierarchy guard, and test code runs outside any tenant, so it may
    /// reach every tenant.
    /// </summary>
    public IShellHost ShellHost { get; }

    /// <summary>
    /// Gets the host's shell settings manager.
    /// </summary>
    public IShellSettingsManager ShellSettingsManager { get; }

    /// <summary>
    /// Gets the loopback port the host listens on.
    /// </summary>
    public int Port => new Uri(_application.Services
        .GetRequiredService<IServer>()
        .Features
        .Get<IServerAddressesFeature>()
        .Addresses
        .First()).Port;

    /// <summary>
    /// Starts the host.
    /// </summary>
    /// <param name="configuration">More application configuration entries.</param>
    public static async Task<TenantHierarchyTestHost> StartAsync(IReadOnlyDictionary<string, string> configuration = null)
    {
        var applicationDataPath = Path.Combine(Path.GetTempPath(), $"crestapps-th-{Guid.NewGuid():N}");
        var webRootPath = Path.Combine(applicationDataPath, "wwwroot");
        Directory.CreateDirectory(webRootPath);

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(TenantHierarchyTestHost).Assembly.FullName,
            ContentRootPath = applicationDataPath,
            EnvironmentName = Environments.Development,
            WebRootPath = webRootPath,
        });

        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration["TenantHierarchy:Scheme"] = "http";
        builder.Configuration["TenantHierarchy:PlatformDomain"] = "localhost";
        builder.Configuration["OrchardCore:OrchardCore_Documents:CheckConcurrency"] = bool.FalseString;

        foreach (var entry in configuration ?? new Dictionary<string, string>())
        {
            builder.Configuration[entry.Key] = entry.Value;
        }

        builder.Services.AddOrchardCms(orchardCore => orchardCore.AddTenantHierarchy());
        builder.Services.Configure<ShellOptions>(options => options.ShellsApplicationDataPath = applicationDataPath);

        var application = builder.Build();
        application.UseOrchardCore();
        await application.StartAsync();
        await application.Services.GetRequiredService<IShellHost>().InitializeAsync();

        return new TenantHierarchyTestHost(application, applicationDataPath);
    }

    /// <summary>
    /// Returns the settings of a tenant, read without the guard's filtering.
    /// </summary>
    /// <param name="name">The tenant name.</param>
    public ShellSettings GetSettings(string name)
    {
        var host = ShellHost is GuardedShellHost guarded ? guarded.Inner : ShellHost;

        return host.TryGetSettings(name, out var settings) ? settings : null;
    }

    /// <summary>
    /// Creates an uninitialized tenant with a host.
    /// </summary>
    /// <param name="name">The tenant name.</param>
    /// <param name="host">The host, or <see langword="null"/> for a path prefix equal to the name.</param>
    public async Task<ShellSettings> CreateTenantAsync(string name, string host)
    {
        using var settings = ShellSettingsManager.CreateDefaultSettings().AsUninitialized().AsDisposable();
        settings.Name = name;
        settings.RequestUrlHost = host;
        settings.RequestUrlPrefix = host is null ? name : null;
        settings["DatabaseProvider"] = DatabaseProviderValue.Sqlite;
        settings["Secret"] = Guid.NewGuid().ToString();

        await ShellHost.UpdateShellSettingsAsync(settings);

        return GetSettings(name);
    }

    /// <summary>
    /// Sets a tenant up with a recipe and an administrator, from the tenant's own setup shell.
    /// </summary>
    /// <param name="tenantName">The tenant name.</param>
    /// <param name="recipeName">The setup recipe.</param>
    /// <param name="userName">The administrator user name.</param>
    /// <param name="password">The administrator password.</param>
    public async Task SetupTenantAsync(string tenantName, string recipeName, string userName, string password)
    {
        var settings = GetSettings(tenantName);
        var errors = new Dictionary<string, string>();

        await using var scope = await ShellHost.GetScopeAsync(settings);
        await scope.UsingAsync(async shellScope =>
        {
            var services = shellScope.ServiceProvider;
            var httpContextAccessor = services.GetRequiredService<IHttpContextAccessor>();
            httpContextAccessor.HttpContext = new DefaultHttpContext
            {
                RequestServices = services,
            };

            var setupService = services.GetRequiredService<ISetupService>();
            var recipes = await setupService.GetSetupRecipesAsync();
            var recipe = recipes.FirstOrDefault(candidate => candidate.Name == recipeName)
                ?? throw new InvalidOperationException($"No setup recipe '{recipeName}'. Available: {string.Join(", ", recipes.Select(candidate => candidate.Name))}.");

            await setupService.SetupAsync(new SetupContext
            {
                ShellSettings = settings,
                EnabledFeatures = [],
                Errors = errors,
                Recipe = recipe,
                Properties =
                {
                    [SetupConstants.SiteName] = tenantName,
                    [SetupConstants.AdminUsername] = userName,
                    [SetupConstants.AdminEmail] = $"{userName}@example.invalid",
                    [SetupConstants.AdminPassword] = password,
                    [SetupConstants.DatabaseProvider] = DatabaseProviderValue.Sqlite,
                    [SetupConstants.DatabaseName] = "OrchardCore.db",
                    [SetupConstants.DatabaseTablePrefix] = string.Empty,
                },
            });

            httpContextAccessor.HttpContext = null;
        });

        Assert.Empty(errors);
    }

    /// <summary>
    /// Runs an operation inside a tenant's scope, with an HTTP context, as a background job of that tenant would.
    /// </summary>
    /// <param name="tenantName">The tenant name.</param>
    /// <param name="operation">The operation.</param>
    public async Task InTenantAsync(string tenantName, Func<IServiceProvider, Task> operation)
    {
        await InTenantAsync<bool>(tenantName, async services =>
        {
            await operation(services);

            return true;
        });
    }

    /// <summary>
    /// Runs an operation inside a tenant's scope, with an HTTP context, and returns its result.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="tenantName">The tenant name.</param>
    /// <param name="operation">The operation.</param>
    public async Task<T> InTenantAsync<T>(string tenantName, Func<IServiceProvider, Task<T>> operation)
    {
        var settings = GetSettings(tenantName) ?? throw new InvalidOperationException($"No tenant '{tenantName}'.");
        var result = default(T);

        await using var scope = await ShellHost.GetScopeAsync(settings);
        await scope.UsingAsync(async shellScope =>
        {
            var services = shellScope.ServiceProvider;
            var httpContextAccessor = services.GetRequiredService<IHttpContextAccessor>();
            httpContextAccessor.HttpContext = new DefaultHttpContext
            {
                RequestServices = services,
            };

            try
            {
                result = await operation(services);
            }
            finally
            {
                httpContextAccessor.HttpContext = null;
            }
        });

        return result;
    }

    /// <summary>
    /// Creates a browser that sends every request to this host and keeps cookies per host.
    /// </summary>
    public TestBrowser CreateBrowser()
        => new(Port);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _application.StopAsync();
        await _application.DisposeAsync();

        var connectionType = Type.GetType("Microsoft.Data.Sqlite.SqliteConnection, Microsoft.Data.Sqlite");
        connectionType
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
}
