using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;
using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Base class for site settings schema definitions that caches the built schema.
/// </summary>
public abstract class SiteSettingsSchemaBase : ISiteSettingsSchemaDefinition
{
    private JsonSchemaBuilder _cache;

    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Builds and caches the schema for the contributed settings property.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public ValueTask<JsonSchemaBuilder> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cache ??= BuildSchemaCore();

        return ValueTask.FromResult(_cache);
    }

    /// <summary>
    /// Builds the schema for this site settings definition.
    /// Override to provide a detailed schema with property definitions.
    /// </summary>
    protected virtual JsonSchemaBuilder BuildSchemaCore()
        => OpenObject();

    /// <summary>
    /// Creates an open object schema that allows additional properties.
    /// </summary>
    protected static JsonSchemaBuilder OpenObject()
        => RecipeStepSchemaBuilders.Object(additionalProperties: true);

    /// <summary>
    /// Creates the schema of a credential that Orchard Core stores protected with ASP.NET Core Data Protection.
    /// </summary>
    /// <param name="credential">The credential, for example "SMTP password".</param>
    /// <param name="secretNameProperty">The name of the sibling property that references a secret instead.</param>
    protected static JsonSchemaBuilder ProtectedCredential(string credential, string secretNameProperty)
        => RecipeStepSchemaBuilders.String()
            .Description($"The {credential}, protected with the tenant's ASP.NET Core Data Protection keys. A clear value does not work. In recipes, set '{secretNameProperty}' instead.");

    /// <summary>
    /// Creates the schema of a property that references a secret of the Orchard Core Secrets module.
    /// </summary>
    /// <param name="credential">The credential, for example "SMTP password".</param>
    protected static JsonSchemaBuilder SecretName(string credential)
        => RecipeStepSchemaBuilders.String()
            .Description($"The name of the secret (OrchardCore.Secrets feature) that holds the {credential}. When it is set, it takes precedence over the protected value. Create the secret with the 'Secrets' recipe step.");
}
