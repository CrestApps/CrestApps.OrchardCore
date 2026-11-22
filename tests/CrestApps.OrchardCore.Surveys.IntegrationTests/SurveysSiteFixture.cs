using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using CrestApps.OrchardCore.Surveys;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrchardCore.Abstractions.Setup;
using OrchardCore.Data;
using OrchardCore.Environment.Shell;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;
using OrchardCore.Setup.Services;
using OrchardCore.Users;
using OrchardCore.Users.Models;
using OrchardCore.Users.Services;

namespace CrestApps.OrchardCore.Surveys.IntegrationTests;

/// <summary>
/// One real Orchard Core host for the Surveys suite: the Default tenant set up blank with Surveys and the features it
/// integrates with, an administrator, and a respondent who may respond to surveys open to signed-in users.
/// </summary>
public partial class SurveysSiteFixture : IAsyncLifetime
{
    /// <summary>
    /// The password of every account the fixture creates.
    /// </summary>
    public const string Password = "Password1!";

    /// <summary>
    /// The user name of the respondent.
    /// </summary>
    public const string RespondentUserName = "respondent1";

    private static readonly string[] _features =
    [
        "OrchardCore.Recipes",
        "OrchardCore.Deployment",
        "OrchardCore.Workflows",
        "OrchardCore.Notifications",
        "OrchardCore.Themes",
        SurveysConstants.Features.Surveys,
        SurveysConstants.Features.Invitations,
        SurveysConstants.Features.Embedding,
        SurveysConstants.Features.Api,
        CrestApps.OrchardCore.Reports.ReportsConstants.Feature,
        CrestApps.OrchardCore.Users.UsersConstants.Feature.Audiences,
    ];

    private WebApplication _application;
    private string _applicationDataPath;

    /// <summary>
    /// Gets the features the fixture turns on after setup.
    /// </summary>
    protected virtual IReadOnlyCollection<string> Features => _features;

    /// <summary>
    /// Gets a value indicating whether the fixture turns on a front-end theme and creates the respondent.
    /// </summary>
    protected virtual bool ConfiguresRespondentSite => true;

    /// <summary>
    /// Gets the shell host.
    /// </summary>
    public IShellHost ShellHost { get; private set; }

    /// <summary>
    /// Gets the address of the host.
    /// </summary>
    public Uri BaseAddress { get; private set; }

    /// <summary>
    /// Gets the id of the respondent.
    /// </summary>
    public string RespondentUserId { get; private set; }

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        _applicationDataPath = Path.Combine(Path.GetTempPath(), $"crestapps-surveys-{Guid.NewGuid():N}");
        var webRootPath = Path.Combine(_applicationDataPath, "wwwroot");
        Directory.CreateDirectory(webRootPath);

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(SurveysSiteFixture).Assembly.FullName,
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

            await features.EnableFeaturesAsync(available.Where(feature => Features.Contains(feature.Id)), force: true);
        });

        if (!ConfiguresRespondentSite)
        {
            return;
        }

        await InTenantAsync(services => RunRecipeAsync(services, new JsonObject
        {
            ["name"] = "SurveysTestTheme",
            ["steps"] = new JsonArray(
                // A front-end theme as on a real site: Orchard Core applies no placement without one.
                new JsonObject
                {
                    ["name"] = "feature",
                    ["enable"] = new JsonArray("CrestApps.OrchardCore.StorefrontTheme"),
                },
                new JsonObject
                {
                    ["name"] = "Themes",
                    ["Site"] = "CrestApps.OrchardCore.StorefrontTheme",
                }),
        }));

        RespondentUserId = await InTenantAsync(services => CreateUserAsync(services, RespondentUserName));
    }

    /// <summary>
    /// Runs an operation in a scope of the Default tenant.
    /// </summary>
    /// <typeparam name="T">The type of the result.</typeparam>
    /// <param name="operation">The operation.</param>
    /// <returns>The result.</returns>
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

    /// <summary>
    /// Runs an operation in a scope of the Default tenant.
    /// </summary>
    /// <param name="operation">The operation.</param>
    /// <returns>A task.</returns>
    public Task InTenantAsync(Func<IServiceProvider, Task> operation)
        => InTenantAsync<bool>(async services =>
        {
            await operation(services);

            return true;
        });

    /// <summary>
    /// Creates an HTTP client with cookies, signed in when a user name is given.
    /// </summary>
    /// <param name="userName">The user to sign in, or <see langword="null"/> for an anonymous client.</param>
    /// <returns>The client.</returns>
    public async Task<HttpClient> CreateClientAsync(string userName = null)
    {
        var http = new HttpClient(new HttpClientHandler { CookieContainer = new CookieContainer(), AllowAutoRedirect = false })
        {
            BaseAddress = BaseAddress,
        };

        if (userName is not null)
        {
            var login = await http.GetStringAsync("Login", TestContext.Current.CancellationToken);
            var signedIn = await http.PostAsync("Login", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["LoginForm.UserName"] = userName,
                ["LoginForm.Password"] = Password,
                ["__RequestVerificationToken"] = Token(login),
            }), TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.Redirect, signedIn.StatusCode);
        }

        return http;
    }

    /// <summary>
    /// Reads the antiforgery token of a page.
    /// </summary>
    /// <param name="html">The page.</param>
    /// <returns>The token.</returns>
    public static string Token(string html)
        => TokenPattern().Match(html) is { Success: true } match ? WebUtility.HtmlDecode(match.Groups["token"].Value) : throw new InvalidOperationException("The page has no antiforgery token.");

    /// <summary>
    /// Runs a recipe.
    /// </summary>
    /// <param name="services">The tenant services.</param>
    /// <param name="recipe">The recipe.</param>
    /// <returns>A task.</returns>
    public static async Task RunRecipeAsync(IServiceProvider services, JsonObject recipe)
    {
        var folder = Path.Combine(Path.GetTempPath(), $"crestapps-surveys-recipe-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(folder, "Recipe.json"), recipe.ToJsonString(), Encoding.UTF8);

            using var fileProvider = new PhysicalFileProvider(folder);
            var descriptor = new RecipeDescriptor
            {
                Name = recipe["name"]?.GetValue<string>() ?? "Recipe",
                FileProvider = fileProvider,
                BasePath = string.Empty,
                RecipeFileInfo = fileProvider.GetFileInfo("Recipe.json"),
            };

            await services.GetRequiredService<IRecipeExecutor>().ExecuteAsync(Guid.NewGuid().ToString("n"), descriptor, new Dictionary<string, object>(), CancellationToken.None);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <inheritdoc />
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

    internal static async Task<string> CreateUserAsync(IServiceProvider services, string userName, params string[] roles)
    {
        var errors = new List<string>();
        var user = await services.GetRequiredService<IUserService>().CreateUserAsync(new User
        {
            UserName = userName,
            Email = userName + "@example.com",
            EmailConfirmed = true,
            IsEnabled = true,
            RoleNames = roles.ToList(),
        }, Password, (_, message) => errors.Add(message));

        Assert.True(user is not null, string.Join(", ", errors));

        return ((User)user).UserId;
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
                    [SetupConstants.SiteName] = "Surveys tests",
                    [SetupConstants.AdminUsername] = "admin",
                    [SetupConstants.AdminEmail] = "admin@example.com",
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

    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*value=\"(?<token>[^\"]+)\"")]
    private static partial Regex TokenPattern();
}
