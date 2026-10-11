namespace CrestApps.OrchardCore.TenantHierarchy.Models;

/// <summary>
/// Represents a child tenant in the registry of its parent. The registry lives in the parent database and is the only
/// way a request names a child tenant: requests carry <see cref="EntryId"/>, never a tenant name.
/// </summary>
public sealed class ChildTenantEntry
{
    /// <summary>
    /// Gets or sets the document identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the random identifier of the entry.
    /// </summary>
    public string EntryId { get; set; }

    /// <summary>
    /// Gets or sets the stable tenant identifier of the child tenant.
    /// </summary>
    public string TenantId { get; set; }

    /// <summary>
    /// Gets or sets the opaque tenant name of the child tenant.
    /// </summary>
    public string TenantName { get; set; }

    /// <summary>
    /// Gets or sets the slug of the child tenant. It is the first label of its host.
    /// </summary>
    public string Slug { get; set; }

    /// <summary>
    /// Gets or sets the display name of the child tenant.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the description of the child tenant.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the host of the child tenant.
    /// </summary>
    public string Host { get; set; }

    /// <summary>
    /// Gets or sets the setup recipe the child tenant was created with.
    /// </summary>
    public string RecipeName { get; set; }

    /// <summary>
    /// Gets or sets the life cycle status of the child tenant.
    /// </summary>
    public ChildTenantStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the last error, when <see cref="Status"/> is <see cref="ChildTenantStatus.Failed"/>.
    /// </summary>
    public string Error { get; set; }

    /// <summary>
    /// Gets or sets the database strategy the child tenant was created with.
    /// </summary>
    public ChildDatabaseStrategy DatabaseStrategy { get; set; }

    /// <summary>
    /// Gets or sets the name of the database pool the child tenant was created in.
    /// </summary>
    public string DatabasePool { get; set; }

    /// <summary>
    /// Gets or sets the name of the database, schema or table prefix the provisioner created for the child tenant.
    /// </summary>
    public string ProvisionedResource { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the bootstrap administrator created by setup. It is disabled after setup.
    /// </summary>
    public string BootstrapUserId { get; set; }

    /// <summary>
    /// Gets or sets when the entry was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the parent user who created the entry.
    /// </summary>
    public string CreatedById { get; set; }

    /// <summary>
    /// Gets or sets the name of the parent user who created the entry.
    /// </summary>
    public string CreatedByName { get; set; }

    /// <summary>
    /// Gets or sets when a child tenant pending removal is removed.
    /// </summary>
    public DateTime? RetainUntilUtc { get; set; }
}
