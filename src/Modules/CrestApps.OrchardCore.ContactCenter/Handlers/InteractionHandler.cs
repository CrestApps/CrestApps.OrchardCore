using System.Security.Claims;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.Core.ContactCenter.Models;
using Microsoft.AspNetCore.Http;
using OrchardCore;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Handlers;

/// <summary>
/// Maintains audit timestamps, the correlation identifier, and creator metadata for interactions.
/// </summary>
internal sealed class InteractionHandler : CatalogEntryHandlerBase<Interaction>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="InteractionHandler"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor used to resolve the current user.</param>
    /// <param name="timeProvider">The time provider used to stamp audit times.</param>
    public InteractionHandler(
        IHttpContextAccessor httpContextAccessor,
        TimeProvider timeProvider)
    {
        _httpContextAccessor = httpContextAccessor;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc/>
    public override Task InitializedAsync(InitializedContext<Interaction> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _timeProvider.GetUtcNow().UtcDateTime;

        if (string.IsNullOrEmpty(context.Model.CorrelationId))
        {
            context.Model.CorrelationId = IdGenerator.GenerateId();
        }

        var user = _httpContextAccessor.HttpContext?.User;

        if (user is not null)
        {
            context.Model.CreatedById = user.FindFirstValue(ClaimTypes.NameIdentifier);
            context.Model.CreatedByUserName = user.Identity?.Name;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task UpdatingAsync(UpdatingContext<Interaction> context, CancellationToken cancellationToken = default)
    {
        context.Model.ModifiedUtc = _timeProvider.GetUtcNow().UtcDateTime;

        return Task.CompletedTask;
    }
}
