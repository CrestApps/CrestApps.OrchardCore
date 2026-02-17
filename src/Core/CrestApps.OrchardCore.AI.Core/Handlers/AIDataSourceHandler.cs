using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Core.Handlers;
using CrestApps.OrchardCore.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Core.Handlers;

public sealed class AIDataSourceHandler : CatalogEntryHandlerBase<AIDataSource>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public AIDataSourceHandler(
        IHttpContextAccessor httpContextAccessor,
        IClock clock,
        IStringLocalizer<AIDataSourceHandler> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _clock = clock;
        S = stringLocalizer;
    }

    public override Task InitializingAsync(InitializingContext<AIDataSource> context)
        => PopulateAsync(context.Model, context.Data);

    public override Task UpdatingAsync(UpdatingContext<AIDataSource> context)
        => PopulateAsync(context.Model, context.Data);

    public override Task ValidatingAsync(ValidatingContext<AIDataSource> context)
    {
        if (string.IsNullOrWhiteSpace(context.Model.DisplayText))
        {
            context.Result.Fail(new ValidationResult(S["Data source display-text is required."], [nameof(AIDataSource.DisplayText)]));
        }

        return Task.CompletedTask;
    }

    public override Task InitializedAsync(InitializedContext<AIDataSource> context)
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
            dataSource.Properties ??= [];
            dataSource.Properties.Merge(properties);
        }

        return Task.CompletedTask;
    }
}
