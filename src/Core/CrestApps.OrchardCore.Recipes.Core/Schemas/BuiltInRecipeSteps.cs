namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Schema for the "feature" recipe step — enables or disables Orchard Core features.
/// </summary>
public sealed class FeatureRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "Feature";

    public ValueTask<JsonSchema> GetSchemaAsync()
    {
        _cached ??= CreateSchema();
        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("feature")),
                ("enable", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))
                    .Description("Feature IDs to enable.")),
                ("disable", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))
                    .Description("Feature IDs to disable.")))
            .Required("name")
            .AdditionalProperties(true)
            .Build();
}

/// <summary>
/// Schema for the "themes" recipe step — sets the site and admin theme.
/// </summary>
public sealed class ThemesRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "Themes";

    public ValueTask<JsonSchema> GetSchemaAsync()
    {
        _cached ??= CreateSchema();
        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("themes")),
                ("site", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Description("The theme ID to use for the front-end site.")),
                ("admin", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Description("The theme ID to use for the admin dashboard.")))
            .Required("name")
            .AdditionalProperties(true)
            .Build();
}

/// <summary>
/// Schema for the "recipes" recipe step — executes other named recipes.
/// </summary>
public sealed class RecipesRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "Recipes";

    public ValueTask<JsonSchema> GetSchemaAsync()
    {
        _cached ??= CreateSchema();
        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("recipes")),
                ("Values", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("executionid", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                            ("name", new JsonSchemaBuilder().Type(SchemaValueType.String)))
                        .Required("executionid", "name")
                        .AdditionalProperties(true))))
            .Required("name", "Values")
            .AdditionalProperties(true)
            .Build();
}

/// <summary>
/// Schema for the "content" recipe step — imports content items.
/// </summary>
public sealed class ContentRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "Content";

    public ValueTask<JsonSchema> GetSchemaAsync()
    {
        _cached ??= CreateSchema();
        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("content")),
                ("data", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("ContentType", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                            ("ContentItemId", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                            ("DisplayText", new JsonSchemaBuilder().Type(SchemaValueType.String)))
                        .Required("ContentType")
                        .AdditionalProperties(true))
                    .MinItems(1)))
            .Required("name", "data")
            .AdditionalProperties(true)
            .Build();
}

/// <summary>
/// Schema for the "media" recipe step — imports media files.
/// </summary>
public sealed class MediaRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "Media";

    public ValueTask<JsonSchema> GetSchemaAsync()
    {
        _cached ??= CreateSchema();
        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("media")),
                ("Files", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("TargetPath", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                            ("SourcePath", new JsonSchemaBuilder()
                                .Type(SchemaValueType.String)
                                .Description("Relative path from the recipe file to the source media.")),
                            ("SourceUrl", new JsonSchemaBuilder()
                                .Type(SchemaValueType.String)
                                .Description("Absolute URL to download the media from.")),
                            ("Base64", new JsonSchemaBuilder()
                                .Type(SchemaValueType.String)
                                .Description("Base64-encoded content of the file.")))
                        .Required("TargetPath")
                        .AdditionalProperties(true))))
            .Required("name", "Files")
            .AdditionalProperties(true)
            .Build();
}

/// <summary>
/// Schema for the "MediaProfiles" recipe step — creates or updates media processing profiles.
/// </summary>
public sealed class MediaProfilesRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "MediaProfiles";

    public ValueTask<JsonSchema> GetSchemaAsync()
    {
        _cached ??= CreateSchema();
        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("MediaProfiles")),
                ("MediaProfiles", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .AdditionalProperties(true)
                    .Description("A dictionary keyed by profile name. Each value is a profile object with Hint, Width, Height, Mode, Format, Quality, BackgroundColor.")))
            .Required("name", "MediaProfiles")
            .AdditionalProperties(true)
            .Build();
}

/// <summary>
/// Schema for the "Roles" recipe step — creates or updates roles with permissions.
/// </summary>
public sealed class RolesRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "Roles";

    public ValueTask<JsonSchema> GetSchemaAsync()
    {
        _cached ??= CreateSchema();
        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("Roles")),
                ("Roles", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                            ("Description", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                            ("Permissions", new JsonSchemaBuilder()
                                .Type(SchemaValueType.Array)
                                .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))),
                            ("PermissionBehavior", new JsonSchemaBuilder()
                                .Type(SchemaValueType.String)
                                .Enum("Add", "Replace", "Remove")
                                .Description("How permissions are merged: Add (default), Replace, or Remove.")))
                        .Required("Name")
                        .AdditionalProperties(true))
                    .MinItems(1)))
            .Required("name", "Roles")
            .AdditionalProperties(true)
            .Build();
}

/// <summary>
/// Schema for the "custom-settings" recipe step — updates custom settings content items stored in site settings.
/// </summary>
public sealed class CustomSettingsRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "custom-settings";

    public ValueTask<JsonSchema> GetSchemaAsync()
    {
        _cached ??= CreateSchema();
        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("custom-settings")))
            .Required("name")
            .MinProperties(2)
            .AdditionalProperties(true)
            .Description("Each additional property is a custom settings content type name with its content item data.")
            .Build();
}

/// <summary>
/// Schema for the "Layers" recipe step — defines display layers with conditional rules.
/// </summary>
public sealed class LayersRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "Layers";

    public ValueTask<JsonSchema> GetSchemaAsync()
    {
        _cached ??= CreateSchema();
        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("Layers")),
                ("Layers", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                            ("Description", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                            ("Rule", new JsonSchemaBuilder()
                                .Type(SchemaValueType.String)
                                .Description("A JavaScript rule expression, e.g. isHomepage().")),
                            ("LayerRule", new JsonSchemaBuilder()
                                .Type(SchemaValueType.Object)
                                .AdditionalProperties(true)
                                .Description("Structured layer rule object.")))
                        .Required("Name")
                        .AdditionalProperties(true))))
            .Required("name", "Layers")
            .AdditionalProperties(true)
            .Build();
}

/// <summary>
/// Schema for the "Queries" recipe step — defines SQL, Lucene, or other query types.
/// </summary>
public sealed class QueriesRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "Queries";

    public ValueTask<JsonSchema> GetSchemaAsync()
    {
        _cached ??= CreateSchema();
        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("Queries")),
                ("Queries", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                            ("Source", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                            ("Schema", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                            ("Template", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                            ("ReturnContentItems", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)))
                        .Required("Name", "Source")
                        .AdditionalProperties(true))
                    .MinItems(1)))
            .Required("name", "Queries")
            .AdditionalProperties(true)
            .Build();
}

/// <summary>
/// Schema for the "Templates" recipe step — creates or updates Liquid templates.
/// </summary>
public sealed class TemplatesRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "Templates";

    public ValueTask<JsonSchema> GetSchemaAsync()
    {
        _cached ??= CreateSchema();
        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("Templates")),
                ("Templates", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .AdditionalProperties(true)
                    .Description("A dictionary keyed by template name. Each value has a Content property with Liquid markup.")))
            .Required("name", "Templates")
            .AdditionalProperties(true)
            .Build();
}

/// <summary>
/// Schema for the "AdminTemplates" recipe step — creates or updates admin Liquid templates.
/// </summary>
public sealed class AdminTemplatesRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "AdminTemplates";

    public ValueTask<JsonSchema> GetSchemaAsync()
    {
        _cached ??= CreateSchema();
        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("AdminTemplates")),
                ("AdminTemplates", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .AdditionalProperties(true)
                    .Description("A dictionary keyed by template name. Each value has a Content property with Liquid markup.")))
            .Required("name", "AdminTemplates")
            .AdditionalProperties(true)
            .Build();
}

/// <summary>
/// Schema for the "ShortcodeTemplates" recipe step — creates or updates shortcode templates.
/// </summary>
public sealed class ShortcodeTemplatesRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "ShortcodeTemplates";

    public ValueTask<JsonSchema> GetSchemaAsync()
    {
        _cached ??= CreateSchema();
        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("ShortcodeTemplates")),
                ("ShortcodeTemplates", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .AdditionalProperties(true)
                    .Description("A dictionary keyed by shortcode name.")))
            .Required("name", "ShortcodeTemplates")
            .AdditionalProperties(true)
            .Build();
}

/// <summary>
/// Schema for the "Placements" recipe step — updates display/editor placement rules.
/// </summary>
public sealed class PlacementsRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "Placements";

    public ValueTask<JsonSchema> GetSchemaAsync()
    {
        _cached ??= CreateSchema();
        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("Placements")),
                ("Placements", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .AdditionalProperties(true)
                    .Description("A dictionary keyed by shape type. Each value is an array of placement objects with 'place' and optional filters.")))
            .Required("name", "Placements")
            .AdditionalProperties(true)
            .Build();
}

/// <summary>
/// Schema for the "AdminMenu" recipe step — creates or updates admin menu structures.
/// </summary>
public sealed class AdminMenuRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "AdminMenu";

    public ValueTask<JsonSchema> GetSchemaAsync()
    {
        _cached ??= CreateSchema();
        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("AdminMenu")),
                ("data", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("Id", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                            ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                            ("Enabled", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)),
                            ("MenuItems", new JsonSchemaBuilder()
                                .Type(SchemaValueType.Array)
                                .Items(new JsonSchemaBuilder()
                                    .Type(SchemaValueType.Object)
                                    .Properties(
                                        ("$type", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                                        ("LinkText", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                                        ("LinkUrl", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                                        ("IconClass", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                                        ("UniqueId", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                                        ("Enabled", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)),
                                        ("Items", new JsonSchemaBuilder()
                                            .Type(SchemaValueType.Array)
                                            .Items(new JsonSchemaBuilder().Type(SchemaValueType.Object).AdditionalProperties(true))))
                                    .Required("$type", "LinkText")
                                    .AdditionalProperties(true))))
                        .Required("Id", "Name", "MenuItems")
                        .AdditionalProperties(true))))
            .Required("name", "data")
            .AdditionalProperties(true)
            .Build();
}

/// <summary>
/// Schema for the "ReplaceContentDefinition" recipe step — replaces content type/part definitions entirely.
/// </summary>
public sealed class ReplaceContentDefinitionRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "ReplaceContentDefinition";

    public ValueTask<JsonSchema> GetSchemaAsync()
    {
        _cached ??= CreateSchema();
        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("ReplaceContentDefinition")),
                ("ContentTypes", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder().Type(SchemaValueType.Object).AdditionalProperties(true))),
                ("ContentParts", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder().Type(SchemaValueType.Object).AdditionalProperties(true))))
            .Required("name")
            .AdditionalProperties(true)
            .Build();
}

/// <summary>
/// Schema for the "DeleteContentDefinition" recipe step — deletes content types/parts by name.
/// </summary>
public sealed class DeleteContentDefinitionRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "DeleteContentDefinition";

    public ValueTask<JsonSchema> GetSchemaAsync()
    {
        _cached ??= CreateSchema();
        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("DeleteContentDefinition")),
                ("ContentTypes", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))),
                ("ContentParts", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))))
            .Required("name")
            .AdditionalProperties(true)
            .Build();
}

/// <summary>
/// Schema for the "deployment" recipe step — configures deployment plans and targets.
/// </summary>
public sealed class DeploymentRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "deployment";

    public ValueTask<JsonSchema> GetSchemaAsync()
    {
        _cached ??= CreateSchema();
        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("deployment")),
                ("Plans", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                            ("DeploymentSteps", new JsonSchemaBuilder()
                                .Type(SchemaValueType.Array)
                                .Items(new JsonSchemaBuilder().Type(SchemaValueType.Object).AdditionalProperties(true))))
                        .Required("Name")
                        .AdditionalProperties(true))))
            .Required("name")
            .AdditionalProperties(true)
            .Build();
}

/// <summary>
/// Schema for the "Sitemaps" recipe step — creates or updates sitemaps.
/// </summary>
public sealed class SitemapsRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "Sitemaps";

    public ValueTask<JsonSchema> GetSchemaAsync()
    {
        _cached ??= CreateSchema();
        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("Sitemaps")),
                ("data", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder().Type(SchemaValueType.Object).AdditionalProperties(true))))
            .Required("name")
            .AdditionalProperties(true)
            .Build();
}

/// <summary>
/// Schema for the "UrlRewriting" recipe step — creates or updates URL rewrite rules.
/// </summary>
public sealed class UrlRewritingRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "UrlRewriting";

    public ValueTask<JsonSchema> GetSchemaAsync()
    {
        _cached ??= CreateSchema();
        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("UrlRewriting")),
                ("Rules", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder().Type(SchemaValueType.Object).AdditionalProperties(true))))
            .Required("name")
            .AdditionalProperties(true)
            .Build();
}

/// <summary>
/// Schema for the "FeatureProfiles" recipe step — defines tenant feature profiles.
/// </summary>
public sealed class FeatureProfilesRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "FeatureProfiles";

    public ValueTask<JsonSchema> GetSchemaAsync()
    {
        _cached ??= CreateSchema();
        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("FeatureProfiles")),
                ("FeatureProfiles", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .AdditionalProperties(true)))
            .Required("name", "FeatureProfiles")
            .AdditionalProperties(true)
            .Build();
}

/// <summary>
/// Schema for the "lucene-index" recipe step — creates or updates Lucene search indexes.
/// </summary>
public sealed class LuceneIndexRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "lucene-index";

    public ValueTask<JsonSchema> GetSchemaAsync()
    {
        _cached ??= CreateSchema();
        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("lucene-index")),
                ("Indices", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder().Type(SchemaValueType.Object).AdditionalProperties(true))))
            .Required("name", "Indices")
            .AdditionalProperties(true)
            .Build();
}

/// <summary>
/// Schema for the "lucene-index-reset" recipe step — resets Lucene indexes.
/// </summary>
public sealed class LuceneIndexResetRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "lucene-index-reset";

    public ValueTask<JsonSchema> GetSchemaAsync()
    {
        _cached ??= CreateSchema();
        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("lucene-index-reset")),
                ("Indices", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))))
            .Required("name", "Indices")
            .AdditionalProperties(true)
            .Build();
}

/// <summary>
/// Schema for the "lucene-index-rebuild" recipe step — rebuilds Lucene indexes.
/// </summary>
public sealed class LuceneIndexRebuildRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "lucene-index-rebuild";

    public ValueTask<JsonSchema> GetSchemaAsync()
    {
        _cached ??= CreateSchema();
        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("lucene-index-rebuild")),
                ("Indices", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))))
            .Required("name", "Indices")
            .AdditionalProperties(true)
            .Build();
}

/// <summary>
/// Schema for the "ElasticIndexSettings" recipe step — creates or updates Elasticsearch indexes.
/// </summary>
public sealed class ElasticIndexSettingsRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "ElasticIndexSettings";

    public ValueTask<JsonSchema> GetSchemaAsync()
    {
        _cached ??= CreateSchema();
        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("ElasticIndexSettings")),
                ("Indices", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder().Type(SchemaValueType.Object).AdditionalProperties(true))))
            .Required("name", "Indices")
            .AdditionalProperties(true)
            .Build();
}

/// <summary>
/// Schema for the "elastic-index-reset" recipe step — resets Elasticsearch indexes.
/// </summary>
public sealed class ElasticIndexResetRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "elastic-index-reset";

    public ValueTask<JsonSchema> GetSchemaAsync()
    {
        _cached ??= CreateSchema();
        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("elastic-index-reset")),
                ("Indices", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))))
            .Required("name", "Indices")
            .AdditionalProperties(true)
            .Build();
}

/// <summary>
/// Schema for the "elastic-index-rebuild" recipe step — rebuilds Elasticsearch indexes.
/// </summary>
public sealed class ElasticIndexRebuildRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "elastic-index-rebuild";

    public ValueTask<JsonSchema> GetSchemaAsync()
    {
        _cached ??= CreateSchema();
        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("elastic-index-rebuild")),
                ("Indices", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))))
            .Required("name", "Indices")
            .AdditionalProperties(true)
            .Build();
}

/// <summary>
/// Schema for the "azureai-index-create" recipe step — creates Azure AI Search indexes.
/// </summary>
public sealed class AzureAIIndexCreateRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "azureai-index-create";

    public ValueTask<JsonSchema> GetSchemaAsync()
    {
        _cached ??= CreateSchema();
        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("azureai-index-create")),
                ("Indices", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder().Type(SchemaValueType.Object).AdditionalProperties(true))))
            .Required("name", "Indices")
            .AdditionalProperties(true)
            .Build();
}

/// <summary>
/// Schema for the "azureai-index-reset" recipe step — resets Azure AI Search indexes.
/// </summary>
public sealed class AzureAIIndexResetRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "azureai-index-reset";

    public ValueTask<JsonSchema> GetSchemaAsync()
    {
        _cached ??= CreateSchema();
        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("azureai-index-reset")),
                ("Indices", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))))
            .Required("name", "Indices")
            .AdditionalProperties(true)
            .Build();
}

/// <summary>
/// Schema for the "azureai-index-rebuild" recipe step — rebuilds Azure AI Search indexes.
/// </summary>
public sealed class AzureAIIndexRebuildRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "azureai-index-rebuild";

    public ValueTask<JsonSchema> GetSchemaAsync()
    {
        _cached ??= CreateSchema();
        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("azureai-index-rebuild")),
                ("Indices", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))))
            .Required("name", "Indices")
            .AdditionalProperties(true)
            .Build();
}

/// <summary>
/// Schema for the "CreateOrUpdateIndexProfile" recipe step — manages index profiles across search providers.
/// </summary>
public sealed class CreateOrUpdateIndexProfileRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "CreateOrUpdateIndexProfile";

    public ValueTask<JsonSchema> GetSchemaAsync()
    {
        _cached ??= CreateSchema();
        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("CreateOrUpdateIndexProfile")),
                ("Indexes", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                            ("ProviderName", new JsonSchemaBuilder().Type(SchemaValueType.String)))
                        .Required("Name", "ProviderName")
                        .AdditionalProperties(true))))
            .Required("name", "Indexes")
            .AdditionalProperties(true)
            .Build();
}

/// <summary>
/// Schema for the "ResetIndex" recipe step — resets search index profiles.
/// </summary>
public sealed class ResetIndexRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "ResetIndex";

    public ValueTask<JsonSchema> GetSchemaAsync()
    {
        _cached ??= CreateSchema();
        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("ResetIndex")),
                ("IncludeAll", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)),
                ("IndexNames", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))))
            .Required("name")
            .AdditionalProperties(true)
            .Build();
}

/// <summary>
/// Schema for the "RebuildIndex" recipe step — rebuilds search index profiles.
/// </summary>
public sealed class RebuildIndexRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "RebuildIndex";

    public ValueTask<JsonSchema> GetSchemaAsync()
    {
        _cached ??= CreateSchema();
        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("RebuildIndex")),
                ("IncludeAll", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)),
                ("IndexNames", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))))
            .Required("name")
            .AdditionalProperties(true)
            .Build();
}

/// <summary>
/// Schema for the "command" recipe step — executes CLI commands.
/// </summary>
public sealed class CommandRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "command";

    public ValueTask<JsonSchema> GetSchemaAsync()
    {
        _cached ??= CreateSchema();
        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("command")),
                ("Commands", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))
                    .MinItems(1)))
            .Required("name", "Commands")
            .AdditionalProperties(true)
            .Build();
}
