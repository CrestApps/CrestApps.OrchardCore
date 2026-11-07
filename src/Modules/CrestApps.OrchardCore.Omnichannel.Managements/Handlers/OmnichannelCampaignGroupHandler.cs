using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Core.Handlers;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Handlers;

internal sealed class OmnichannelCampaignGroupHandler : AuditedCatalogEntryHandlerBase<OmnichannelCampaignGroup>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelCampaignGroupHandler"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public OmnichannelCampaignGroupHandler(
        IHttpContextAccessor httpContextAccessor,
        IClock clock,
        IStringLocalizer<OmnichannelCampaignGroupHandler> stringLocalizer)
        : base(httpContextAccessor, clock)
    {
        S = stringLocalizer;
    }

    public override Task ValidatingAsync(ValidatingContext<OmnichannelCampaignGroup> context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context.Model.DisplayText))
        {
            context.Result.Fail(new ValidationResult(S["Name is required."], [nameof(OmnichannelCampaignGroup.DisplayText)]));
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    protected override void Populate(OmnichannelCampaignGroup group, JsonNode data)
    {
        base.Populate(group, data);

        if (data is null)
        {
            return;
        }

        var displayText = data[nameof(OmnichannelCampaignGroup.DisplayText)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(displayText))
        {
            group.DisplayText = displayText;
        }
    }
}
