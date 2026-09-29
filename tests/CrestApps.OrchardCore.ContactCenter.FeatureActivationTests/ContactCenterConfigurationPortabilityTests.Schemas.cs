using System.Reflection;
using System.Text.Json;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Messaging;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telnyx;
using Json.Schema;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.ContactCenter.FeatureActivationTests;

/// <summary>
/// Proves that what a deployment plan exports is described by the schema a recipe author, or an AI agent writing a
/// recipe, is given for the same step.
/// </summary>
/// <remarks>
/// The schema is the only description of a step anyone outside the code sees. When an exported plan does not validate
/// against it, a tool that checks a recipe before importing it refuses a plan this same platform produced, and when a
/// registered step has no schema at all, the step is invisible to everyone who builds recipes from the schemas.
/// </remarks>
public sealed partial class ContactCenterConfigurationPortabilityTests
{
    /// <summary>
    /// The assemblies whose recipe steps this suite holds to having a schema: the Contact Center, Omnichannel and phone
    /// system modules.
    /// </summary>
    private static readonly string[] _schemaOwningAssemblyPrefixes =
    [
        "CrestApps.OrchardCore.ContactCenter",
        "CrestApps.OrchardCore.Omnichannel",
        "CrestApps.OrchardCore.Telephony",
        "CrestApps.OrchardCore.Telnyx",
        "CrestApps.OrchardCore.Asterisk",
        "CrestApps.OrchardCore.WebSockets",
    ];

    /// <summary>
    /// Every Contact Center, Omnichannel and phone system feature that registers a recipe step, so the audit sees them
    /// all at once.
    /// </summary>
    private static readonly string[] _allRecipeFeatures =
    [
        ContactCenterConstants.Feature.Area,
        ContactCenterConstants.Feature.Agents,
        ContactCenterConstants.Feature.AgentEntitlements,
        ContactCenterConstants.Feature.BusinessHours,
        ContactCenterConstants.Feature.Queues,
        ContactCenterConstants.Feature.InboundVoice,
        ContactCenterConstants.Feature.Dialer,
        ContactCenterConstants.Feature.Recording,
        ContactCenterConstants.Feature.SecureCapture,
        OmnichannelConstants.Features.Activities,
        MessagingConstants.Feature.Workspace,
        MessagingConstants.Feature.Sms,
        TelephonyConstants.Feature.Area,
        TelephonyConstants.Feature.SoftPhone,
        TelnyxConstants.Feature.Area,
        TelnyxConstants.Feature.Sms,
        "CrestApps.OrchardCore.Asterisk",
        DeploymentFeatureId,
        RecipesFeatureId,
        RecipeSchemasFeatureId,
    ];

    [Theory]
    [InlineData(ContactCenterGroup)]
    [InlineData(OmnichannelGroup)]
    [InlineData(MessagingGroup)]
    [InlineData(TelephonyGroup)]
    public async Task ExportedConfiguration_IsValidAgainstTheRecipeStepSchema(string group)
    {
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();

        var source = await host.CreateTenantAsync(new ContactCenterTenantProfile
        {
            Id = $"configuration-schema-{group.ToLowerInvariant()}",
            ProviderProfile = "none",
            Features = _configurationFeatures,
        });

        await SeedAsync(host, source, group);

        var plan = await ExportAsync(host, source, group);

        var problems = await host.ExecuteInTenantScopeAsync(source, async serviceProvider =>
        {
            var schemas = serviceProvider.GetServices<IRecipeStep>().ToDictionary(schema => schema.Name, StringComparer.Ordinal);
            var failures = new List<string>();

            foreach (var step in plan)
            {
                var name = step["name"].GetValue<string>();

                if (!schemas.TryGetValue(name, out var schema))
                {
                    failures.Add($"{name}: no recipe step schema is registered for the step.");

                    continue;
                }

                failures.AddRange(await EvaluateAsync(schema, step, name));
            }

            return failures;
        });

        Assert.True(
            problems.Count == 0,
            Describe(
                $"The {group} deployment plan does not validate against the recipe step schemas, so a tool that checks a " +
                "recipe before importing it refuses a plan this platform exported.",
                "Describe every exported member in the step's IRecipeStep schema with the type it is exported as.",
                problems));
    }

    [Fact]
    public async Task EveryRecipeStepTheseModulesRegister_HasASchema()
    {
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();

        var tenant = await host.CreateTenantAsync(new ContactCenterTenantProfile
        {
            Id = "configuration-schema-audit",
            ProviderProfile = "none",
            Features = _allRecipeFeatures,
        });

        var (stepNames, schemaNames) = await host.ExecuteInTenantScopeAsync(tenant, serviceProvider =>
        {
            var names = serviceProvider.GetServices<IRecipeStepHandler>()
                .Where(handler => _schemaOwningAssemblyPrefixes.Any(prefix => handler.GetType().Assembly.GetName().Name.StartsWith(prefix, StringComparison.Ordinal)))
                .Select(GetStepName)
                .Where(name => name is not null)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
            var schemas = serviceProvider.GetServices<IRecipeStep>().Select(schema => schema.Name).ToHashSet(StringComparer.Ordinal);

            return Task.FromResult((names, schemas));
        });

        Assert.True(stepNames.Length >= 20, $"Only {stepNames.Length} recipe steps were found, so the audit would pass without proving anything.");

        var missing = stepNames.Where(name => !schemaNames.Contains(name)).ToArray();

        Assert.True(
            missing.Length == 0,
            Describe(
                "A recipe step is registered without a schema, so it is invisible to everyone who builds recipes from the schemas.",
                "Add an IRecipeStep schema under src/Core/CrestApps.OrchardCore.Recipes.Core/Schemas/Steps and register it under the same feature as the step.",
                missing));
    }

    private static string GetStepName(IRecipeStepHandler handler)
    {
        for (var type = handler.GetType(); type is not null; type = type.BaseType)
        {
            if (type == typeof(NamedRecipeStepHandler))
            {
                return type.GetField("StepName", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?.GetValue(handler) as string;
            }
        }

        return null;
    }

    private static async Task<IEnumerable<string>> EvaluateAsync(IRecipeStep schema, System.Text.Json.Nodes.JsonObject step, string name)
    {
        var built = await schema.GetSchemaAsync();

        using var document = JsonDocument.Parse(step.ToJsonString());

        var result = built.Evaluate(document.RootElement, new EvaluationOptions
        {
            OutputFormat = OutputFormat.List,
        });

        if (result.IsValid)
        {
            return [];
        }

        return (result.Details ?? [])
            .Where(detail => !detail.IsValid && detail.Errors is not null)
            .SelectMany(detail => detail.Errors.Select(error => $"{name}{detail.InstanceLocation}: {error.Value}"))
            .DefaultIfEmpty($"{name}: the step is not valid against its schema.");
    }
}
