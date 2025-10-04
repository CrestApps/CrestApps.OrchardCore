using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Core.Handlers;
using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Handlers;

internal sealed class OmnichannelActivityBatchHandler : ModelHandlerBase<OmnichannelActivityBatch>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public OmnichannelActivityBatchHandler(
        IHttpContextAccessor httpContextAccessor,
        IClock clock,
        IStringLocalizer<OmnichannelCampaignHandler> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _clock = clock;
        S = stringLocalizer;
    }

    public override Task InitializingAsync(InitializingContext<OmnichannelActivityBatch> context)
        => PopulateAsync(context.Model, context.Data);

    public override Task UpdatingAsync(UpdatingContext<OmnichannelActivityBatch> context)
        => PopulateAsync(context.Model, context.Data);

    public override Task ValidatingAsync(ValidatingContext<OmnichannelActivityBatch> context)
    {
        if (string.IsNullOrWhiteSpace(context.Model.DisplayText))
        {
            context.Result.Fail(new ValidationResult(S["Name is required."], [nameof(OmnichannelActivityBatch.DisplayText)]));
        }

        return Task.CompletedTask;
    }

    public override Task InitializedAsync(InitializedContext<OmnichannelActivityBatch> context)
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

    private static Task PopulateAsync(OmnichannelActivityBatch enabpoint, JsonNode data)
    {
        var displayText = data[nameof(OmnichannelCampaign.DisplayText)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(displayText))
        {
            enabpoint.DisplayText = displayText;
        }

        var properties = data[nameof(OmnichannelCampaign.Properties)]?.AsObject();

        if (properties != null)
        {
            enabpoint.Properties ??= [];
            enabpoint.Properties.Merge(properties);
        }

        return Task.CompletedTask;
    }
}
