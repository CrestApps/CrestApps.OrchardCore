using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.A2A.Models;
using CrestApps.Core.AI.A2A.Services;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Core.Handlers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.A2A.Handlers;

internal sealed class A2AConnectionHandler : CatalogEntryAuditHandlerBase<A2AConnection>
{
    private readonly IA2AAgentCardCacheService _cacheService;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="A2AConnectionHandler"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="cacheService">The cache service.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public A2AConnectionHandler(
        IHttpContextAccessor httpContextAccessor,
        IA2AAgentCardCacheService cacheService,
        IClock clock,
        IStringLocalizer<A2AConnectionHandler> stringLocalizer)
        : base(httpContextAccessor, clock)
    {
        _cacheService = cacheService;
        S = stringLocalizer;
    }

    protected override void SetCreated(A2AConnection entry, DateTime createdUtc, string ownerId, string author, bool hasUser)
    {
        entry.CreatedUtc = createdUtc;

        if (hasUser)
        {
            entry.OwnerId = ownerId;
            entry.Author = author;
        }
    }

    public override Task InitializingAsync(InitializingContext<A2AConnection> context, CancellationToken cancellationToken = default)
        => PopulateAsync(context.Model, context.Data, true);

    public override Task UpdatingAsync(UpdatingContext<A2AConnection> context, CancellationToken cancellationToken = default)
    {
        StampModified(context.Model);

        return PopulateAsync(context.Model, context.Data, false);
    }

    public override Task UpdatedAsync(UpdatedContext<A2AConnection> context, CancellationToken cancellationToken = default)
    {
        _cacheService.Invalidate(context.Model.ItemId);

        return Task.CompletedTask;
    }

    public override Task DeletedAsync(DeletedContext<A2AConnection> context, CancellationToken cancellationToken = default)
    {
        _cacheService.Invalidate(context.Model.ItemId);

        return Task.CompletedTask;
    }

    public override Task ValidatingAsync(ValidatingContext<A2AConnection> context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(context.Model.DisplayText))
        {
            context.Result.Fail(new ValidationResult(S["The Title is required."], [nameof(A2AConnection.DisplayText)]));
        }

        if (string.IsNullOrEmpty(context.Model.Endpoint))
        {
            context.Result.Fail(new ValidationResult(S["The Endpoint is required."], [nameof(A2AConnection.Endpoint)]));
        }
        else if (!Uri.TryCreate(context.Model.Endpoint, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            context.Result.Fail(new ValidationResult(S["The Endpoint must be a valid HTTP or HTTPS URL."], [nameof(A2AConnection.Endpoint)]));
        }

        return Task.CompletedTask;
    }

    private Task PopulateAsync(A2AConnection connection, JsonNode data, bool isNew)
    {
        if (isNew)
        {
            StampCreated(connection);
        }

        var displayText = data[nameof(A2AConnection.DisplayText)]?.ToString();

        if (!string.IsNullOrWhiteSpace(displayText))
        {
            connection.DisplayText = displayText;
        }

        var endpoint = data[nameof(A2AConnection.Endpoint)]?.ToString();

        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            connection.Endpoint = endpoint;
        }

        var properties = data[nameof(A2AConnection.Properties)]?.AsObject();

        if (properties is not null)
        {
            connection.Properties ??= new Dictionary<string, object>();

            foreach (var prop in properties)
            {
                connection.Properties[prop.Key] = prop.Value?.DeepClone();
            }
        }

        return Task.CompletedTask;
    }
}
