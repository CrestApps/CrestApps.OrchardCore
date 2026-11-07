using System.Text.Json.Nodes;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Models;
using Microsoft.AspNetCore.Http;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Core.Handlers;

/// <summary>
/// The handler a catalog-backed configuration entry starts from: it binds the JSON a recipe or deployment plan gives
/// the manager onto the entry, and stamps when the entry was created and changed, and by whom.
/// </summary>
/// <remarks>
/// A derived handler is left with what is specific to its entry, which is usually only its validation.
/// </remarks>
/// <typeparam name="T">The catalog entry type.</typeparam>
public abstract class AuditedCatalogEntryHandlerBase<T> : CatalogEntryAuditHandlerBase<T>
    where T : CatalogItem, IAuditedModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AuditedCatalogEntryHandlerBase{T}"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The accessor that gives the user making the change.</param>
    /// <param name="clock">The clock the created and modified dates are read from.</param>
    protected AuditedCatalogEntryHandlerBase(
        IHttpContextAccessor httpContextAccessor,
        IClock clock)
        : base(httpContextAccessor, clock)
    {
    }

    /// <inheritdoc/>
    public override Task InitializingAsync(InitializingContext<T> context, CancellationToken cancellationToken = default)
    {
        Populate(context.Model, context.Data);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task InitializedAsync(InitializedContext<T> context, CancellationToken cancellationToken = default)
    {
        StampCreated(context.Model);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task UpdatingAsync(UpdatingContext<T> context, CancellationToken cancellationToken = default)
    {
        StampModified(context.Model);

        Populate(context.Model, context.Data);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    protected override void SetCreated(T entry, DateTime createdUtc, string ownerId, string author, bool hasUser)
    {
        entry.CreatedUtc = createdUtc;

        if (hasUser)
        {
            entry.OwnerId = ownerId;
            entry.Author = author;
        }
    }

    /// <summary>
    /// Applies the members the given JSON names onto the entry. The editor gives no JSON, so nothing is applied then.
    /// </summary>
    /// <param name="entry">The entry to populate.</param>
    /// <param name="data">The JSON a recipe or deployment plan gave the manager, if any.</param>
    protected virtual void Populate(T entry, JsonNode data)
        => CatalogDeploymentSerializer.Populate(entry, data);
}
