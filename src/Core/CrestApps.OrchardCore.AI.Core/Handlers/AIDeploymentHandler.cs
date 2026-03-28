using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Core.Handlers;
using CrestApps.OrchardCore.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Core.Handlers;

public sealed class AIDeploymentHandler : CatalogEntryHandlerBase<AIDeployment>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AIProviderOptions _providerOptions;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public AIDeploymentHandler(
        IHttpContextAccessor httpContextAccessor,
        IOptions<AIProviderOptions> providerOptions,
        IClock clock,
        IStringLocalizer<AIDeploymentHandler> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _providerOptions = providerOptions.Value;
        _clock = clock;
        S = stringLocalizer;
    }

    public override Task InitializingAsync(InitializingContext<AIDeployment> context)
        => PopulateAsync(context.Model, context.Data);

    public override Task UpdatingAsync(UpdatingContext<AIDeployment> context)
        => PopulateAsync(context.Model, context.Data);

    public override Task ValidatingAsync(ValidatingContext<AIDeployment> context)
    {
        if (string.IsNullOrWhiteSpace(context.Model.Name))
        {
            context.Result.Fail(new ValidationResult(S["Deployment Name is required."], [nameof(AIDeployment.Name)]));
        }

        if (!context.Model.Type.IsValidSelection())
        {
            context.Result.Fail(new ValidationResult(S["The deployment type '{0}' is not valid.", context.Model.Type], [nameof(AIDeployment.Type)]));
        }

        var hasConnectionName = true;

        if (string.IsNullOrWhiteSpace(context.Model.ConnectionName))
        {
            hasConnectionName = false;
            context.Result.Fail(new ValidationResult(S["Connection name is required."], [nameof(AIDeployment.ConnectionName)]));
        }

        if (string.IsNullOrWhiteSpace(context.Model.ClientName))
        {
            context.Result.Fail(new ValidationResult(S["Provider is required."], [nameof(AIDeployment.ClientName)]));
        }
        else
        {
            if (hasConnectionName)
            {
                if (!_providerOptions.Providers.TryGetValue(context.Model.ClientName, out var provider))
                {
                    context.Result.Fail(new ValidationResult(S["There are no configured connection for the provider: {0}", context.Model.ClientName], [nameof(AIDeployment.ClientName)]));
                }
                else if (!provider.Connections.TryGetValue(context.Model.ConnectionName, out var _) &&
                    !provider.Connections.Any(x => x.Value.TryGetValue("ConnectionNameAlias", out var r) &&
                    string.Equals(r.ToString(), context.Model.ConnectionName, StringComparison.OrdinalIgnoreCase)))
                {
                    context.Result.Fail(new ValidationResult(S["Invalid connection name provided."], [nameof(AIDeployment.ConnectionName)]));
                }
            }
        }

        return Task.CompletedTask;
    }

    public override Task InitializedAsync(InitializedContext<AIDeployment> context)
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

    private Task PopulateAsync(AIDeployment deployment, JsonNode data)
    {
        var name = data[nameof(AIDeployment.Name)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(name))
        {
            deployment.Name = name;
        }

        var clientName = data[nameof(AIDeployment.ClientName)]?.GetValue<string>()?.Trim()
            ?? data["ProviderName"]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(clientName))
        {
            deployment.ClientName = clientName;
        }

        var connectionName = data[nameof(AIDeployment.ConnectionName)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(connectionName))
        {
            deployment.ConnectionName = connectionName;
        }
        else if (!string.IsNullOrEmpty(clientName) && _providerOptions.Providers.TryGetValue(clientName, out var provider))
        {
            deployment.ConnectionName = provider.DefaultConnectionName;
        }

        if (TryGetDeploymentType(data[nameof(AIDeployment.Type)], out var type))
        {
            deployment.Type = type;
        }

        var isDefault = data[nameof(AIDeployment.IsDefault)]?.GetValue<bool>();

        if (isDefault.HasValue)
        {
            deployment.IsDefault = isDefault.Value;
        }

        var properties = data[nameof(AIDeployment.Properties)]?.AsObject();

        if (properties != null)
        {
            deployment.Properties ??= [];
            deployment.Properties.Merge(properties);
        }

        return Task.CompletedTask;
    }

    private static bool TryGetDeploymentType(JsonNode typeNode, out AIDeploymentType type)
    {
        type = AIDeploymentType.None;

        if (typeNode is null)
        {
            return false;
        }

        if (typeNode is JsonArray array)
        {
            foreach (var item in array)
            {
                if (item is null ||
                    !Enum.TryParse<AIDeploymentType>(item.GetValue<string>(), ignoreCase: true, out var parsedType) ||
                    parsedType == AIDeploymentType.None)
                {
                    type = AIDeploymentType.None;
                    return false;
                }

                type |= parsedType;
            }

            return type.IsValidSelection();
        }

        var typeValue = typeNode.GetValue<string>();

        return !string.IsNullOrEmpty(typeValue) &&
            Enum.TryParse(typeValue, ignoreCase: true, out type) &&
            type.IsValidSelection();
    }
}
