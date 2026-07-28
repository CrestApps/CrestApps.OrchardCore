namespace CrestApps.OrchardCore.Core.Configuration;

/// <summary>
/// Describes how a configuration catalog appears in recipes and deployment plans.
/// </summary>
public sealed class ConfigurationCatalogDescriptor
{
    /// <summary>
    /// Gets or sets the identifier of the group the catalog belongs to, which determines the deployment step that exports it.
    /// </summary>
    public string Group { get; set; }

    /// <summary>
    /// Gets or sets the recipe step name that carries the catalog's entries.
    /// </summary>
    public string StepName { get; set; }

    /// <summary>
    /// Gets or sets the name of the property inside the recipe step that holds the array of entries.
    /// </summary>
    public string CollectionName { get; set; }

    /// <summary>
    /// Gets or sets the relative import order of the catalog, lowest first.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Gets or sets the members that identify an entry when the destination does not know its identifier.
    /// </summary>
    /// <remarks>
    /// An entry is reconciled with the copy the destination already had by identifier first and by identity second.
    /// Most catalogs identify an entry by its name or its display text and need nothing here. A catalog whose entries
    /// carry neither - a setting that belongs to a subject type, an action that belongs to a subject type and a
    /// disposition - has to say which members make one of its entries the same entry, or every replay would create a
    /// second copy of configuration the destination already had and the module would act on both.
    /// </remarks>
    public string[] IdentityProperties { get; set; }
}
