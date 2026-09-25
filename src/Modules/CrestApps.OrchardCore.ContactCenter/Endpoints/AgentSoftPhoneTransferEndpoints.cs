using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace CrestApps.OrchardCore.ContactCenter.Endpoints;

/// <summary>
/// The soft phone's transfer panel for Contact Center calls: where the call can go, and the blind and warm transfers
/// that send it there through the Contact Center rather than straight to the provider.
/// </summary>
/// <remarks>
/// Every refusal is written as a status with a problem body. The soft phone calls these with <c>fetch</c>, which
/// follows the sign-in redirect a challenge would produce and reads the page as a success.
/// </remarks>
internal static class AgentSoftPhoneTransferEndpoints
{
    public const string TargetsRouteName = "ContactCenterSoftPhoneTransferTargets";
    public const string TransferRouteName = "ContactCenterSoftPhoneTransfer";
    public const string ConsultStartRouteName = "ContactCenterSoftPhoneConsultStart";
    public const string ConsultStatusRouteName = "ContactCenterSoftPhoneConsultStatus";
    public const string ConsultCompleteRouteName = "ContactCenterSoftPhoneConsultComplete";
    public const string ConsultCancelRouteName = "ContactCenterSoftPhoneConsultCancel";

    // The phone reads presence and target kinds as words, the same way the Telephony hub sends call state.
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static IEndpointRouteBuilder AddAgentSoftPhoneTransferEndpoints(this IEndpointRouteBuilder builder, string adminUrlPrefix)
    {
        var routePrefix = string.IsNullOrWhiteSpace(adminUrlPrefix)
            ? "Admin"
            : adminUrlPrefix.Trim('/');
        var basePath = $"{routePrefix}/contact-center/agent/soft-phone/transfer";

        builder.MapGet($"{basePath}/targets", HandleTargetsAsync).WithName(TargetsRouteName);
        builder.MapPost(basePath, HandleTransferAsync).WithName(TransferRouteName);
        builder.MapPost($"{basePath}/consult", HandleConsultStartAsync).WithName(ConsultStartRouteName);
        builder.MapGet($"{basePath}/consult", HandleConsultStatusAsync).WithName(ConsultStatusRouteName);
        builder.MapPost($"{basePath}/consult/complete", HandleConsultCompleteAsync).WithName(ConsultCompleteRouteName);
        builder.MapPost($"{basePath}/consult/cancel", HandleConsultCancelAsync).WithName(ConsultCancelRouteName);

        return builder;
    }

    internal static async Task<IResult> HandleTargetsAsync(
        string interactionId,
        IAuthorizationService authorizationService,
        ICallControlAuthorizationService callControlAuthorizationService,
        IInteractionManager interactionManager,
        IContactCenterTransferDirectoryService directoryService,
        HttpContext httpContext)
    {
        var (userId, refusal) = await AuthorizeAgentAsync(authorizationService, httpContext);

        if (refusal is not null)
        {
            return refusal;
        }

        var interaction = string.IsNullOrWhiteSpace(interactionId)
            ? null
            : await interactionManager.FindByIdAsync(interactionId, httpContext.RequestAborted);

        if (interaction is null)
        {
            return ContactCenterApiResults.NotFound("The call is not available.");
        }

        // Only the agent on the call may see where it can go: the directory names every agent and their state.
        var authorization = await callControlAuthorizationService.AuthorizeAsync(new CallControlAuthorizationContext
        {
            Principal = httpContext.User,
            UserId = userId,
            Verb = CallControlVerb.Transfer,
            InteractionId = interaction.ItemId,
            ProviderName = interaction.ProviderName,
        }, httpContext.RequestAborted);

        if (!authorization.Succeeded)
        {
            return ContactCenterApiResults.NotFound("The call is not available.");
        }

        var directory = await directoryService.GetAsync(userId, httpContext.User, interaction.ProviderName, httpContext.RequestAborted);

        return TypedResults.Json(directory, _jsonOptions, statusCode: StatusCodes.Status200OK);
    }

    internal static async Task<IResult> HandleTransferAsync(
        SoftPhoneTransferBody body,
        IAuthorizationService authorizationService,
        IAntiforgery antiforgery,
        IContactCenterTransferService transferService,
        HttpContext httpContext)
    {
        var (userId, refusal) = await AuthorizeCommandAsync(authorizationService, antiforgery, httpContext);

        if (refusal is not null)
        {
            return refusal;
        }

        if (!TryReadTarget(body, out var targetType))
        {
            return TypedResults.Problem(detail: "Choose an agent, a queue or a number to transfer to.", statusCode: StatusCodes.Status400BadRequest);
        }

        var result = await transferService.TransferAsync(new TransferRequest
        {
            InteractionId = body.InteractionId,
            Type = InteractionTransferType.Blind,
            TargetType = targetType,
            TargetId = body.TargetId.Trim(),
            InitiatedByUserId = userId,
            Principal = httpContext.User,
        }, httpContext.RequestAborted);

        return TypedResults.Json(new SoftPhoneTransferResponse
        {
            Succeeded = result.Succeeded,
            Message = result.Succeeded ? result.Reason : null,
            Error = result.Succeeded ? null : result.Reason,
        }, _jsonOptions, statusCode: StatusCodes.Status200OK);
    }

    internal static async Task<IResult> HandleConsultStartAsync(
        SoftPhoneTransferBody body,
        IAuthorizationService authorizationService,
        IAntiforgery antiforgery,
        IWarmTransferService warmTransferService,
        HttpContext httpContext)
    {
        var (userId, refusal) = await AuthorizeCommandAsync(authorizationService, antiforgery, httpContext);

        if (refusal is not null)
        {
            return refusal;
        }

        if (!TryReadTarget(body, out var targetType))
        {
            return TypedResults.Problem(detail: "Choose an agent or a number to consult.", statusCode: StatusCodes.Status400BadRequest);
        }

        var result = await warmTransferService.StartAsync(new WarmTransferRequest
        {
            InteractionId = body.InteractionId,
            UserId = userId,
            Principal = httpContext.User,
            TargetType = targetType,
            TargetId = body.TargetId.Trim(),
        }, httpContext.RequestAborted);

        return Consult(result);
    }

    internal static async Task<IResult> HandleConsultStatusAsync(
        string interactionId,
        string consultId,
        IAuthorizationService authorizationService,
        IWarmTransferService warmTransferService,
        HttpContext httpContext)
    {
        var (userId, refusal) = await AuthorizeAgentAsync(authorizationService, httpContext);

        if (refusal is not null)
        {
            return refusal;
        }

        var result = await warmTransferService.GetAsync(new WarmTransferCommand
        {
            InteractionId = interactionId,
            ConsultId = consultId,
            UserId = userId,
            Principal = httpContext.User,
        }, httpContext.RequestAborted);

        return Consult(result);
    }

    internal static Task<IResult> HandleConsultCompleteAsync(
        SoftPhoneConsultBody body,
        IAuthorizationService authorizationService,
        IAntiforgery antiforgery,
        IWarmTransferService warmTransferService,
        HttpContext httpContext)
        => HandleConsultCommandAsync(body, authorizationService, antiforgery, httpContext, warmTransferService.CompleteAsync);

    internal static Task<IResult> HandleConsultCancelAsync(
        SoftPhoneConsultBody body,
        IAuthorizationService authorizationService,
        IAntiforgery antiforgery,
        IWarmTransferService warmTransferService,
        HttpContext httpContext)
        => HandleConsultCommandAsync(body, authorizationService, antiforgery, httpContext, warmTransferService.CancelAsync);

    private static async Task<IResult> HandleConsultCommandAsync(
        SoftPhoneConsultBody body,
        IAuthorizationService authorizationService,
        IAntiforgery antiforgery,
        HttpContext httpContext,
        Func<WarmTransferCommand, CancellationToken, Task<WarmTransferResult>> command)
    {
        var (userId, refusal) = await AuthorizeCommandAsync(authorizationService, antiforgery, httpContext);

        if (refusal is not null)
        {
            return refusal;
        }

        var result = await command(new WarmTransferCommand
        {
            InteractionId = body?.InteractionId,
            ConsultId = body?.ConsultId,
            UserId = userId,
            Principal = httpContext.User,
        }, httpContext.RequestAborted);

        return Consult(result);
    }

    private static async Task<(string UserId, IResult Refusal)> AuthorizeAgentAsync(IAuthorizationService authorizationService, HttpContext httpContext)
    {
        if (httpContext.User?.Identity?.IsAuthenticated != true)
        {
            return (null, ContactCenterApiResults.Unauthorized());
        }

        if (!await authorizationService.AuthorizeAsync(httpContext.User, ContactCenterPermissions.SignIntoQueues))
        {
            return (null, ContactCenterApiResults.Forbidden());
        }

        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

        return string.IsNullOrEmpty(userId)
            ? (null, ContactCenterApiResults.Forbidden())
            : (userId, null);
    }

    private static async Task<(string UserId, IResult Refusal)> AuthorizeCommandAsync(
        IAuthorizationService authorizationService,
        IAntiforgery antiforgery,
        HttpContext httpContext)
    {
        var (userId, refusal) = await AuthorizeAgentAsync(authorizationService, httpContext);

        if (refusal is not null)
        {
            return (null, refusal);
        }

        // A transfer moves a live customer, so it is never something another site can make the agent's browser do.
        if (!await ContactCenterEndpointAntiforgery.ValidateRequestAsync(antiforgery, httpContext))
        {
            return (null, TypedResults.Problem(detail: "The request could not be verified. Reload the page and try again.", statusCode: StatusCodes.Status400BadRequest));
        }

        return (userId, null);
    }

    private static bool TryReadTarget(SoftPhoneTransferBody body, out InteractionTransferTargetType targetType)
    {
        targetType = default;

        return body is not null &&
            !string.IsNullOrWhiteSpace(body.InteractionId) &&
            !string.IsNullOrWhiteSpace(body.TargetId) &&
            Enum.TryParse(body.TargetType, ignoreCase: true, out targetType) &&
            targetType is InteractionTransferTargetType.Agent or InteractionTransferTargetType.Queue or InteractionTransferTargetType.External;
    }

    private static JsonHttpResult<SoftPhoneConsultResponse> Consult(WarmTransferResult result)
        => TypedResults.Json(new SoftPhoneConsultResponse
        {
            Succeeded = result.Succeeded,
            Message = result.Succeeded ? result.Reason : null,
            Error = result.Succeeded ? null : result.Reason,
            Consult = string.IsNullOrEmpty(result.ConsultId)
                ? null
                : new SoftPhoneConsultState
                {
                    Id = result.ConsultId,
                    Status = result.Status switch
                    {
                        ConsultCallStatus.Connected => "connected",
                        ConsultCallStatus.Completed => "completed",
                        ConsultCallStatus.Cancelled => "cancelled",
                        ConsultCallStatus.Failed => "failed",
                        _ => "ringing",
                    },
                    Live = result.IsLive,
                    TargetType = result.TargetType?.ToString().ToLowerInvariant(),
                    TargetId = result.TargetId,
                },
        }, _jsonOptions, statusCode: StatusCodes.Status200OK);
}

/// <summary>A soft phone's transfer or consult request.</summary>
internal sealed class SoftPhoneTransferBody
{
    /// <summary>Gets or sets the interaction the agent is on.</summary>
    public string InteractionId { get; set; }

    /// <summary>Gets or sets the kind of destination: <c>agent</c>, <c>queue</c> or <c>external</c>.</summary>
    public string TargetType { get; set; }

    /// <summary>Gets or sets the destination: an agent or queue id, an approved destination id, or a number.</summary>
    public string TargetId { get; set; }
}

/// <summary>A soft phone's command on a consult it started.</summary>
internal sealed class SoftPhoneConsultBody
{
    /// <summary>Gets or sets the interaction the agent is on.</summary>
    public string InteractionId { get; set; }

    /// <summary>Gets or sets the consult.</summary>
    public string ConsultId { get; set; }
}

/// <summary>The answer to a blind transfer request.</summary>
internal sealed class SoftPhoneTransferResponse
{
    /// <summary>Gets or sets a value indicating whether the transfer went ahead.</summary>
    public bool Succeeded { get; set; }

    /// <summary>Gets or sets what happened to the call.</summary>
    public string Message { get; set; }

    /// <summary>Gets or sets why the transfer was refused.</summary>
    public string Error { get; set; }
}

/// <summary>The answer to a consult command.</summary>
internal sealed class SoftPhoneConsultResponse
{
    /// <summary>Gets or sets a value indicating whether the command did what was asked.</summary>
    public bool Succeeded { get; set; }

    /// <summary>Gets or sets what happened.</summary>
    public string Message { get; set; }

    /// <summary>Gets or sets why the command was refused.</summary>
    public string Error { get; set; }

    /// <summary>Gets or sets where the consult stands.</summary>
    public SoftPhoneConsultState Consult { get; set; }
}

/// <summary>Where a consult stands, as the soft phone shows it.</summary>
internal sealed class SoftPhoneConsultState
{
    /// <summary>Gets or sets the consult.</summary>
    public string Id { get; set; }

    /// <summary>Gets or sets the state: <c>ringing</c>, <c>connected</c>, <c>completed</c>, <c>cancelled</c> or <c>failed</c>.</summary>
    public string Status { get; set; }

    /// <summary>Gets or sets a value indicating whether the consult is still going.</summary>
    public bool Live { get; set; }

    /// <summary>Gets or sets the kind of destination: <c>agent</c> or <c>external</c>.</summary>
    public string TargetType { get; set; }

    /// <summary>Gets or sets the destination.</summary>
    public string TargetId { get; set; }
}
