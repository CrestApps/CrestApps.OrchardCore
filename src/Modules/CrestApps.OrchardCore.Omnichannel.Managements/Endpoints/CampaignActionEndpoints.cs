using CrestApps.Core;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using OrchardCore.Entities;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Endpoints;

internal static class CampaignActionEndpoints
{
    public static IEndpointRouteBuilder AddDispositionActionsEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapPost("api/omnichannel/disposition-actions", HandleAsync)
            .AllowAnonymous()
            .DisableAntiforgery();

        return builder;
    }

    private static async Task<IResult> HandleAsync(
        DispositionActionsRequest request,
        IAuthorizationService authorizationService,
        ISourceCatalog<CampaignAction> actionCatalog,
        ICatalog<OmnichannelDisposition> dispositionCatalog,
        IOptions<CampaignActionOptions> actionOptions,
        IClock clock,
        HttpContext httpContext)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, OmnichannelConstants.Permissions.CompleteActivity))
        {
            return Results.Forbid();
        }

        if (string.IsNullOrEmpty(request?.CampaignId) || string.IsNullOrEmpty(request?.DispositionId))
        {
            return Results.BadRequest();
        }

        var allActions = await actionCatalog.GetAllAsync();

        var actions = allActions
            .Where(a => string.Equals(a.CampaignId, request.CampaignId, StringComparison.OrdinalIgnoreCase)
                     && string.Equals(a.DispositionId, request.DispositionId, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var options = actionOptions.Value;
        var now = clock.UtcNow;

        var result = actions.Select(action =>
        {
            var typeDisplayName = options.ActionTypes.TryGetValue(action.Source, out var typeEntry)
                ? typeEntry.DisplayName?.Value
                : action.Source;

            int? defaultScheduleHours = null;
            var requiresScheduleDate = false;

            if (string.Equals(action.Source, OmnichannelConstants.ActionTypes.TryAgain, StringComparison.OrdinalIgnoreCase))
            {
                requiresScheduleDate = true;

                if (action.TryGet<TryAgainActionMetadata>(out var tryAgainMeta))
                {
                    defaultScheduleHours = tryAgainMeta.DefaultScheduleHours;
                }
            }
            else if (string.Equals(action.Source, OmnichannelConstants.ActionTypes.NewActivity, StringComparison.OrdinalIgnoreCase))
            {
                requiresScheduleDate = true;

                if (action.TryGet<NewActivityActionMetadata>(out var newActivityMeta))
                {
                    defaultScheduleHours = newActivityMeta.DefaultScheduleHours;
                }
            }

            DateTime? defaultScheduleDate = null;

            if (requiresScheduleDate)
            {
                defaultScheduleDate = defaultScheduleHours.HasValue
                    ? now.AddHours(defaultScheduleHours.Value)
                    : now.AddDays(1);
            }

            return new DispositionActionResponse
            {
                ActionId = action.ItemId,
                ActionType = action.Source,
                ActionTypeDisplayName = typeDisplayName,
                RequiresScheduleDate = requiresScheduleDate,
                DefaultScheduleDate = defaultScheduleDate,
            };
        }).ToArray();

        return Results.Ok(result);
    }
}

internal sealed class DispositionActionsRequest
{
    public string CampaignId { get; set; }

    public string DispositionId { get; set; }
}

internal sealed class DispositionActionResponse
{
    public string ActionId { get; set; }

    public string ActionType { get; set; }

    public string ActionTypeDisplayName { get; set; }

    public bool RequiresScheduleDate { get; set; }

    public DateTime? DefaultScheduleDate { get; set; }
}
