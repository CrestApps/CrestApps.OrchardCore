using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using CrestApps.OrchardCore.Core.Handlers;
using CrestApps.OrchardCore.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AzureAIInference.Handlers;

internal sealed class McpConnectionHandler : ModelHandlerBase<McpConnection>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public McpConnectionHandler(
        IHttpContextAccessor httpContextAccessor,
        IClock clock,
        IStringLocalizer<McpConnectionHandler> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _clock = clock;
        S = stringLocalizer;
    }

    public override Task InitializingAsync(InitializingContext<McpConnection> context)
        => PopulateAsync(context.Model, context.Data, true);

    public override Task UpdatingAsync(UpdatingContext<McpConnection> context)
        => PopulateAsync(context.Model, context.Data, false);

    public override Task ValidatingAsync(ValidatingContext<McpConnection> context)
    {
        if (string.IsNullOrEmpty(context.Model.DisplayText))
        {
            context.Result.Fail(new ValidationResult(S["DisplayText is required field."], [nameof(McpConnection.DisplayText)]));
        }

        return Task.CompletedTask;
    }

    private Task PopulateAsync(McpConnection connection, JsonNode data, bool isNew)
    {
        if (isNew)
        {
            var user = _httpContextAccessor.HttpContext?.User;

            if (user is not null)
            {
                connection.Author = user.Identity.Name;
                connection.CreatedUtc = _clock.UtcNow;
            }
        }

        var displayText = data[nameof(McpConnection.DisplayText)]?.ToString();

        if (!string.IsNullOrWhiteSpace(displayText))
        {
            connection.DisplayText = displayText;
        }

        var metadataNode = data["Properties"]?.AsObject();

        if (metadataNode is not null)
        {
            connection.Properties = metadataNode.Clone();
        }

        return Task.CompletedTask;
    }
}
