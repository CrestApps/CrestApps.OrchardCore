using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Core.Handlers;

public sealed class AIToolInstanceHandler : AIToolInstanceHandlerBase
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IServiceProvider _serviceProvider;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public AIToolInstanceHandler(
        IHttpContextAccessor httpContextAccessor,
        IServiceProvider serviceProvider,
        IClock clock,
        IStringLocalizer<AIToolInstanceHandler> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _serviceProvider = serviceProvider;
        _clock = clock;
        S = stringLocalizer;
    }

    public override Task InitializingAsync(InitializingAIToolInstanceContext context)
        => PopulateAsync(context.Instance, context.Data, true);

    public override Task UpdatingAsync(UpdatingAIToolInstanceContext context)
        => PopulateAsync(context.Instance, context.Data, false);

    public override Task ValidatingAsync(ValidatingAIToolInstanceContext context)
    {
        if (string.IsNullOrWhiteSpace(context.Instance.Source))
        {
            context.Result.Fail(new ValidationResult(S["Source is required."], [nameof(AIToolInstance.Source)]));
        }
        else if (_serviceProvider.GetKeyedService<IAIToolSource>(context.Instance.Source) is null)
        {
            context.Result.Fail(new ValidationResult(S["Invalid source."], [nameof(AIToolInstance.Source)]));
        }

        return Task.CompletedTask;
    }

    public override Task InitializedAsync(InitializedAIToolInstanceContext context)
    {
        context.Instance.CreatedUtc = _clock.UtcNow;

        var user = _httpContextAccessor.HttpContext?.User;

        if (user != null)
        {
            context.Instance.OwnerId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            context.Instance.Author = user.Identity.Name;
        }

        return Task.CompletedTask;
    }

    private static Task PopulateAsync(AIToolInstance instance, JsonNode data, bool isNew)
    {
        var displayText = data[nameof(AIToolInstance.DisplayText)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(displayText))
        {
            instance.DisplayText = displayText;
        }

        var source = data[nameof(AIToolInstance.Source)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(source))
        {
            instance.Source = source;
        }

        var ownerId = data[nameof(AIToolInstance.OwnerId)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(ownerId))
        {
            instance.OwnerId = ownerId;
        }

        var author = data[nameof(AIToolInstance.Author)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(author))
        {
            instance.Author = author;
        }

        var createdUtc = data[nameof(AIToolInstance.CreatedUtc)]?.GetValue<DateTime?>();

        if (createdUtc.HasValue)
        {
            instance.CreatedUtc = createdUtc.Value;
        }

        return Task.CompletedTask;
    }
}
