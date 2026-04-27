using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Core.Handlers;

/// <summary>
/// Handles catalog lifecycle events for <see cref="AIDataSource"/> entries, including initialization, validation, and population from JSON data.
/// </summary>
public sealed class AIDataSourceHandler : CatalogEntryHandlerBase<AIDataSource>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIDataSourceHandler"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor for retrieving the current user.</param>
    /// <param name="clock">The clock service for obtaining the current UTC time.</param>
    /// <param name="stringLocalizer">The string localizer for validation messages.</param>
    public AIDataSourceHandler(
        IHttpContextAccessor httpContextAccessor,
        IClock clock,
        IStringLocalizer<AIDataSourceHandler> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _clock = clock;
        S = stringLocalizer;
    }

    public override Task InitializingAsync(InitializingContext<AIDataSource> context, CancellationToken cancellationToken = default)
        => PopulateAsync(context.Model, context.Data);

    public override Task UpdatingAsync(UpdatingContext<AIDataSource> context, CancellationToken cancellationToken = default)
        => PopulateAsync(context.Model, context.Data);

    public override Task ValidatingAsync(ValidatingContext<AIDataSource> context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context.Model.DisplayText))
        {
            context.Result.Fail(new ValidationResult(S["Data source display-text is required."], [nameof(AIDataSource.DisplayText)]));
        }

        return Task.CompletedTask;
    }

    public override Task InitializedAsync(InitializedContext<AIDataSource> context, CancellationToken cancellationToken = default)
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

    private static Task PopulateAsync(AIDataSource dataSource, JsonNode data)
    {
        var displayText = data[nameof(AIDataSource.DisplayText)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(displayText))
        {
            dataSource.DisplayText = displayText;
        }

        var properties = data[nameof(AIDataSource.Properties)]?.AsObject();

        if (properties != null)
        {
            dataSource.Properties ??= new Dictionary<string, object>();

            foreach (var (key, value) in properties)
            {
                dataSource.Properties[key] = value;
            }
        }

        return Task.CompletedTask;
    }
}
