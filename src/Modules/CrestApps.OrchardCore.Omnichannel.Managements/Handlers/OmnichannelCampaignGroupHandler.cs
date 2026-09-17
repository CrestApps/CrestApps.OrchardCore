using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Deployments;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Handlers;

internal sealed class OmnichannelCampaignGroupHandler : CatalogEntryHandlerBase<OmnichannelCampaignGroup>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly TimeProvider _timeProvider;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelCampaignGroupHandler"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="timeProvider">The time provider.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public OmnichannelCampaignGroupHandler(
        IHttpContextAccessor httpContextAccessor,
        TimeProvider timeProvider,
        IStringLocalizer<OmnichannelCampaignGroupHandler> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _timeProvider = timeProvider;
        S = stringLocalizer;
    }

    public override Task InitializingAsync(InitializingContext<OmnichannelCampaignGroup> context, CancellationToken cancellationToken = default)
    {
        return PopulateAsync(context.Model, context.Data);
    }

    public override Task UpdatingAsync(UpdatingContext<OmnichannelCampaignGroup> context, CancellationToken cancellationToken = default)
    {
        context.Model.ModifiedUtc = _timeProvider.GetUtcNow().UtcDateTime;

        return PopulateAsync(context.Model, context.Data);
    }

    public override Task ValidatingAsync(ValidatingContext<OmnichannelCampaignGroup> context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context.Model.DisplayText))
        {
            context.Result.Fail(new ValidationResult(S["Name is required."], [nameof(OmnichannelCampaignGroup.DisplayText)]));
        }

        return Task.CompletedTask;
    }

    public override Task InitializedAsync(InitializedContext<OmnichannelCampaignGroup> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _timeProvider.GetUtcNow().UtcDateTime;

        var user = _httpContextAccessor.HttpContext?.User;

        if (user != null)
        {
            context.Model.OwnerId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            context.Model.Author = user.Identity.Name;
        }

        return Task.CompletedTask;
    }

    private static Task PopulateAsync(OmnichannelCampaignGroup group, JsonNode data)
    {
        OmnichannelDeploymentSerializer.Populate(group, data);

        var displayText = data[nameof(OmnichannelCampaignGroup.DisplayText)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(displayText))
        {
            group.DisplayText = displayText;
        }

        return Task.CompletedTask;
    }
}
