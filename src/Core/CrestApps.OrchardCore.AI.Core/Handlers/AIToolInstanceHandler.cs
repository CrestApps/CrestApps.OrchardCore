using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Core.Handlers;
using CrestApps.OrchardCore.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Core.Handlers;

public sealed class AIToolInstanceHandler : ModelHandlerBase<AIToolInstance>
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

    public override Task InitializingAsync(InitializingContext<AIToolInstance> context)
        => PopulateAsync(context.Model, context.Data);

    public override Task UpdatingAsync(UpdatingContext<AIToolInstance> context)
        => PopulateAsync(context.Model, context.Data);

    public override Task ValidatingAsync(ValidatingContext<AIToolInstance> context)
    {
        if (string.IsNullOrWhiteSpace(context.Model.Source))
        {
            context.Result.Fail(new ValidationResult(S["Source is required."], [nameof(AIToolInstance.Source)]));
        }
        else if (_serviceProvider.GetKeyedService<IAIToolSource>(context.Model.Source) is null)
        {
            context.Result.Fail(new ValidationResult(S["Invalid source."], [nameof(AIToolInstance.Source)]));
        }

        return Task.CompletedTask;
    }

    public override Task InitializedAsync(InitializedContext<AIToolInstance> context)
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

    private static Task PopulateAsync(AIToolInstance instance, JsonNode data)
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

        var properties = data[nameof(AIToolInstance.Properties)]?.AsObject();

        if (properties != null)
        {
            instance.Properties ??= [];
            instance.Properties.Merge(properties);
        }

        return Task.CompletedTask;
    }
}
