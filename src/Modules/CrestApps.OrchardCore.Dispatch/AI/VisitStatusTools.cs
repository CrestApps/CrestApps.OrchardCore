using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Dispatch.Core;
using CrestApps.OrchardCore.Dispatch.Models;
using CrestApps.OrchardCore.Dispatch.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Dispatch.AI;

/// <summary>
/// Moves a visit along to Dispatched, En route, On site or Paused, as the visit page's status buttons do.
/// </summary>
internal sealed class SetVisitStatusTool : DispatchAdminToolBase
{
    public const string ToolName = "setVisitStatus";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "visit": { "type": "string", "description": "The visit's number (V-000012), its ID, or its work order's number for the work order's open visit." },
        "status": { "type": "string", "enum": ["Dispatched", "EnRoute", "OnSite", "Paused"], "description": "The new status. Use closeVisit to complete, mark incomplete or cancel a visit." },
        "note": { "type": "string", "description": "Optional. A note kept in the visit's history." }
      },
      "required": ["visit", "status"],
      "additionalProperties": false
    }
    """);

    private static readonly VisitStatus[] _allowed = [VisitStatus.Dispatched, VisitStatus.EnRoute, VisitStatus.OnSite, VisitStatus.Paused];

    public override string Name => ToolName;

    public override string Description => "Moves a visit to Dispatched, En route, On site or Paused, with an optional note. En route texts or emails the customer when customer notices are on.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => DispatchPermissions.ManageDispatch;

    protected override Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
        => VisitStatusUpdate.RunAsync(arguments, services, user, _allowed, noteRequired: false, cancellationToken);
}

/// <summary>
/// Ends a visit as Completed, Incomplete or Canceled, as the visit page's status buttons do.
/// </summary>
internal sealed class CloseVisitTool : DispatchAdminToolBase
{
    public const string ToolName = "closeVisit";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "visit": { "type": "string", "description": "The visit's number (V-000012), its ID, or its work order's number for the work order's open visit." },
        "status": { "type": "string", "enum": ["Completed", "Incomplete", "Canceled"], "description": "How the visit ends. Incomplete makes a return visit for the work order while it is open." },
        "note": { "type": "string", "description": "What happened. Required for Incomplete (why it couldn't be finished) and Canceled (why it is canceled)." }
      },
      "required": ["visit", "status"],
      "additionalProperties": false
    }
    """);

    private static readonly VisitStatus[] _allowed = [VisitStatus.Completed, VisitStatus.Incomplete, VisitStatus.Canceled];

    public override string Name => ToolName;

    public override string Description => "Ends a visit as Completed, Incomplete or Canceled, with a note. These are final, and Completed and Canceled notify the customer when notices are on.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => DispatchPermissions.ManageDispatch;

    protected override Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
        => VisitStatusUpdate.RunAsync(arguments, services, user, _allowed, noteRequired: true, cancellationToken);
}

/// <summary>
/// The status change both visit status tools make through <see cref="IDispatchService.ChangeStatusAsync"/>.
/// </summary>
internal static class VisitStatusUpdate
{
    public static async Task<object> RunAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, IReadOnlyCollection<VisitStatus> allowed, bool noteRequired, CancellationToken cancellationToken)
    {
        var reference = DispatchAdminTools.Read(arguments, "visit");

        if (reference is null)
        {
            return "Give the visit's number or ID.";
        }

        if (!DispatchAdminTools.TryReadEnum<VisitStatus>(arguments, "status", out var status) || !allowed.Contains(status))
        {
            return $"The status must be one of: {string.Join(", ", allowed)}.";
        }

        var note = DispatchAdminTools.Read(arguments, "note");

        if (noteRequired && note is null && status is VisitStatus.Incomplete or VisitStatus.Canceled)
        {
            return status == VisitStatus.Canceled
                ? "Nothing was changed: say why the visit is canceled (note)."
                : "Nothing was changed: say why the visit couldn't be finished (note).";
        }

        var visit = await DispatchAdminTools.FindVisitAsync(services, reference, cancellationToken);

        if (visit is null)
        {
            return $"No visit or work order is numbered '{reference}'. Use listVisits to find it.";
        }

        var previous = visit.Status;

        if (previous != status && !VisitStatusRules.CanMove(previous, status))
        {
            var next = VisitStatusRules.Next(previous);

            return $"Nothing was changed: a visit can't move from {previous.Text()} to {status.Text()}." + (next.Count == 0
                ? " The visit is closed."
                : $" From {previous.Text()} it can move to: {string.Join(", ", next)}.");
        }

        var result = await services.GetRequiredService<IDispatchService>().ChangeStatusAsync(visit.ItemId, status, note, user.Identity?.Name, cancellationToken);

        if (!result.Succeeded)
        {
            return $"Nothing was changed: {string.Join(" ", result.Errors)}";
        }

        await DispatchAdminTools.SaveAsync(services, cancellationToken);

        var zone = await DispatchAdminTools.TimeZoneAsync(services);
        var names = await DispatchAdminTools.WorkerNamesAsync(services, cancellationToken);

        return DispatchAdminTools.Json(new
        {
            previousStatus = previous.ToString(),
            changed = previous != status,
            eta = status == VisitStatus.EnRoute ? DispatchAdminTools.Local(result.Visit.EtaUtc, zone) : null,
            visit = DispatchAdminTools.Changed(result.Visit, names, zone),
        });
    }
}
