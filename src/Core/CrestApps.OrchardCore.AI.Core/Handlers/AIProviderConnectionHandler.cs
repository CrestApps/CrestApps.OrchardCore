using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Core.Handlers;

/// <summary>
/// Handles catalog lifecycle events for <see cref="AIProviderConnection"/> entries, including initialization, validation, and population from JSON data.
/// </summary>
public sealed class AIProviderConnectionHandler : CatalogEntryHandlerBase<AIProviderConnection>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AIOptions _aiOptions;
    private readonly INamedCatalog<AIProviderConnection> _connectionsCatalog;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProviderConnectionHandler"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor for retrieving the current user.</param>
    /// <param name="aiOptions">The AI options containing valid connection source definitions.</param>
    /// <param name="connectionsCatalog">The connections catalog used for uniqueness validation.</param>
    /// <param name="clock">The clock service for obtaining the current UTC time.</param>
    /// <param name="stringLocalizer">The string localizer for validation messages.</param>
    public AIProviderConnectionHandler(
        IHttpContextAccessor httpContextAccessor,
        IOptions<AIOptions> aiOptions,
        INamedCatalog<AIProviderConnection> connectionsCatalog,
        IClock clock,
        IStringLocalizer<AIProviderConnectionHandler> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _aiOptions = aiOptions.Value;
        _connectionsCatalog = connectionsCatalog;
        _clock = clock;
        S = stringLocalizer;
    }

    public override Task InitializingAsync(InitializingContext<AIProviderConnection> context, CancellationToken cancellationToken = default)
        => PopulateAsync(context.Model, context.Data, true);

    public override Task UpdatingAsync(UpdatingContext<AIProviderConnection> context, CancellationToken cancellationToken = default)
        => PopulateAsync(context.Model, context.Data, false);

    public override async Task ValidatingAsync(ValidatingContext<AIProviderConnection> context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context.Model.Name))
        {
            context.Result.Fail(new ValidationResult(S["Profile Name is required."], [nameof(AIProviderConnection.Name)]));
        }
        else
        {
            var connection = await _connectionsCatalog.FindByNameAsync(context.Model.Name, cancellationToken);

            if (connection is not null && connection.ItemId != context.Model.ItemId)
            {
                context.Result.Fail(new ValidationResult(S["A connection with this name already exists. The name must be unique."], [nameof(AIProviderConnection.Name)]));
            }
        }

        if (string.IsNullOrWhiteSpace(context.Model.Source))
        {
            context.Result.Fail(new ValidationResult(S["Source is required."], [nameof(AIProviderConnection.Source)]));
        }
        else if (!_aiOptions.ConnectionSources.TryGetValue(context.Model.Source, out _))
        {
            context.Result.Fail(new ValidationResult(S["Invalid source."], [nameof(AIProviderConnection.Source)]));
        }
    }

    public override Task InitializedAsync(InitializedContext<AIProviderConnection> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _clock.UtcNow;

        var user = _httpContextAccessor.HttpContext?.User;

        if (user != null)
        {
            context.Model.OwnerId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            context.Model.Author = user.Identity.Name;
        }

        return Task.CompletedTask;
    }

    private static Task PopulateAsync(AIProviderConnection connection, JsonNode data, bool isNew)
    {
        if (isNew)
        {
            var name = data[nameof(AIProfile.Name)]?.GetValue<string>()?.Trim();

            if (!string.IsNullOrEmpty(name))
            {
                connection.Name = name;
            }
        }

        var displayText = data[nameof(AIProviderConnection.DisplayText)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(displayText))
        {
            connection.DisplayText = displayText;
        }

        var source = data[nameof(AIProviderConnection.Source)]?.GetValue<string>()?.Trim()
            ?? data[nameof(AIProviderConnection.ClientName)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(source))
        {
            connection.Source = source;
        }

        var ownerId = data[nameof(AIProviderConnection.OwnerId)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(ownerId))
        {
            connection.OwnerId = ownerId;
        }

        var author = data[nameof(AIProviderConnection.Author)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(author))
        {
            connection.Author = author;
        }

        var createdUtc = data[nameof(AIProviderConnection.CreatedUtc)]?.GetValue<DateTime?>();

        if (createdUtc.HasValue)
        {
            connection.CreatedUtc = createdUtc.Value;
        }

        var properties = data[nameof(AIProviderConnection.Properties)]?.AsObject();

        if (properties != null)
        {
            connection.Properties ??= new Dictionary<string, object>();

            var currentJson = JsonSerializer.SerializeToNode(connection.Properties)?.AsObject() ?? [];
            currentJson.Merge(properties);
            connection.Properties = JsonSerializer.Deserialize<Dictionary<string, object>>(currentJson) ?? [];
        }

        return Task.CompletedTask;
    }
}
