using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json.Nodes;
using System.Text.Json.Settings;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Core.Handlers;
using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Core.Handlers;

public sealed class AIProfileTemplateHandler : CatalogEntryHandlerBase<AIProfileTemplate>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly INamedCatalog<AIProfileTemplate> _templatesCatalog;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public AIProfileTemplateHandler(
        IHttpContextAccessor httpContextAccessor,
        INamedCatalog<AIProfileTemplate> templatesCatalog,
        IClock clock,
        IStringLocalizer<AIProfileTemplateHandler> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _templatesCatalog = templatesCatalog;
        _clock = clock;
        S = stringLocalizer;
    }

    public override Task InitializingAsync(InitializingContext<AIProfileTemplate> context)
        => PopulateAsync(context.Model, context.Data, true);

    public override Task UpdatingAsync(UpdatingContext<AIProfileTemplate> context)
        => PopulateAsync(context.Model, context.Data, false);

    public override async Task ValidatingAsync(ValidatingContext<AIProfileTemplate> context)
    {
        if (string.IsNullOrWhiteSpace(context.Model.Name))
        {
            context.Result.Fail(new ValidationResult(S["Template name is required."], [nameof(AIProfileTemplate.Name)]));
        }
        else
        {
            var existing = await _templatesCatalog.FindByNameAsync(context.Model.Name);

            if (existing is not null && existing.ItemId != context.Model.ItemId)
            {
                context.Result.Fail(new ValidationResult(S["A template with this name already exists. The name must be unique."], [nameof(AIProfileTemplate.Name)]));
            }
        }

        if (string.IsNullOrWhiteSpace(context.Model.DisplayText))
        {
            context.Result.Fail(new ValidationResult(S["Title is required."], [nameof(AIProfileTemplate.DisplayText)]));
        }
    }

    public override Task InitializedAsync(InitializedContext<AIProfileTemplate> context)
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

    public override Task CreatingAsync(CreatingContext<AIProfileTemplate> context)
    {
        if (string.IsNullOrWhiteSpace(context.Model.DisplayText))
        {
            context.Model.DisplayText = context.Model.Name;
        }

        return Task.CompletedTask;
    }

    private static Task PopulateAsync(AIProfileTemplate template, JsonNode data, bool isNew)
    {
        if (isNew)
        {
            var name = data[nameof(AIProfileTemplate.Name)]?.GetValue<string>()?.Trim();

            if (!string.IsNullOrEmpty(name))
            {
                template.Name = name;
            }
        }

        var displayText = data[nameof(AIProfileTemplate.DisplayText)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(displayText))
        {
            template.DisplayText = displayText;
        }

        var description = data[nameof(AIProfileTemplate.Description)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(description))
        {
            template.Description = description;
        }

        var category = data[nameof(AIProfileTemplate.Category)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(category))
        {
            template.Category = category;
        }

        var profileType = data[nameof(AIProfileTemplate.ProfileType)]?.GetEnumValue<AIProfileType>();

        if (profileType.HasValue)
        {
            template.ProfileType = profileType.Value;
        }

        var connectionName = data[nameof(AIProfileTemplate.ConnectionName)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(connectionName))
        {
            template.ConnectionName = connectionName;
        }

        var systemMessage = data[nameof(AIProfileTemplate.SystemMessage)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(systemMessage))
        {
            template.SystemMessage = systemMessage;
        }

        var welcomeMessage = data[nameof(AIProfileTemplate.WelcomeMessage)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(welcomeMessage))
        {
            template.WelcomeMessage = welcomeMessage;
        }

        var properties = data[nameof(AIProfileTemplate.Properties)]?.AsObject();

        if (properties != null)
        {
            template.Properties ??= [];

            // Snapshot existing properties before merge so named entries can be
            // merged by name (upsert) instead of being fully replaced.
            var existingSnapshot = template.Properties.Clone();

            template.Properties.Merge(properties, new JsonMergeSettings
            {
                MergeArrayHandling = MergeArrayHandling.Replace,
            });

            AIPropertiesMergeHelper.MergeNamedEntries(template.Properties, existingSnapshot);
        }

        if (string.IsNullOrWhiteSpace(template.DisplayText))
        {
            template.DisplayText = template.Name;
        }

        return Task.CompletedTask;
    }
}
