using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.Deployment;
using OrchardCore.Deployment.Services;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Models;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Navigation;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;
using YSession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Agent.SiteAdministration;

/// <summary>
/// Builds the services of a tool call for the site administration tools: the real display manager and MVC model
/// binding with a test settings driver, an admin menu, feature profiles, tenants, deployment plans and a recipe step
/// handler that records the steps it gets. Every secret of the fixture starts with <see cref="Canary"/>.
/// </summary>
internal sealed class SiteAdministrationToolHarness
{
    /// <summary>
    /// The text every value a tool must not return starts with.
    /// </summary>
    public const string Canary = "CANARY";

    /// <summary>
    /// The settings group of the test settings driver.
    /// </summary>
    public const string GroupId = "test";

    /// <summary>
    /// The settings group of the cultures driver, whose editor doesn't show the values it posts.
    /// </summary>
    public const string CulturesGroupId = "cultures";

    public SiteAdministrationToolHarness()
    {
        StoredSettings = new JsonObject
        {
            [nameof(TestSettings)] = new JsonObject
            {
                [nameof(TestSettings.Title)] = "Contoso",
                [nameof(TestSettings.ShowBanner)] = false,
                [nameof(TestSettings.PageSize)] = 10,
                [nameof(TestSettings.ApiKey)] = Canary + "-api-key",
            },
            [nameof(LocalizationSettings)] = new JsonObject
            {
                [nameof(LocalizationSettings.SupportedCultures)] = new JsonArray("en-US"),
                [nameof(LocalizationSettings.DefaultCulture)] = "en-US",
            },
        };

        Profiles["profile-1"] = new FeatureProfile
        {
            Id = "profile-1",
            Name = "No search",
            FeatureRules =
            [
                new FeatureRule { Rule = "Exclude", Expression = "OrchardCore.Search*" },
            ],
        };

        Plans.Add(new DeploymentPlan
        {
            Id = 7,
            Name = "Nightly",
            DeploymentSteps =
            {
                new TestExportStep(),
            },
        });
    }

    public bool SignedIn { get; set; } = true;

    public Func<Permission, object, bool> Authorize { get; set; } = (_, _) => true;

    /// <summary>
    /// Gets the stored site properties the test driver reads and writes.
    /// </summary>
    public JsonObject StoredSettings { get; private set; }

    /// <summary>
    /// Gets the number of times the site settings were saved.
    /// </summary>
    public int SiteSaves { get; private set; }

    public Mock<IShellReleaseManager> ShellReleaseManager { get; } = new();

    public List<MenuItem> AdminMenu { get; } =
    [
        new MenuItem
        {
            Text = new LocalizedString("Configuration", "Configuration"),
            Href = "#",
            Items =
            [
                new MenuItem
                {
                    Text = new LocalizedString("Settings", "Settings"),
                    Href = "#",
                    Items =
                    [
                        new MenuItem
                        {
                            Text = new LocalizedString("General", "General"),
                            Href = "/Admin/Settings/general",
                            RouteValues = new RouteValueDictionary { ["area"] = "OrchardCore.Settings", ["groupId"] = "general" },
                        },
                        new MenuItem
                        {
                            Text = new LocalizedString("reCAPTCHA", "reCAPTCHA"),
                            Href = "/Admin/Settings/recaptcha",
                            RouteValues = new RouteValueDictionary { ["area"] = "OrchardCore.Settings", ["groupId"] = "recaptcha" },
                        },
                    ],
                },
            ],
        },
        new MenuItem
        {
            Text = new LocalizedString("Tools", "Tools"),
            Href = "#",
            Items =
            [
                new MenuItem
                {
                    Text = new LocalizedString("Background Tasks", "Background Tasks"),
                    Href = "/Admin/BackgroundTasks",
                    RouteValues = new RouteValueDictionary { ["area"] = "OrchardCore.BackgroundTasks" },
                },
            ],
        },
    ];

    public Dictionary<string, FeatureProfile> Profiles { get; } = [];

    public List<ShellSettings> Tenants { get; } =
    [
        new ShellSettings { Name = ShellSettings.DefaultShellName },
        new ShellSettings { Name = "acme", ["FeatureProfile"] = "profile-1" },
    ];

    public bool IsDefaultTenant { get; set; } = true;

    public List<DeploymentPlan> Plans { get; } = [];

    public List<DeploymentPlan> DeletedPlans { get; } = [];

    public List<RecipeExecutionContext> RecipeSteps { get; } = [];

    public List<JsonObject> AdminMenus { get; } =
    [
        new JsonObject
        {
            ["Id"] = "menu-1",
            ["Name"] = "Shortcuts",
            ["Enabled"] = true,
            ["MenuItems"] = new JsonArray(
                new JsonObject
                {
                    ["$type"] = "LinkAdminNode",
                    ["LinkText"] = "Reports",
                    ["LinkUrl"] = "/Admin/Reports",
                    ["Items"] = new JsonArray(),
                }),
        },
    ];

    /// <summary>
    /// Gets or sets a last change to the services, made before the provider is built.
    /// </summary>
    public Action<IServiceCollection> Configure { get; set; }

    public ServiceProvider Build()
    {
        var services = AgentToolTestContext.CreateServices(SignedIn, Authorize);

        services.AddLogging();
        services.AddLocalization();
        services.AddMvcCore().AddDataAnnotations();
        services.AddEditorDisplayManagement();
        services.AddScoped<IDisplayDriver<ISite>, TestSettingsDisplayDriver>();
        services.AddScoped<IDisplayDriver<ISite>, CulturesSettingsDisplayDriver>();

        var siteService = new Mock<ISiteService>();
        siteService.Setup(service => service.GetSiteSettingsAsync()).ReturnsAsync(() => CreateSite());
        siteService.Setup(service => service.LoadSiteSettingsAsync()).ReturnsAsync(() => CreateSite());
        siteService.Setup(service => service.UpdateSiteSettingsAsync(It.IsAny<ISite>()))
            .Callback<ISite>(site =>
            {
                SiteSaves++;
                StoredSettings = (JsonObject)site.Properties.DeepClone();
            })
            .Returns(Task.CompletedTask);

        services.AddSingleton(siteService.Object);
        services.AddSingleton(ShellReleaseManager.Object);

        var navigationManager = new Mock<INavigationManager>();
        navigationManager
            .Setup(manager => manager.BuildMenuAsync(NavigationConstants.AdminId, It.IsAny<Microsoft.AspNetCore.Mvc.ActionContext>()))
            .ReturnsAsync(() => AdminMenu);
        services.AddSingleton(navigationManager.Object);

        var featureProfilesService = new Mock<IFeatureProfilesService>();
        featureProfilesService.Setup(service => service.GetFeatureProfilesAsync()).ReturnsAsync(() => Profiles);
        services.AddSingleton(featureProfilesService.Object);

        var shellHost = new Mock<IShellHost>();
        shellHost.Setup(host => host.GetAllSettings()).Returns(() => Tenants);
        services.AddSingleton(shellHost.Object);
        services.AddSingleton(_ => new ShellSettings { Name = IsDefaultTenant ? ShellSettings.DefaultShellName : "acme" });

        services.AddSingleton<IRecipeStepHandler>(new RecordingRecipeStepHandler(this));

        services.AddSingleton<IDeploymentStepFactory>(new TestStepFactory("AdminMenuDeploymentStep", () => new TestAdminMenuStep()));
        services.AddSingleton<IDeploymentStepFactory>(new TestStepFactory(nameof(TestExportStep), () => new TestExportStep()));
        services.AddSingleton<IDeploymentManager>(new TestDeploymentManager(this));

        var planService = new Mock<IDeploymentPlanService>();
        planService.Setup(service => service.GetAllDeploymentPlansAsync()).ReturnsAsync(() => Plans);
        planService.Setup(service => service.GetDeploymentPlansAsync(It.IsAny<string[]>()))
            .ReturnsAsync((string[] names) => Plans.Where(plan => names.Contains(plan.Name)).ToList());
        services.AddSingleton(planService.Object);

        // The deployment plan tools read the plans with a session query, which sees the plans saved so far.
        var planQuery = new Mock<global::YesSql.IQuery<DeploymentPlan>>();
        planQuery.Setup(query => query.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Plans.ToList());

        var rootQuery = new Mock<global::YesSql.IQuery>();
        rootQuery.Setup(query => query.For<DeploymentPlan>(It.IsAny<bool>())).Returns(planQuery.Object);

        var session = new Mock<YSession>();
        session.Setup(s => s.Query(It.IsAny<string>())).Returns(rootQuery.Object);
        session.Setup(s => s.GetAsync<DeploymentPlan>(It.IsAny<long[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((long[] ids, string _, CancellationToken _) => Plans.Where(plan => ids.Contains(plan.Id)).ToList());
        session.Setup(s => s.Delete(It.IsAny<object>(), It.IsAny<string>()))
            .Callback<object, string>((entity, _) =>
            {
                if (entity is DeploymentPlan plan)
                {
                    DeletedPlans.Add(plan);
                    Plans.Remove(plan);
                }
            });
        services.AddSingleton(session.Object);

        Configure?.Invoke(services);

        return services.BuildServiceProvider();
    }

    private ISite CreateSite()
    {
        var properties = (JsonObject)StoredSettings.DeepClone();
        var site = new Mock<ISite>();

        site.SetupGet(s => s.Properties).Returns(properties);
        site.SetupProperty(s => s.SuperUser, "super-user");
        site.SetupProperty(s => s.SiteSalt, Canary + "-salt");
        site.SetupProperty(s => s.HomeRoute, new RouteValueDictionary { ["area"] = "OrchardCore.Contents" });

        return site.Object;
    }

    /// <summary>
    /// The settings the test driver stores in the site properties.
    /// </summary>
    public sealed class TestSettings
    {
        public string Title { get; set; }

        public bool ShowBanner { get; set; }

        public int PageSize { get; set; }

        public string ApiKey { get; set; }
    }


    /// <summary>
    /// A settings driver written the way Orchard Core's are: it checks nothing more, binds its view model with its
    /// prefix, validates, keeps the current API key when none is posted and asks for the tenant reload.
    /// </summary>
    public sealed class TestSettingsDisplayDriver : SiteDisplayDriver<TestSettings>
    {
        private readonly IShellReleaseManager _shellReleaseManager;

        public TestSettingsDisplayDriver(IShellReleaseManager shellReleaseManager)
        {
            _shellReleaseManager = shellReleaseManager;
        }

        protected override string SettingsGroupId => GroupId;

        public override IDisplayResult Edit(ISite site, TestSettings settings, BuildEditorContext context)
        {
            return Initialize<TestSettingsViewModel>("TestSettings_Edit", model =>
            {
                model.Title = settings.Title;
                model.ShowBanner = settings.ShowBanner;
                model.PageSize = settings.PageSize;
                model.ApiKey = settings.ApiKey;
            }).Location("Content:1")
            .OnGroup(GroupId);
        }

        public override async Task<IDisplayResult> UpdateAsync(ISite site, TestSettings settings, UpdateEditorContext context)
        {
            var model = new TestSettingsViewModel();

            await context.Updater.TryUpdateModelAsync(model, Prefix);

            if (model.PageSize < 1)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.PageSize), "The page size must be greater than zero.");
            }

            settings.Title = model.Title;
            settings.ShowBanner = model.ShowBanner;
            settings.PageSize = model.PageSize;

            if (!string.IsNullOrEmpty(model.ApiKey))
            {
                settings.ApiKey = model.ApiKey;
            }

            _shellReleaseManager.RequestRelease();

            return Edit(site, settings, context);
        }
    }

    /// <summary>
    /// The settings of the cultures driver, stored under the key Orchard Core's Cultures page uses.
    /// </summary>
    public sealed class LocalizationSettings
    {
        public string[] SupportedCultures { get; set; } = [];

        public string DefaultCulture { get; set; }
    }

    /// <summary>
    /// A settings driver written as Orchard Core's Cultures page is: its editor shows a list of cultures and leaves the
    /// posted fields empty (a script on the page fills them), and its save reads SupportedCultures as JSON text.
    /// </summary>
    public sealed class CulturesSettingsDisplayDriver : SiteDisplayDriver<LocalizationSettings>
    {
        protected override string SettingsGroupId => CulturesGroupId;

        public override IDisplayResult Edit(ISite site, LocalizationSettings settings, BuildEditorContext context)
        {
            return Initialize<CulturesSettingsViewModel>("CulturesSettings_Edit", model =>
            {
                model.Shown = settings.SupportedCultures;
            }).Location("Content:1")
            .OnGroup(CulturesGroupId);
        }

        public override async Task<IDisplayResult> UpdateAsync(ISite site, LocalizationSettings settings, UpdateEditorContext context)
        {
            var model = new CulturesSettingsViewModel();

            await context.Updater.TryUpdateModelAsync(model, Prefix);

            // As Orchard Core's driver: anything but JSON text throws.
            var cultures = JsonSerializer.Deserialize<string[]>(model.SupportedCultures);

            settings.SupportedCultures = cultures;
            settings.DefaultCulture = cultures.Contains(model.DefaultCulture)
                ? model.DefaultCulture
                : cultures.FirstOrDefault();

            return Edit(site, settings, context);
        }
    }

    public sealed class TestAdminMenuStep : DeploymentStep
    {
        public TestAdminMenuStep()
        {
            Name = "AdminMenu";
        }
    }

    public sealed class TestExportStep : DeploymentStep
    {
        public TestExportStep()
        {
            Name = "Export";
        }

        public bool IncludeAll { get; set; } = true;
    }

    private sealed class TestStepFactory : IDeploymentStepFactory
    {
        private readonly Func<DeploymentStep> _create;

        public TestStepFactory(string name, Func<DeploymentStep> create)
        {
            Name = name;
            _create = create;
        }

        public string Name { get; }

        public DeploymentStep Create()
            => _create();
    }

    private sealed class TestDeploymentManager : IDeploymentManager
    {
        private readonly SiteAdministrationToolHarness _harness;

        public TestDeploymentManager(SiteAdministrationToolHarness harness)
        {
            _harness = harness;
        }

        public async Task ExecuteDeploymentPlanAsync(DeploymentPlan deploymentPlan, DeploymentPlanResult result)
        {
            foreach (var step in deploymentPlan.DeploymentSteps)
            {
                if (step is TestAdminMenuStep)
                {
                    result.Steps.Add(new JsonObject
                    {
                        ["name"] = "AdminMenu",
                        ["data"] = new JsonArray([.. _harness.AdminMenus.Select(menu => menu.DeepClone())]),
                    });
                }
                else if (step is TestExportStep)
                {
                    result.Steps.Add(new JsonObject
                    {
                        ["name"] = "Settings",
                        ["SiteName"] = "Contoso",
                    });

                    await result.FileBuilder.SetFileAsync("media/logo.png", new byte[] { 1, 2, 3, 4 });
                }
            }

            await result.FinalizeAsync();
        }

        public Task<IEnumerable<DeploymentTarget>> GetDeploymentTargetsAsync()
            => Task.FromResult<IEnumerable<DeploymentTarget>>([]);

        public Task ImportDeploymentPackageAsync(Microsoft.Extensions.FileProviders.IFileProvider deploymentPackage)
            => Task.CompletedTask;
    }

    private sealed class RecordingRecipeStepHandler : IRecipeStepHandler
    {
        private readonly SiteAdministrationToolHarness _harness;

        public RecordingRecipeStepHandler(SiteAdministrationToolHarness harness)
        {
            _harness = harness;
        }

        public Task ExecuteAsync(RecipeExecutionContext context)
        {
            _harness.RecipeSteps.Add(context);

            return Task.CompletedTask;
        }
    }
}

/// <summary>
/// The editor view model of the test settings, as a settings driver has one. It is public, as Orchard Core builds a
/// proxy type of each editor view model.
/// </summary>
public class TestSettingsViewModel
{
    [Required]
    public string Title { get; set; }

    public bool ShowBanner { get; set; }

    public int PageSize { get; set; }

    public string ApiKey { get; set; }
}

/// <summary>
/// The editor view model of the cultures driver: the page shows <see cref="Shown"/> and posts the other two.
/// </summary>
public class CulturesSettingsViewModel
{
    public string SupportedCultures { get; set; }

    public string DefaultCulture { get; set; }

    [BindNever]
    public string[] Shown { get; set; }
}
