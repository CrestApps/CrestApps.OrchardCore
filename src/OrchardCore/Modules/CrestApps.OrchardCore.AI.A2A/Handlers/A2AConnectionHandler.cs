using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.Handlers;
using CrestApps.Models;
using CrestApps.OrchardCore.AI.A2A.Models;
using CrestApps.OrchardCore.AI.A2A.Services;
using CrestApps.OrchardCore.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.A2A.Handlers;

internal sealed class A2AConnectionHandler : CatalogEntryHandlerBase<A2AConnection>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IA2AAgentCardCacheService _cacheService;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public A2AConnectionHandler(
        IHttpContextAccessor httpContextAccessor,
        IA2AAgentCardCacheService cacheService,
        IClock clock,
        IStringLocalizer<A2AConnectionHandler> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _cacheService = cacheService;
        _clock = clock;
        S = stringLocalizer;
    }

    public override Task InitializingAsync(InitializingContext<A2AConnection> context)
        => PopulateAsync(context.Model, context.Data, true);

    public override Task UpdatingAsync(UpdatingContext<A2AConnection> context)
        => PopulateAsync(context.Model, context.Data, false);

    public override Task UpdatedAsync(UpdatedContext<A2AConnection> context)
    {
        _cacheService.Invalidate(context.Model.ItemId);

        return Task.CompletedTask;
    }

    public override Task DeletedAsync(DeletedContext<A2AConnection> context)
    {
        _cacheService.Invalidate(context.Model.ItemId);

        return Task.CompletedTask;
    }

    public override Task ValidatingAsync(ValidatingContext<A2AConnection> context)
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
            connection.CreatedUtc = _clock.UtcNow;

            var user = _httpContextAccessor.HttpContext?.User;

            if (user is not null)
            {
                connection.Author = user.Identity.Name;
                connection.OwnerId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            }
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
