using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace CrestApps.OrchardCore.ContactCenter.Endpoints;

/// <summary>
/// The live dashboard's interventions on a call and on an agent. Each requires Monitor Contact Center and a valid
/// antiforgery token, is limited to calls in the supervisor's queues, and is authorized again, and audited, by the
/// service that carries it out.
/// </summary>
internal static class SupervisorInterventionEndpoints
{
    public const string StopRouteName = "ContactCenterSupervisorDashboardStop";
    public const string SwitchRouteName = "ContactCenterSupervisorDashboardSwitch";
    public const string TakeOverRouteName = "ContactCenterSupervisorDashboardTakeOver";
    public const string EndCallRouteName = "ContactCenterSupervisorDashboardEndCall";
    public const string TransferRouteName = "ContactCenterSupervisorDashboardTransfer";
    public const string RecordingRouteName = "ContactCenterSupervisorDashboardRecording";
    public const string AgentStateRouteName = "ContactCenterSupervisorDashboardAgentState";
    public const string MessageRouteName = "ContactCenterSupervisorDashboardMessage";

    // The value of the agent-state request that signs the agent out rather than setting a state.
    private const string SignOutState = "SignOut";

    public static IEndpointRouteBuilder AddSupervisorInterventionEndpoints(this IEndpointRouteBuilder builder)
    {
        builder.MapPost("Admin/contact-center/dashboard/stop", HandleStopAsync).WithName(StopRouteName);
        builder.MapPost("Admin/contact-center/dashboard/switch", HandleSwitchAsync).WithName(SwitchRouteName);
        builder.MapPost("Admin/contact-center/dashboard/takeover", HandleTakeOverAsync).WithName(TakeOverRouteName);
        builder.MapPost("Admin/contact-center/dashboard/end-call", HandleEndCallAsync).WithName(EndCallRouteName);
        builder.MapPost("Admin/contact-center/dashboard/transfer", HandleTransferAsync).WithName(TransferRouteName);
        builder.MapPost("Admin/contact-center/dashboard/recording", HandleRecordingAsync).WithName(RecordingRouteName);
        builder.MapPost("Admin/contact-center/dashboard/agent-state", HandleAgentStateAsync).WithName(AgentStateRouteName);
        builder.MapPost("Admin/contact-center/dashboard/message", HandleMessageAsync).WithName(MessageRouteName);

        return builder;
    }

    private static Task<IResult> HandleStopAsync(
        [FromForm] InteractionRequest request,
        IContactCenterMonitoringService monitoring,
        ICallSessionManager sessions,
        CallScope scope,
        HttpContext httpContext)
        => scope.RunAsync(httpContext, request.InteractionId, async supervisorId =>
        {
            if (PhoneCallKey.IsPhoneCall(request.InteractionId))
            {
                return await scope.PhoneCalls.StopAsync(request.InteractionId, supervisorId, httpContext.User, httpContext.RequestAborted);
            }

            var session = await sessions.FindByInteractionIdAsync(request.InteractionId, httpContext.RequestAborted);
            var engagement = session?.ActiveMonitorSessions.FirstOrDefault(monitorSession =>
                string.Equals(monitorSession.SupervisorUserId, supervisorId, StringComparison.Ordinal));

            return engagement is null
                ? SupervisorEngagementResult.Success()
                : await monitoring.StopEngagementAsync(request.InteractionId, supervisorId, httpContext.User, engagement.Mode, httpContext.RequestAborted);
        });

    private static Task<IResult> HandleSwitchAsync(
        [FromForm] SwitchRequest request,
        IContactCenterMonitoringService monitoring,
        CallScope scope,
        HttpContext httpContext)
        => scope.RunAsync(httpContext, request.InteractionId, supervisorId => PhoneCallKey.IsPhoneCall(request.InteractionId)
            ? scope.PhoneCalls.SwitchModeAsync(request.InteractionId, supervisorId, httpContext.User, request.Mode, httpContext.RequestAborted)
            : monitoring.SwitchModeAsync(request.InteractionId, supervisorId, httpContext.User, request.Mode, httpContext.RequestAborted));

    private static Task<IResult> HandleTakeOverAsync(
        [FromForm] InteractionRequest request,
        IContactCenterSupervisorInterventionService interventions,
        CallScope scope,
        HttpContext httpContext)
        => scope.RunAsync(httpContext, request.InteractionId, supervisorId => PhoneCallKey.IsPhoneCall(request.InteractionId)
            ? scope.PhoneCalls.TakeOverAsync(request.InteractionId, supervisorId, httpContext.User, httpContext.RequestAborted)
            : interventions.TakeOverAsync(request.InteractionId, supervisorId, httpContext.User, httpContext.RequestAborted));

    private static Task<IResult> HandleEndCallAsync(
        [FromForm] InteractionRequest request,
        IContactCenterSupervisorInterventionService interventions,
        CallScope scope,
        HttpContext httpContext)
        => scope.RunAsync(httpContext, request.InteractionId, supervisorId => PhoneCallKey.IsPhoneCall(request.InteractionId)
            ? scope.PhoneCalls.EndCallAsync(request.InteractionId, supervisorId, httpContext.User, httpContext.RequestAborted)
            : interventions.EndCallAsync(request.InteractionId, supervisorId, httpContext.User, httpContext.RequestAborted));

    private static Task<IResult> HandleTransferAsync(
        [FromForm] TransferRequestForm request,
        IContactCenterSupervisorInterventionService interventions,
        CallScope scope,
        HttpContext httpContext)
        => scope.RunAsync(httpContext, request.InteractionId, supervisorId =>
            interventions.TransferAsync(request.InteractionId, supervisorId, httpContext.User, request.TargetType, request.TargetId, httpContext.RequestAborted));

    private static Task<IResult> HandleRecordingAsync(
        [FromForm] RecordingRequest request,
        IContactCenterSupervisorInterventionService interventions,
        CallScope scope,
        HttpContext httpContext)
        => scope.RunAsync(httpContext, request.InteractionId, supervisorId =>
            interventions.SetRecordingAsync(request.InteractionId, supervisorId, httpContext.User, request.Record, httpContext.RequestAborted));

    private static Task<IResult> HandleAgentStateAsync(
        [FromForm] AgentStateRequest request,
        IContactCenterSupervisorInterventionService interventions,
        CallScope scope,
        HttpContext httpContext)
        => scope.RunForAgentAsync(httpContext, async supervisorId =>
        {
            if (string.Equals(request.Status, SignOutState, StringComparison.OrdinalIgnoreCase))
            {
                return await interventions.SignOutAgentAsync(request.AgentId, supervisorId, httpContext.User, httpContext.RequestAborted);
            }

            return Enum.TryParse<AgentPresenceStatus>(request.Status, ignoreCase: true, out var status)
                ? await interventions.SetAgentStateAsync(request.AgentId, supervisorId, httpContext.User, status, request.Reason, httpContext.RequestAborted)
                : SupervisorEngagementResult.Failure("Choose a state.");
        });

    private static Task<IResult> HandleMessageAsync(
        [FromForm] MessageRequest request,
        IContactCenterSupervisorInterventionService interventions,
        CallScope scope,
        HttpContext httpContext)
        => scope.RunForAgentAsync(httpContext, supervisorId =>
            interventions.SendMessageAsync(
                request.AgentId,
                supervisorId,
                httpContext.User.Identity?.Name,
                httpContext.User,
                request.Text,
                httpContext.RequestAborted));

    /// <summary>
    /// The checks every intervention request passes before its service runs.
    /// </summary>
    internal sealed class CallScope
    {
        private readonly IAuthorizationService _authorizationService;
        private readonly IAntiforgery _antiforgery;
        private readonly IInteractionManager _interactionManager;
        private readonly ISupervisorQueueAuthorizationService _supervisorQueueAuthorizationService;

        public CallScope(
            IAuthorizationService authorizationService,
            IAntiforgery antiforgery,
            IInteractionManager interactionManager,
            ISupervisorQueueAuthorizationService supervisorQueueAuthorizationService,
            IContactCenterPhoneCallSupervisionService phoneCalls)
        {
            _authorizationService = authorizationService;
            _antiforgery = antiforgery;
            _interactionManager = interactionManager;
            _supervisorQueueAuthorizationService = supervisorQueueAuthorizationService;
            PhoneCalls = phoneCalls;
        }

        // An agent's own phone call, named with a PhoneCallKey where a Contact Center call is named by its interaction.
        public IContactCenterPhoneCallSupervisionService PhoneCalls { get; }

        public async Task<IResult> RunAsync(HttpContext httpContext, string interactionId, Func<string, Task<SupervisorEngagementResult>> action)
        {
            var (refusal, supervisorId) = await AdmitAsync(httpContext);

            if (refusal is not null)
            {
                return refusal;
            }

            if (string.IsNullOrEmpty(interactionId))
            {
                return TypedResults.BadRequest();
            }

            // A phone call has no interaction: the supervisor may act on it when they oversee a queue its agent works.
            if (PhoneCallKey.IsPhoneCall(interactionId))
            {
                return await PhoneCalls.IsAuthorizedAsync(httpContext.User, supervisorId, interactionId, httpContext.RequestAborted)
                    ? ToResult(await action(supervisorId))
                    : TypedResults.NotFound();
            }

            var interaction = await _interactionManager.FindByIdAsync(interactionId, httpContext.RequestAborted);

            if (interaction is null ||
                !await _supervisorQueueAuthorizationService.IsAuthorizedAsync(httpContext.User, supervisorId, interaction.QueueId, httpContext.RequestAborted))
            {
                return TypedResults.NotFound();
            }

            return ToResult(await action(supervisorId));
        }

        public async Task<IResult> RunForAgentAsync(HttpContext httpContext, Func<string, Task<SupervisorEngagementResult>> action)
        {
            var (refusal, supervisorId) = await AdmitAsync(httpContext);

            return refusal ?? ToResult(await action(supervisorId));
        }

        private async Task<(IResult Refusal, string SupervisorId)> AdmitAsync(HttpContext httpContext)
        {
            if (!await _authorizationService.AuthorizeAsync(httpContext.User, ContactCenterPermissions.MonitorContactCenter))
            {
                return (TypedResults.Forbid(), null);
            }

            if (!await ContactCenterEndpointAntiforgery.ValidateRequestAsync(_antiforgery, httpContext))
            {
                return (TypedResults.BadRequest(), null);
            }

            var supervisorId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

            return string.IsNullOrEmpty(supervisorId)
                ? (TypedResults.Forbid(), null)
                : (null, supervisorId);
        }

        private static IResult ToResult(SupervisorEngagementResult result)
            => Results.Ok(new
            {
                result.Succeeded,
                result.OutcomeUnknown,

                // A success can carry a note (a state that applies once the agent's work ends); a failure says why.
                Message = result.Succeeded ? result.Reason : null,
                ErrorMessage = result.Succeeded ? null : result.Reason,
            });
    }

    private class InteractionRequest
    {
        public string InteractionId { get; set; }
    }

    private sealed class SwitchRequest : InteractionRequest
    {
        public MonitorMode Mode { get; set; }
    }

    private sealed class TransferRequestForm : InteractionRequest
    {
        public InteractionTransferTargetType TargetType { get; set; }

        public string TargetId { get; set; }
    }

    private sealed class RecordingRequest : InteractionRequest
    {
        public bool Record { get; set; }
    }

    private sealed class AgentStateRequest
    {
        public string AgentId { get; set; }

        public string Status { get; set; }

        public string Reason { get; set; }
    }

    private sealed class MessageRequest
    {
        public string AgentId { get; set; }

        public string Text { get; set; }
    }
}
