using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Core.Handlers;
using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Handlers;

internal sealed class CampaignActionHandler : CatalogEntryHandlerBase<CampaignAction>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IClock _clock;
    private readonly ICatalog<OmnichannelCampaign> _campaignsCatalog;
    private readonly ICatalog<OmnichannelDisposition> _dispositionsCatalog;
    private readonly CampaignActionOptions _options;

    internal readonly IStringLocalizer S;

    public CampaignActionHandler(
        IHttpContextAccessor httpContextAccessor,
        IClock clock,
        ICatalog<OmnichannelCampaign> campaignsCatalog,
        ICatalog<OmnichannelDisposition> dispositionsCatalog,
        IOptions<CampaignActionOptions> options,
        IStringLocalizer<CampaignActionHandler> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _clock = clock;
        _campaignsCatalog = campaignsCatalog;
        _dispositionsCatalog = dispositionsCatalog;
        _options = options.Value;
        S = stringLocalizer;
    }

    public override Task InitializingAsync(InitializingContext<CampaignAction> context)
        => PopulateAsync(context.Model, context.Data);

    public override Task UpdatingAsync(UpdatingContext<CampaignAction> context)
        => PopulateAsync(context.Model, context.Data);

    public override async Task ValidatingAsync(ValidatingContext<CampaignAction> context)
    {
        var model = context.Model;

        if (string.IsNullOrWhiteSpace(model.CampaignId))
        {
            context.Result.Fail(new ValidationResult(S["The campaign is required."], [nameof(CampaignAction.CampaignId)]));
        }
        else
        {
            var campaign = await _campaignsCatalog.FindByIdAsync(model.CampaignId);

            if (campaign is null)
            {
                context.Result.Fail(new ValidationResult(S["The selected campaign does not exist."], [nameof(CampaignAction.CampaignId)]));
            }
        }

        if (string.IsNullOrWhiteSpace(model.DispositionId))
        {
            context.Result.Fail(new ValidationResult(S["The disposition is required."], [nameof(CampaignAction.DispositionId)]));
        }
        else
        {
            var disposition = await _dispositionsCatalog.FindByIdAsync(model.DispositionId);

            if (disposition is null)
            {
                context.Result.Fail(new ValidationResult(S["The selected disposition does not exist."], [nameof(CampaignAction.DispositionId)]));
            }
        }

        if (string.IsNullOrWhiteSpace(model.Source))
        {
            context.Result.Fail(new ValidationResult(S["The action type is required."], [nameof(CampaignAction.Source)]));
        }
        else if (!_options.ActionTypes.ContainsKey(model.Source))
        {
            context.Result.Fail(new ValidationResult(S["The action type '{0}' is not valid.", model.Source], [nameof(CampaignAction.Source)]));
        }
    }

    public override Task InitializedAsync(InitializedContext<CampaignAction> context)
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

    private static Task PopulateAsync(CampaignAction action, JsonNode data)
    {
        if (data is null)
        {
            return Task.CompletedTask;
        }

        var campaignId = data[nameof(CampaignAction.CampaignId)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(campaignId))
        {
            action.CampaignId = campaignId;
        }

        var dispositionId = data[nameof(CampaignAction.DispositionId)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(dispositionId))
        {
            action.DispositionId = dispositionId;
        }

        var properties = data[nameof(CampaignAction.Properties)]?.AsObject();

        if (properties != null)
        {
            action.Properties ??= [];
            action.Properties.Merge(properties);
        }

        return Task.CompletedTask;
    }
}
