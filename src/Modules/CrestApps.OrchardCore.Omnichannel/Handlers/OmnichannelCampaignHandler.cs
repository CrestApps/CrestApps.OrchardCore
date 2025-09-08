using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Core.Handlers;
using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Handlers;

internal sealed class OmnichannelCampaignHandler : ModelHandlerBase<OmnichannelCampaign>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public OmnichannelCampaignHandler(
        IHttpContextAccessor httpContextAccessor,
        IClock clock,
        IStringLocalizer<OmnichannelCampaignHandler> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _clock = clock;
        S = stringLocalizer;
    }

    public override Task InitializingAsync(InitializingContext<OmnichannelCampaign> context)
        => PopulateAsync(context.Model, context.Data);

    public override Task UpdatingAsync(UpdatingContext<OmnichannelCampaign> context)
        => PopulateAsync(context.Model, context.Data);

    public override Task ValidatingAsync(ValidatingContext<OmnichannelCampaign> context)
    {
        if (string.IsNullOrWhiteSpace(context.Model.DisplayText))
        {
            context.Result.Fail(new ValidationResult(S["Name is required."], [nameof(OmnichannelCampaign.DisplayText)]));
        }

        return Task.CompletedTask;
    }

    public override Task InitializedAsync(InitializedContext<OmnichannelCampaign> context)
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

    private static Task PopulateAsync(OmnichannelCampaign campaign, JsonNode data)
    {
        var displayText = data[nameof(OmnichannelCampaign.DisplayText)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(displayText))
        {
            campaign.DisplayText = displayText;
        }

        var properties = data[nameof(OmnichannelCampaign.Properties)]?.AsObject();

        if (properties != null)
        {
            campaign.Properties ??= [];
            campaign.Properties.Merge(properties);
        }

        return Task.CompletedTask;
    }
}
