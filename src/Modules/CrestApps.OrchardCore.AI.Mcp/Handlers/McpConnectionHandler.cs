using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Mcp.Models;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Core.Handlers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Mcp.Handlers;

internal sealed class McpConnectionHandler : CatalogEntryAuditHandlerBase<McpConnection>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="McpConnectionHandler"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The http context accessor.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public McpConnectionHandler(
        IHttpContextAccessor httpContextAccessor,
        IClock clock,
        IStringLocalizer<McpConnectionHandler> stringLocalizer)
        : base(httpContextAccessor, clock)
    {
        S = stringLocalizer;
    }

    protected override void SetCreated(McpConnection entry, DateTime createdUtc, string ownerId, string author, bool hasUser)
    {
        entry.CreatedUtc = createdUtc;

        if (hasUser)
        {
            entry.OwnerId = ownerId;
            entry.Author = author;
        }
    }

    public override Task InitializingAsync(InitializingContext<McpConnection> context, CancellationToken cancellationToken = default)
        => PopulateAsync(context.Model, context.Data, true);

    public override Task UpdatingAsync(UpdatingContext<McpConnection> context, CancellationToken cancellationToken = default)
    {
        StampModified(context.Model);

        return PopulateAsync(context.Model, context.Data, false);
    }

    public override Task ValidatingAsync(ValidatingContext<McpConnection> context, CancellationToken cancellationToken = default)
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
            StampCreated(connection);
        }

        var displayText = data[nameof(McpConnection.DisplayText)]?.ToString();

        if (!string.IsNullOrWhiteSpace(displayText))
        {
            connection.DisplayText = displayText;
        }

        var properties = data[nameof(McpConnection.Properties)]?.AsObject();

        if (properties is not null)
        {
            connection.Properties ??= new Dictionary<string, object>();

            foreach (var (key, value) in properties) { connection.Properties[key] = value; }
        }

        return Task.CompletedTask;
    }
}
