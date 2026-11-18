using System.Reflection;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Omnichannel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;
using OrchardCore.Workflows.Options;
using ModuleStartup = OrchardCore.Modules.StartupBase;
using StartupFeature = OrchardCore.Modules.FeatureAttribute;

namespace CrestApps.OrchardCore.Tests.Omnichannel;

/// <summary>
/// A site with Workflows and the omnichannel activities but without AI opens the workflow editor: no workflow activity
/// editor the module registers then needs an AI service.
/// </summary>
public sealed class OmnichannelWorkflowsWithoutAITests
{
    [Fact]
    public void WithActivitiesAndWorkflowsButNoAI_NoActivityEditorNeedsAI()
    {
        // Arrange
        var assembly = typeof(global::CrestApps.OrchardCore.Omnichannel.Managements.CrmStartup).Assembly;
        var enabled = new HashSet<string>(StringComparer.Ordinal)
        {
            assembly.GetName().Name,
            OmnichannelConstants.Features.Area,
            OmnichannelConstants.Features.Activities,
            OmnichannelConstants.Features.Crm,
            OrchardCoreFeatureIds.Workflows,
        };
        var services = new ServiceCollection();

        // Act: the module's startups that load with those features, as the shell would choose them.
        foreach (var startup in Startups(assembly, enabled))
        {
            startup.ConfigureServices(services);
        }

        var aiTypes = new[] { typeof(CrestApps.Core.AI.Profiles.IAIProfileStore), typeof(CrestApps.Core.AI.Profiles.IAIProfileManager) };
        var options = services.BuildServiceProvider().GetRequiredService<IOptions<WorkflowOptions>>().Value;
        var drivers = assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(global::OrchardCore.Workflows.Activities.IActivity).IsAssignableFrom(type))
            .Select(options.GetActivityRegistration)
            .Where(registration => registration is not null)
            .SelectMany(registration => registration.DriverTypes)
            .ToList();
        var needingAI = drivers
            .Where(type => type.GetConstructors().Any(constructor => constructor.GetParameters().Any(parameter => aiTypes.Contains(parameter.ParameterType))))
            .Select(type => type.Name)
            .ToList();

        // Assert
        Assert.Empty(needingAI);
        Assert.Contains(drivers, type => type.Name == "LeadConvertedEventDisplayDriver");
    }

    [Fact]
    public void TheCreateAIActivityTask_LoadsOnlyWithAI()
    {
        var attribute = typeof(global::CrestApps.OrchardCore.Omnichannel.Managements.AIActivityWorkflowsStartup).GetCustomAttribute<RequireFeaturesAttribute>();

        Assert.Contains(AIConstants.Feature.Area, attribute.RequiredFeatureNames);
        Assert.Contains(OrchardCoreFeatureIds.Workflows, attribute.RequiredFeatureNames);
    }

    private static IEnumerable<ModuleStartup> Startups(Assembly assembly, HashSet<string> enabled)
    {
        var localizers = new ServiceCollection()
            .AddSingleton(typeof(IStringLocalizer<>), typeof(PassThroughLocalizer<>))
            .AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance)
            .BuildServiceProvider();

        foreach (var type in assembly.GetTypes().Where(type => !type.IsAbstract && typeof(ModuleStartup).IsAssignableFrom(type)))
        {
            var feature = type.GetCustomAttribute<StartupFeature>()?.FeatureName ?? assembly.GetName().Name;
            var required = type.GetCustomAttribute<RequireFeaturesAttribute>()?.RequiredFeatureNames ?? [];

            if (enabled.Contains(feature) && required.All(enabled.Contains))
            {
                yield return (ModuleStartup)ActivatorUtilities.CreateInstance(localizers, type);
            }
        }
    }

    private sealed class PassThroughLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
