using CrestApps.Core;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Entities;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Endpoints;

internal static class SubjectActionEndpoints
{
    public const string SubjectDispositionActionsRouteName = "CrestApps.Omnichannel.SubjectDispositionActions";

    public static IEndpointRouteBuilder AddSubjectDispositionActionsEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapPost("api/omnichannel/subject-disposition-actions", HandleAsync)
            .AllowAnonymous()
            .DisableAntiforgery()
            .WithName(SubjectDispositionActionsRouteName);

        return builder;
    }

    private static async Task<IResult> HandleAsync(
        SubjectDispositionActionsRequest request,
        IAuthorizationService authorizationService,
        ISourceCatalog<SubjectAction> actionCatalog,
        IOptions<SubjectActionOptions> actionOptions,
        IContentDefinitionManager contentDefinitionManager,
        IStringLocalizer<SubjectActionEndpointsMarker> S,
        IClock clock,
        HttpContext httpContext)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, OmnichannelConstants.Permissions.CompleteActivity))
        {
            return Results.Forbid();
        }

        if (string.IsNullOrEmpty(request?.SubjectContentType) || string.IsNullOrEmpty(request?.DispositionId))
        {
            return Results.BadRequest();
        }

        var allActions = await actionCatalog.GetAllAsync();

        var actions = allActions
            .Where(a => string.Equals(a.SubjectContentType, request.SubjectContentType, StringComparison.OrdinalIgnoreCase)
                     && string.Equals(a.DispositionId, request.DispositionId, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var options = actionOptions.Value;
        var now = clock.UtcNow;

        var responses = await Task.WhenAll(actions.Select(async action =>
        {
            if (string.Equals(action.Source, OmnichannelConstants.ActionTypes.Finish, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

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

            var previewTitle = await ResolvePreviewTitleAsync(action, request, contentDefinitionManager, S);

            return new SubjectDispositionActionResponse
            {
                ActionId = action.ItemId,
                ActionType = action.Source,
                ActionTypeDisplayName = typeDisplayName,
                PreviewTitle = string.IsNullOrWhiteSpace(previewTitle) ? typeDisplayName : previewTitle,
                RequiresScheduleDate = requiresScheduleDate,
                DefaultScheduleDate = defaultScheduleDate,
            };
        }));

        var result = responses
            .OfType<SubjectDispositionActionResponse>()
            .OrderBy(x => x.RequiresScheduleDate ? 1 : 0)
            .ThenBy(x => x.DefaultScheduleDate)
            .ToArray();

        return Results.Ok(result);
    }

    private static async Task<string> ResolvePreviewTitleAsync(
        SubjectAction action,
        SubjectDispositionActionsRequest request,
        IContentDefinitionManager contentDefinitionManager,
        IStringLocalizer stringLocalizer)
    {
        if (string.Equals(action.Source, OmnichannelConstants.ActionTypes.TryAgain, StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(request?.CurrentSubjectTitle))
            {
                return stringLocalizer["{0} {1} attempt", request.CurrentSubjectTitle, ToOrdinal(request.CurrentAttempts + 1)].Value;
            }

            return null;
        }

        if (!string.Equals(action.Source, OmnichannelConstants.ActionTypes.NewActivity, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string subjectContentType = null;

        if (action.TryGet<NewActivityActionMetadata>(out var newActivityMeta) && !string.IsNullOrWhiteSpace(newActivityMeta.SubjectContentType))
        {
            subjectContentType = newActivityMeta.SubjectContentType;
        }
        else
        {
            subjectContentType = request?.SubjectContentType;
        }

        if (string.IsNullOrWhiteSpace(subjectContentType))
        {
            return null;
        }

        var contentTypeDefinition = await contentDefinitionManager.GetTypeDefinitionAsync(subjectContentType);

        return contentTypeDefinition?.DisplayName ?? subjectContentType;
    }

    private static string ToOrdinal(int number)
    {
        if (number <= 0)
        {
            return number.ToString();
        }

        var lastTwoDigits = number % 100;
        var lastDigit = number % 10;
        var suffix = lastTwoDigits is 11 or 12 or 13
            ? "th"
            : lastDigit switch
            {
                1 => "st",
                2 => "nd",
                3 => "rd",
                _ => "th",
            };

        return $"{number}{suffix}";
    }

    private sealed class SubjectActionEndpointsMarker
    {
    }
}

internal sealed class SubjectDispositionActionsRequest
{
    /// <summary>
    /// Gets or sets the subject content type to look up actions for.
    /// </summary>
    public string SubjectContentType { get; set; }

    /// <summary>
    /// Gets or sets the disposition identifier.
    /// </summary>
    public string DispositionId { get; set; }

    /// <summary>
    /// Gets or sets the current subject title for preview purposes.
    /// </summary>
    public string CurrentSubjectTitle { get; set; }

    /// <summary>
    /// Gets or sets the current attempt number for preview purposes.
    /// </summary>
    public int CurrentAttempts { get; set; }
}

internal sealed class SubjectDispositionActionResponse
{
    /// <summary>
    /// Gets or sets the action identifier.
    /// </summary>
    public string ActionId { get; set; }

    /// <summary>
    /// Gets or sets the action type.
    /// </summary>
    public string ActionType { get; set; }

    /// <summary>
    /// Gets or sets the action type display name.
    /// </summary>
    public string ActionTypeDisplayName { get; set; }

    /// <summary>
    /// Gets or sets the preview title.
    /// </summary>
    public string PreviewTitle { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this action requires a schedule date.
    /// </summary>
    public bool RequiresScheduleDate { get; set; }

    /// <summary>
    /// Gets or sets the default schedule date.
    /// </summary>
    public DateTime? DefaultScheduleDate { get; set; }
}
