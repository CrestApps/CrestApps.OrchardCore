using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.OpenAI.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.OpenAI.Core.Handlers;

public class AIChatProfileHandler : AIChatProfileHandlerBase
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public AIChatProfileHandler(
        IHttpContextAccessor httpContextAccessor,
        IClock clock,
        IStringLocalizer<AIChatProfileHandler> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _clock = clock;
        S = stringLocalizer;
    }

    public override Task InitializingAsync(InitializingAIChatProfileContext context)
        => PopulateAsync(context.Profile, context.Data);

    public override Task UpdatingAsync(UpdatingAIChatProfileContext context)
        => PopulateAsync(context.Profile, context.Data);

    public override Task ValidatingAsync(ValidatingAIChatProfileContext context)
    {
        if (string.IsNullOrWhiteSpace(context.Profile.Title))
        {
            context.Result.Fail(new ValidationResult(S["Rule name is required"], [nameof(AIChatProfile.Title)]));
        }

        if (string.IsNullOrWhiteSpace(context.Profile.Source))
        {
            context.Result.Fail(new ValidationResult(S["Source name is required"], [nameof(AIChatProfile.Source)]));
        }

        return Task.CompletedTask;
    }

    public override Task InitializedAsync(InitializedAIChatProfileContext context)
    {
        context.Profile.CreatedUtc = _clock.UtcNow;

        var user = _httpContextAccessor.HttpContext?.User;

        if (user != null)
        {
            context.Profile.OwnerId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            context.Profile.Author = user.Identity.Name;
        }

        return Task.CompletedTask;
    }

    private static Task PopulateAsync(AIChatProfile rule, JsonNode data)
    {
        var title = data[nameof(AIChatProfile.Title)]?.GetValue<string>();

        if (!string.IsNullOrEmpty(title))
        {
            rule.Title = title;
        }

        return Task.CompletedTask;
    }
}
