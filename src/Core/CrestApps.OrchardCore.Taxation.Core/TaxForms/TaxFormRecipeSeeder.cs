using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Extensions;

namespace CrestApps.OrchardCore.Taxation.Core.TaxForms;

/// <summary>
/// Adds the tax forms of a module's tax forms recipe (its Recipes folder) that a site doesn't have yet, by name, without
/// running the recipe's other steps: a feature's migration uses it, so turning the feature on, or updating a site that
/// had it on, gives the forms, and the same recipe stays on the Recipes page to put them back. Payroll's country
/// features seed their forms with it, and Tax Returns its generic return.
/// </summary>
public sealed class TaxFormRecipeSeeder
{
    private readonly ITaxFormManager _forms;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly IExtensionManager _extensionManager;
    private readonly ILogger _logger;

    public TaxFormRecipeSeeder(
        ITaxFormManager forms,
        IHostEnvironment hostEnvironment,
        IExtensionManager extensionManager,
        ILogger<TaxFormRecipeSeeder> logger)
    {
        _forms = forms;
        _hostEnvironment = hostEnvironment;
        _extensionManager = extensionManager;
        _logger = logger;
    }

    /// <summary>
    /// Gets the tax forms a recipe adds: the entries of its TaxForms steps.
    /// </summary>
    /// <param name="recipe">The recipe.</param>
    /// <returns>The forms.</returns>
    public static IEnumerable<JsonObject> Forms(JsonNode recipe)
        => (recipe?["steps"] as JsonArray ?? [])
            .OfType<JsonObject>()
            .Where(step => string.Equals(step["name"]?.GetValue<string>(), TaxationConstants.TaxFormsRecipeStepName, StringComparison.OrdinalIgnoreCase))
            .SelectMany(step => (step["Forms"] as JsonArray ?? []).OfType<JsonObject>());

    /// <summary>
    /// Adds the forms of a module's recipe that the site doesn't have yet.
    /// </summary>
    /// <param name="moduleId">The module whose Recipes folder has the recipe, such as CrestApps.OrchardCore.Payroll.</param>
    /// <param name="recipeFileName">The recipe's file name in the module's Recipes folder.</param>
    /// <returns>The forms added.</returns>
    public async Task<IReadOnlyList<TaxForm>> SeedAsync(string moduleId, string recipeFileName)
    {
        ArgumentException.ThrowIfNullOrEmpty(moduleId);
        ArgumentException.ThrowIfNullOrEmpty(recipeFileName);

        // The module's files, embedded in it, are under its sub path of the content root, as Orchard's recipe migrator
        // reads a migration recipe.
        var extension = _extensionManager.GetExtension(moduleId);
        var file = extension?.Exists == true
            ? _hostEnvironment.ContentRootFileProvider.GetFileInfo(Path.Combine(extension.SubPath, "Recipes", recipeFileName))
            : null;

        if (file?.Exists != true)
        {
            _logger.LogWarning("The tax forms recipe '{Recipe}' of '{Module}' was not found; no tax form was added.", recipeFileName, moduleId);

            return [];
        }

        JsonNode recipe;

        await using (var stream = file.CreateReadStream())
        {
            recipe = await JsonNode.ParseAsync(stream);
        }

        return await SeedAsync(_forms, Forms(recipe), _logger);
    }

    /// <summary>
    /// Adds the forms that a site doesn't have yet, by name; a form that fails its checks is logged and left out.
    /// </summary>
    /// <param name="manager">The tax form manager.</param>
    /// <param name="forms">The forms, as a recipe writes them.</param>
    /// <param name="logger">The logger.</param>
    /// <returns>The forms added.</returns>
    public static async Task<IReadOnlyList<TaxForm>> SeedAsync(ITaxFormManager manager, IEnumerable<JsonObject> forms, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(forms);

        var added = new List<TaxForm>();

        foreach (var data in forms)
        {
            var name = data["Name"]?.GetValue<string>();

            if (string.IsNullOrWhiteSpace(name) || await manager.FindByNameAsync(name) is not null)
            {
                continue;
            }

            var form = await manager.NewAsync(data.DeepClone());
            var validation = await manager.ValidateAsync(form);

            if (!validation.Succeeded)
            {
                logger?.LogError("The tax form '{Form}' was not added: {Errors}", name, string.Join(" ", validation.Errors.Select(error => error.ErrorMessage)));

                continue;
            }

            await manager.CreateAsync(form);
            added.Add(form);
        }

        return added;
    }
}
