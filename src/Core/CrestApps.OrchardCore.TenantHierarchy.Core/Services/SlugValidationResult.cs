namespace CrestApps.OrchardCore.TenantHierarchy.Core.Services;

/// <summary>
/// Describes the outcome of validating a tenant slug.
/// </summary>
public enum SlugValidationResult
{
    /// <summary>
    /// The slug is valid.
    /// </summary>
    Valid,

    /// <summary>
    /// The slug is empty.
    /// </summary>
    Empty,

    /// <summary>
    /// The slug is shorter than <see cref="TenantHierarchyNaming.MinSlugLength"/>.
    /// </summary>
    TooShort,

    /// <summary>
    /// The slug is longer than <see cref="TenantHierarchyNaming.MaxSlugLength"/>.
    /// </summary>
    TooLong,

    /// <summary>
    /// The slug is not a DNS label of lowercase letters, digits and inner hyphens.
    /// </summary>
    InvalidCharacters,

    /// <summary>
    /// The slug is reserved.
    /// </summary>
    Reserved,
}
