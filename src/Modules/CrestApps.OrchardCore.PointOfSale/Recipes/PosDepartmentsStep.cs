using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Core.Recipes;
using CrestApps.OrchardCore.PointOfSale.Models;
using CrestApps.OrchardCore.PointOfSale.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Recipes.Models;

namespace CrestApps.OrchardCore.PointOfSale.Recipes;

/// <summary>
/// Adds or updates the departments registers ring amounts up in, from a <c>PosDepartments</c> recipe step. A department
/// is matched by its <c>ItemId</c>, then by its name, so a second run changes it instead of adding another, and it's
/// checked the way its admin screen checks it.
/// </summary>
/// <remarks>
/// <code>
/// { "name": "PosDepartments", "PosDepartments": [ { "ItemId": "...", "Name": "Candy", "Number": "4",
///   "TaxCategoryCode": "Candy", "Color": "#c2185b", "MaximumAmount": 50, "Position": 4 } ] }
/// </code>
/// Import departments before the registers whose keys name them.
/// </remarks>
internal sealed class PosDepartmentsStep : CatalogStoreRecipeStep<PosDepartment>
{
    public const string EntriesName = "PosDepartments";

    private readonly IPosDepartmentStore _store;

    internal readonly IStringLocalizer S;

    public PosDepartmentsStep(
        IPosDepartmentStore store,
        IStringLocalizer<PosDepartmentsStep> stringLocalizer)
        : base(PosConstants.Recipes.Departments, EntriesName, store)
    {
        _store = store;
        S = stringLocalizer;
    }

    protected override async ValueTask<CatalogRecipeLookup<PosDepartment>> FindExistingAsync(JsonObject token, RecipeExecutionContext context)
    {
        var name = token[nameof(PosDepartment.Name)]?.GetValue<string>()?.Trim();

        if (string.IsNullOrEmpty(name))
        {
            return CatalogRecipeLookup<PosDepartment>.NotFound;
        }

        var department = (await _store.GetAllAsync()).FirstOrDefault(entry => string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase));

        return department is null ? CatalogRecipeLookup<PosDepartment>.NotFound : CatalogRecipeLookup<PosDepartment>.Found(department);
    }

    protected override void Preparing(PosDepartment entry, JsonObject token)
        => PosDepartments.Normalize(entry);

    protected override async ValueTask<ValidationResultDetails> ValidateAsync(PosDepartment entry)
    {
        var result = new ValidationResultDetails();

        foreach (var problem in PosDepartments.Validate(entry, await _store.GetAllAsync(), S))
        {
            result.Fail(new ValidationResult(problem));
        }

        return result;
    }
}
