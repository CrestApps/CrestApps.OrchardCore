using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Core.Handlers;

public sealed class AIDeploymentHandler : AIDeploymentHandlerBase
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AIProviderOptions _connectionOptions;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public AIDeploymentHandler(
        IHttpContextAccessor httpContextAccessor,
        IAIDeploymentStore deploymentStore,
        IOptions<AIProviderOptions> connectionOptions,
        IClock clock,
        IStringLocalizer<AIDeploymentHandler> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _connectionOptions = connectionOptions.Value;
        _clock = clock;
        S = stringLocalizer;
    }

    public override Task InitializingAsync(InitializingAIDeploymentContext context)
        => PopulateAsync(context.Deployment, context.Data);

    public override Task UpdatingAsync(UpdatingModelDeploymentContext context)
        => PopulateAsync(context.Deployment, context.Data);

    public override Task ValidatingAsync(ValidatingAIDeploymentContext context)
    {
        if (string.IsNullOrWhiteSpace(context.Deployment.Name))
        {
            context.Result.Fail(new ValidationResult(S["Deployment Name is required."], [nameof(AIDeployment.Name)]));
        }

        var hasConnectionName = true;

        if (string.IsNullOrWhiteSpace(context.Deployment.ConnectionName))
        {
            hasConnectionName = false;
            context.Result.Fail(new ValidationResult(S["Connection name is required."], [nameof(AIDeployment.ConnectionName)]));
        }

        if (string.IsNullOrWhiteSpace(context.Deployment.ProviderName))
        {
            context.Result.Fail(new ValidationResult(S["Provider is required."], [nameof(AIDeployment.ProviderName)]));
        }
        else
        {
            if (hasConnectionName)
            {
                if (!_connectionOptions.Providers.TryGetValue(context.Deployment.ProviderName, out var provider))
                {
                    context.Result.Fail(new ValidationResult(S["There are no configured connection for the provider: {0}", context.Deployment.ProviderName], [nameof(AIDeployment.ProviderName)]));
                }
                else if (!provider.Connections.TryGetValue(context.Deployment.ConnectionName, out var _))
                {
                    context.Result.Fail(new ValidationResult(S["Invalid connection name provided."], [nameof(AIDeployment.ConnectionName)]));
                }
            }
        }

        return Task.CompletedTask;
    }

    public override Task InitializedAsync(InitializedAIDeploymentContext context)
    {
        context.Deployment.CreatedUtc = _clock.UtcNow;

        var user = _httpContextAccessor.HttpContext?.User;

        if (user != null)
        {
            context.Deployment.OwnerId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            context.Deployment.Author = user.Identity.Name;
        }

        return Task.CompletedTask;
    }

    private static Task PopulateAsync(AIDeployment deployment, JsonNode data)
    {
        var name = data[nameof(AIDeployment.Name)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(name))
        {
            deployment.Name = name;
        }

        var providerName = data[nameof(AIDeployment.ProviderName)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(providerName))
        {
            deployment.ProviderName = providerName;
        }

        var connectionName = data[nameof(AIDeployment.ConnectionName)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(connectionName))
        {
            deployment.ConnectionName = connectionName;
        }

        return Task.CompletedTask;
    }
}
