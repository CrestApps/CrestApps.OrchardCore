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
    private readonly IAIChatProfileStore _profileStore;
    private readonly IModelDeploymentStore _modelDeploymentStore;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public AIChatProfileHandler(
        IHttpContextAccessor httpContextAccessor,
        IAIChatProfileStore profileStore,
        IModelDeploymentStore modelDeploymentStore,
        IClock clock,
        IStringLocalizer<AIChatProfileHandler> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _profileStore = profileStore;
        _modelDeploymentStore = modelDeploymentStore;
        _clock = clock;
        S = stringLocalizer;
    }

    public override Task InitializingAsync(InitializingAIChatProfileContext context)
        => PopulateAsync(context.Profile, context.Data, true);

    public override Task UpdatingAsync(UpdatingAIChatProfileContext context)
        => PopulateAsync(context.Profile, context.Data, false);

    public override async Task ValidatingAsync(ValidatingAIChatProfileContext context)
    {
        if (string.IsNullOrWhiteSpace(context.Profile.Name))
        {
            context.Result.Fail(new ValidationResult(S["Profile Name is required."], [nameof(AIChatProfile.Name)]));
        }
        else
        {
            var profile = await _profileStore.FindByNameAsync(context.Profile.Name);

            if (profile is not null && profile.Id != context.Profile.Id)
            {
                context.Result.Fail(new ValidationResult(S["A profile with this name already exists. The name must be unique."], [nameof(AIChatProfile.Name)]));
            }
        }

        if (string.IsNullOrWhiteSpace(context.Profile.Source))
        {
            context.Result.Fail(new ValidationResult(S["Source is required."], [nameof(AIChatProfile.Source)]));
        }

        if (string.IsNullOrWhiteSpace(context.Profile.DeploymentId))
        {
            context.Result.Fail(new ValidationResult(S["DeploymentId is required."], [nameof(AIChatProfile.DeploymentId)]));
        }
        else if (await _modelDeploymentStore.FindByIdAsync(context.Profile.DeploymentId) is null)
        {
            context.Result.Fail(new ValidationResult(S["Invalid DeploymentId provided."], [nameof(AIChatProfile.DeploymentId)]));
        }
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

    private static Task PopulateAsync(AIChatProfile profile, JsonNode data, bool isNew)
    {
        if (isNew)
        {
            var name = data[nameof(AIChatProfile.Name)]?.GetValue<string>()?.Trim();

            if (!string.IsNullOrEmpty(name))
            {
                profile.Name = name;
            }
        }

        var deploymentId = data[nameof(AIChatProfile.DeploymentId)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(deploymentId))
        {
            profile.DeploymentId = deploymentId;
        }

        return Task.CompletedTask;
    }
}
