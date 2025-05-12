using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.Core.Handlers;
using CrestApps.OrchardCore.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Core.Handlers;

public sealed class AIDataSourceHandler : ModelHandlerBase<AIDataSource>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AIOptions _aiOptions;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public AIDataSourceHandler(
        IHttpContextAccessor httpContextAccessor,
        IOptions<AIOptions> aiOptions,
        IClock clock,
        IStringLocalizer<AIDataSourceHandler> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _aiOptions = aiOptions.Value;
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
            context.Result.Fail(new ValidationResult(S["Data-source display-text is required."], [nameof(AIDataSource.DisplayText)]));
        }

        var hasProfileSource = !string.IsNullOrWhiteSpace(context.Model.ProfileSource);
        var hasType = !string.IsNullOrWhiteSpace(context.Model.Type);

        if (!hasProfileSource)
        {
            context.Result.Fail(new ValidationResult(S["Data-source profile-source is required."], [nameof(AIDataSource.ProfileSource)]));
        }

        if (!hasType)
        {
            context.Result.Fail(new ValidationResult(S["Data-source type is required."], [nameof(AIDataSource.Type)]));
        }

        if (hasProfileSource && hasType && !_aiOptions.DataSources.TryGetValue(new AIDataSourceKey(context.Model.ProfileSource, context.Model.Type), out _))
        {
            context.Result.Fail(new ValidationResult(S["Unable to find a profile-source named '{0}' with the type '{1}'.", context.Model.ProfileSource, context.Model.Type], [nameof(AIDataSource.Type)]));
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

    private static Task PopulateAsync(AIDataSource deployment, JsonNode data)
    {
        var displayText = data[nameof(AIDataSource.DisplayText)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(displayText))
        {
            deployment.DisplayText = displayText;
        }

        var profileSource = data[nameof(AIDataSource.ProfileSource)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(profileSource))
        {
            deployment.ProfileSource = profileSource;
        }

        var type = data[nameof(AIDataSource.Type)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(type))
        {
            deployment.Type = type;
        }

        return Task.CompletedTask;
    }
}
