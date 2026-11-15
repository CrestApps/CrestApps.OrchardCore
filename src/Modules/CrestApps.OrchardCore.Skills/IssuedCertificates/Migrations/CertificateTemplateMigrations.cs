using OrchardCore.Data.Migration;
using OrchardCore.Recipes;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Skills.IssuedCertificates.Migrations;

/// <summary>
/// Creates the Certificate Template content type.
/// </summary>
internal sealed class CertificateTemplateMigrations : DataMigration
{
    private readonly IRecipeMigrator _recipeMigrator;

    public CertificateTemplateMigrations(IRecipeMigrator recipeMigrator)
    {
        _recipeMigrator = recipeMigrator;
    }

    public async Task<int> CreateAsync()
    {
        await _recipeMigrator.ExecuteAsync($"certificate-template{RecipesConstants.RecipeExtension}", this);

        return 1;
    }
}
