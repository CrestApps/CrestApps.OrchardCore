using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.OpenAI.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.OpenAI.Core.Handlers;

public class ModelDeploymentHandler : ModelDeploymentHandlerBase
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IModelDeploymentStore _deploymentStore;
    private readonly OpenAIConnectionOptions _connectionOptions;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public ModelDeploymentHandler(
        IHttpContextAccessor httpContextAccessor,
        IModelDeploymentStore deploymentStore,
        IOptions<OpenAIConnectionOptions> connectionOptions,
        IClock clock,
        IStringLocalizer<ModelDeploymentHandler> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _deploymentStore = deploymentStore;
        _connectionOptions = connectionOptions.Value;
        _clock = clock;
        S = stringLocalizer;
    }

    public override Task InitializingAsync(InitializingModelDeploymentContext context)
        => PopulateAsync(context.Deployment, context.Data);

    public override Task UpdatingAsync(UpdatingModelDeploymentContext context)
        => PopulateAsync(context.Deployment, context.Data);

    public override Task ValidatingAsync(ValidatingModelDeploymentContext context)
    {
        if (string.IsNullOrWhiteSpace(context.Deployment.Name))
        {
            context.Result.Fail(new ValidationResult(S["Profile Name is required."], [nameof(ModelDeployment.Name)]));
        }

        var hasConnectionName = true;

        if (string.IsNullOrWhiteSpace(context.Deployment.ConnectionName))
        {
            hasConnectionName = false;
            context.Result.Fail(new ValidationResult(S["Connection name is required."], [nameof(ModelDeployment.ConnectionName)]));
        }

        if (string.IsNullOrWhiteSpace(context.Deployment.Source))
        {
            context.Result.Fail(new ValidationResult(S["Source is required."], [nameof(ModelDeployment.Source)]));
        }
        else
        {
            if (hasConnectionName)
            {
                // validate the connection name.

                if (!_connectionOptions.Connections.TryGetValue(context.Deployment.Source, out var connections))
                {
                    context.Result.Fail(new ValidationResult(S["There are no configured connection for the source: {0}", context.Deployment.Source], [nameof(ModelDeployment.Source)]));
                }
                else if (!connections.Any(x => x.Name != null && x.Name.Equals(context.Deployment.ConnectionName, StringComparison.OrdinalIgnoreCase)))
                {
                    context.Result.Fail(new ValidationResult(S["Invalid connection name provided."], [nameof(ModelDeployment.ConnectionName)]));
                }
            }
        }

        return Task.CompletedTask;
    }

    public override Task InitializedAsync(InitializedModelDeploymentContext context)
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

    private static Task PopulateAsync(ModelDeployment deployment, JsonNode data)
    {
        var name = data[nameof(ModelDeployment.Name)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(name))
        {
            deployment.Name = name;
        }

        var connectionName = data[nameof(ModelDeployment.ConnectionName)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(connectionName))
        {
            deployment.ConnectionName = connectionName;
        }

        return Task.CompletedTask;
    }
}
