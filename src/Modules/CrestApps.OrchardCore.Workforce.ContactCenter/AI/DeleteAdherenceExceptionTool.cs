using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Workforce.ContactCenter.Services;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;
using YesSql;
using static CrestApps.OrchardCore.Workforce.ContactCenter.AI.ContactCenterWorkforceTools;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.AI;

/// <summary>
/// Removes an adherence exception of a worker the user manages, as the Adherence Exceptions page's Delete does. The same
/// stretch can be recorded again with <c>addAdherenceException</c>, so it doesn't ask for approval.
/// </summary>
internal sealed class DeleteAdherenceExceptionTool : ContactCenterWorkforceToolBase
{
    public const string ToolName = "deleteAdherenceException";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "exceptionId": { "type": "string", "description": "The exception's ID, from listAdherenceExceptions or getAdherenceDay." }
      },
      "required": ["exceptionId"],
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Deletes an adherence exception, so the time counts against the agent again once their day is saved.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => WorkforcePermissions.ManageAdherence;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var id = Read(arguments, "exceptionId");

        if (id is null)
        {
            return "Give the exception's ID (exceptionId), from listAdherenceExceptions.";
        }

        var adherence = services.GetRequiredService<AdherenceService>();
        var exception = await adherence.FindExceptionAsync(id, cancellationToken);

        if (exception is null)
        {
            return $"No adherence exception has the ID '{id}'.";
        }

        var worker = await services.GetRequiredService<IStaffMemberManager>().FindByIdAsync(exception.WorkerId, cancellationToken);

        if (worker is not null && !await services.GetRequiredService<IWorkforceScopeService>().ManagesWorkerAsync(user, worker, cancellationToken))
        {
            return $"The user doesn't manage {worker.DisplayName}, so the exception was not deleted.";
        }

        adherence.RemoveException(exception);
        await services.GetRequiredService<ISession>().SaveChangesAsync(cancellationToken);

        return ToJson(new
        {
            deleted = true,
            id,
            worker = worker?.DisplayName ?? exception.WorkerId,
            reason = exception.Reason.ToString(),
            message = "The exception was removed. The time counts against the agent once their day is saved again.",
        });
    }
}
