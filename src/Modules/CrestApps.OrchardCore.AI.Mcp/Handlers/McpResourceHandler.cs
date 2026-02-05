using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Mcp.Core;
using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using CrestApps.OrchardCore.Core.Handlers;
using CrestApps.OrchardCore.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using ModelContextProtocol.Protocol;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Mcp.Handlers;

internal sealed class McpResourceHandler : CatalogEntryHandlerBase<McpResource>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IMcpResourceStore _store;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public McpResourceHandler(
        IHttpContextAccessor httpContextAccessor,
        IMcpResourceStore store,
        IClock clock,
        IStringLocalizer<McpResourceHandler> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _store = store;
        _clock = clock;
        S = stringLocalizer;
    }

    public override Task InitializingAsync(InitializingContext<McpResource> context)
        => PopulateAsync(context.Model, context.Data, true);

    public override Task UpdatingAsync(UpdatingContext<McpResource> context)
        => PopulateAsync(context.Model, context.Data, false);

    public override async Task ValidatingAsync(ValidatingContext<McpResource> context)
    {
        if (string.IsNullOrEmpty(context.Model.Source))
        {
            context.Result.Fail(new ValidationResult(S["Resource type is required."], ["Source"]));
        }

        if (string.IsNullOrEmpty(context.Model.DisplayText))
        {
            context.Result.Fail(new ValidationResult(S["Display text is required."], ["DisplayText"]));
        }

        if (string.IsNullOrEmpty(context.Model.Resource?.Uri))
        {
            context.Result.Fail(new ValidationResult(S["URI is required."], ["Resource.Uri"]));
        }
        else
        {
            // Enforce unique URI using efficient lookup
            var duplicate = await _store.FindByUriAsync(context.Model.Resource.Uri);

            if (duplicate is not null && duplicate.ItemId != context.Model.ItemId)
            {
                context.Result.Fail(new ValidationResult(S["A resource with the URI '{0}' already exists.", context.Model.Resource.Uri], ["Resource.Uri"]));
            }
        }

        if (string.IsNullOrEmpty(context.Model.Resource?.Name))
        {
            context.Result.Fail(new ValidationResult(S["Name is required."], ["Resource.Name"]));
        }
    }

    private Task PopulateAsync(McpResource entry, JsonNode data, bool isNew)
    {
        if (isNew)
        {
            entry.CreatedUtc = _clock.UtcNow;

            var user = _httpContextAccessor.HttpContext?.User;

            if (user is not null)
            {
                entry.Author = user.Identity.Name;
                entry.OwnerId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            }
        }

        var displayText = data?[nameof(McpResource.DisplayText)]?.ToString();

        if (!string.IsNullOrWhiteSpace(displayText))
        {
            entry.DisplayText = displayText;
        }

        // Populate the Resource from data if provided
        var resourceData = data?[nameof(McpResource.Resource)];

        if (resourceData != null)
        {
            entry.Resource ??= new Resource
            {
                Uri = string.Empty,
                Name = string.Empty,
            };

            var uri = resourceData[nameof(Resource.Uri)]?.ToString();
            if (!string.IsNullOrWhiteSpace(uri))
            {
                entry.Resource.Uri = uri;
            }

            var name = resourceData[nameof(Resource.Name)]?.ToString();
            if (!string.IsNullOrWhiteSpace(name))
            {
                entry.Resource.Name = name;
            }

            var title = resourceData[nameof(Resource.Title)]?.ToString();
            if (!string.IsNullOrWhiteSpace(title))
            {
                entry.Resource.Title = title;
            }

            var description = resourceData[nameof(Resource.Description)]?.ToString();
            if (!string.IsNullOrWhiteSpace(description))
            {
                entry.Resource.Description = description;
            }

            var mimeType = resourceData[nameof(Resource.MimeType)]?.ToString();
            if (!string.IsNullOrWhiteSpace(mimeType))
            {
                entry.Resource.MimeType = mimeType;
            }
        }

        return Task.CompletedTask;
    }
}
