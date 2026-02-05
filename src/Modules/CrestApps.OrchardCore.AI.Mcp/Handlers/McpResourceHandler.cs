using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json.Nodes;
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
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public McpResourceHandler(
        IHttpContextAccessor httpContextAccessor,
        IClock clock,
        IStringLocalizer<McpResourceHandler> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _clock = clock;
        S = stringLocalizer;
    }

    public override Task InitializingAsync(InitializingContext<McpResource> context)
        => PopulateAsync(context.Model, context.Data, true);

    public override Task UpdatingAsync(UpdatingContext<McpResource> context)
        => PopulateAsync(context.Model, context.Data, false);

    public override Task ValidatingAsync(ValidatingContext<McpResource> context)
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

        if (string.IsNullOrEmpty(context.Model.Resource?.Name))
        {
            context.Result.Fail(new ValidationResult(S["Name is required."], ["Resource.Name"]));
        }

        return Task.CompletedTask;
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
