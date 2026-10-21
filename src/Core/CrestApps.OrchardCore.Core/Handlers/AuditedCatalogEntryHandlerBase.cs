using System.Security.Claims;
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
public abstract class AuditedCatalogEntryHandlerBase<T> : CatalogEntryHandlerBase<T>
    where T : CatalogItem, IAuditedModel
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuditedCatalogEntryHandlerBase{T}"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The accessor that gives the user making the change.</param>
    /// <param name="clock">The clock the created and modified dates are read from.</param>
    protected AuditedCatalogEntryHandlerBase(
        IHttpContextAccessor httpContextAccessor,
        IClock clock)
    {
        _httpContextAccessor = httpContextAccessor;
        Clock = clock;
    }

    /// <summary>
    /// Gets the clock the created and modified dates are read from.
    /// </summary>
    protected IClock Clock { get; }

    /// <inheritdoc/>
    public override Task InitializingAsync(InitializingContext<T> context, CancellationToken cancellationToken = default)
    {
        Populate(context.Model, context.Data);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task InitializedAsync(InitializedContext<T> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = Clock.UtcNow;

        var user = _httpContextAccessor.HttpContext?.User;

        if (user is not null)
        {
            context.Model.OwnerId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            context.Model.Author = user.Identity?.Name;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task UpdatingAsync(UpdatingContext<T> context, CancellationToken cancellationToken = default)
    {
        context.Model.ModifiedUtc = Clock.UtcNow;

        Populate(context.Model, context.Data);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Applies the members the given JSON names onto the entry. The editor gives no JSON, so nothing is applied then.
    /// </summary>
    /// <param name="entry">The entry to populate.</param>
    /// <param name="data">The JSON a recipe or deployment plan gave the manager, if any.</param>
    protected virtual void Populate(T entry, JsonNode data)
        => CatalogDeploymentSerializer.Populate(entry, data);
}
