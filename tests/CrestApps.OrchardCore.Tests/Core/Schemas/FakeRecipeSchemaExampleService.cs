using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas;

namespace CrestApps.OrchardCore.Tests.Core.Schemas;

/// <summary>
/// A configurable <see cref="IRecipeSchemaExampleService"/> used to assert that schema definitions surface
/// live tenant values as non-restrictive suggestions.
/// </summary>
public sealed class FakeRecipeSchemaExampleService : IRecipeSchemaExampleService
{
    private readonly RecipeSchemaExamples _examples;

    public FakeRecipeSchemaExampleService()
        : this(RecipeSchemaExamples.Empty)
    {
    }

    public FakeRecipeSchemaExampleService(RecipeSchemaExamples examples)
    {
        _examples = examples ?? RecipeSchemaExamples.Empty;
    }

    public ValueTask<RecipeSchemaExamples> GetExamplesAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult(_examples);
}
