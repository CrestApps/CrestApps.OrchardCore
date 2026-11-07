using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.Deployment;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Tests.Doubles;

/// <summary>
/// Exports a deployment step and replays recipe steps, for round-trip tests of recipe and deployment steps.
/// </summary>
public static class RecipeRoundTrip
{
    /// <summary>
    /// Runs a deployment source for its step and returns the one recipe step it writes, read back from its JSON text.
    /// </summary>
    public static async Task<JsonObject> ExportAsync(IDeploymentSource source, DeploymentStep step)
    {
        var result = new DeploymentPlanResult(Mock.Of<IFileBuilder>(), new RecipeDescriptor());

        await source.ProcessDeploymentStepAsync(step, result);

        return JsonNode.Parse(Assert.Single(result.Steps).ToJsonString()).AsObject();
    }

    /// <summary>
    /// Runs a recipe step handler on a copy of the step and returns the errors it reported.
    /// </summary>
    public static async Task<IList<string>> RunAsync(IRecipeStepHandler handler, JsonObject step)
    {
        var context = new RecipeExecutionContext
        {
            Name = step["name"].GetValue<string>(),
            Step = step.DeepClone().AsObject(),
        };

        await handler.ExecuteAsync(context);

        return context.Errors;
    }

    /// <summary>
    /// Creates a localizer that returns each text as written, with its arguments.
    /// </summary>
    public static IStringLocalizer<T> Localizer<T>()
        => new PassThroughLocalizer<T>();

    private sealed class PassThroughLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name]
            => new(name, name);

        public LocalizedString this[string name, params object[] arguments]
            => new(name, string.Format(CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
            => [];
    }
}
