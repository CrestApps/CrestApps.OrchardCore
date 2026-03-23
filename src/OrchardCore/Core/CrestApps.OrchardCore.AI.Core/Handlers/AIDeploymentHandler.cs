using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.AI.Models;
using CrestApps.Handlers;
using CrestApps.Models;
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

        if (!Enum.IsDefined(context.Model.Type))
        {
            context.Result.Fail(new ValidationResult(S["The deployment type '{0}' is not valid.", context.Model.Type], [nameof(AIDeployment.Type)]));
        }

        var hasConnectionName = true;

        if (string.IsNullOrWhiteSpace(context.Model.ConnectionName))
        {
            hasConnectionName = false;
            context.Result.Fail(new ValidationResult(S["Connection name is required."], [nameof(AIDeployment.ConnectionName)]));
        }

        if (string.IsNullOrWhiteSpace(context.Model.ProviderName))
        {
            context.Result.Fail(new ValidationResult(S["Provider is required."], [nameof(AIDeployment.ProviderName)]));
        }
        else
        {
            if (hasConnectionName)
            {
                if (!_providerOptions.Providers.TryGetValue(context.Model.ProviderName, out var provider))
                {
                    context.Result.Fail(new ValidationResult(S["There are no configured connection for the provider: {0}", context.Model.ProviderName], [nameof(AIDeployment.ProviderName)]));
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

        var providerName = data[nameof(AIDeployment.ProviderName)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(providerName))
        {
            deployment.Source = providerName;
        }

        var connectionName = data[nameof(AIDeployment.ConnectionName)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(connectionName))
        {
            deployment.ConnectionName = connectionName;
        }
        else if (!string.IsNullOrEmpty(providerName) && _providerOptions.Providers.TryGetValue(providerName, out var provider))
        {
            deployment.ConnectionName = provider.DefaultConnectionName;
        }

        var typeValue = data[nameof(AIDeployment.Type)]?.GetValue<string>();

        if (!string.IsNullOrEmpty(typeValue) && Enum.TryParse<AIDeploymentType>(typeValue, ignoreCase: true, out var type))
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
            deployment.Properties ??= new Dictionary<string, object>();
            foreach (var (key, value) in properties)

            {

                deployment.Properties[key] = value;

            }
        }

        return Task.CompletedTask;
    }
}
