using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Handlers;

internal sealed class OmnichannelCampaignHandler : CatalogEntryHandlerBase<OmnichannelCampaign>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelCampaignHandler"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The http context accessor.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public OmnichannelCampaignHandler(
        IHttpContextAccessor httpContextAccessor,
        IClock clock,
        IStringLocalizer<OmnichannelCampaignHandler> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _clock = clock;
        S = stringLocalizer;
    }

    public override Task InitializingAsync(InitializingContext<OmnichannelCampaign> context, CancellationToken cancellationToken = default)
        => PopulateAsync(context.Model, context.Data);

    public override Task UpdatingAsync(UpdatingContext<OmnichannelCampaign> context, CancellationToken cancellationToken = default)
        => PopulateAsync(context.Model, context.Data);

    public override Task ValidatingAsync(ValidatingContext<OmnichannelCampaign> context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context.Model.DisplayText))
        {
            context.Result.Fail(new ValidationResult(S["Name is required."], [nameof(OmnichannelCampaign.DisplayText)]));
        }

        return Task.CompletedTask;
    }

    public override Task InitializedAsync(InitializedContext<OmnichannelCampaign> context, CancellationToken cancellationToken = default)
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
            campaign.Properties ??= new Dictionary<string, object>();

            foreach (var (key, value) in properties)
            {
                campaign.Properties[key] = value;
            }
        }

        return Task.CompletedTask;
    }
}
