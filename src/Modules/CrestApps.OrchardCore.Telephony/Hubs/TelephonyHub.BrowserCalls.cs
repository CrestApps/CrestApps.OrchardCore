using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telephony.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.Telephony.Hubs;

/// <summary>
/// The calls a soft phone places itself. A client-originated provider (such as Telnyx) places the call straight from
/// the browser: it never passes through a server "Dial" action and the platform never sees it, so what the phone
/// reports here -- the call was placed, it is still up, it ended -- is the only record the platform gets of it.
/// </summary>
public sealed partial class TelephonyHub
{
    /// <summary>
    /// Records a browser-originated outbound call in the caller's history, so it appears at the top of the Recent tab
    /// like any other outbound call.
    /// </summary>
    /// <param name="callId">The client call identifier.</param>
    /// <param name="to">The dialed destination (a number, or an extension's display name).</param>
    /// <param name="from">The presented caller id.</param>
    public async Task RecordBrowserCall(string callId, string to, string from)
    {
        if (string.IsNullOrEmpty(callId))
        {
            return;
        }

        await ShellScope.UsingChildScopeAsync(async scope =>
        {
            if (!await AuthorizeAsync(scope.ServiceProvider))
            {
                LogHubActionUnauthorized("RecordBrowserCall");
                return;
            }

            var call = new TelephonyCall
            {
                CallId = callId,
                To = to,
                From = from,
                Direction = CallDirection.Outbound,
                State = CallState.Connecting,
            };

            await RecordInteractionAsync(scope.ServiceProvider, "Dial", call, Context.ConnectionAborted);
        });
    }

    /// <summary>
    /// Marks a browser-originated call ended in the caller's history. Without this the interaction stays "in progress",
    /// which keeps it out of completed history until the reconciliation sweep settles it as unreported. The phone may
    /// send the same end again (it resends an end it could not confirm), and a call already settled is left as it is.
    /// </summary>
    /// <param name="callId">The client call identifier reported to <see cref="RecordBrowserCall"/>.</param>
    /// <param name="connected">Whether the call reached a connected state before it ended.</param>
    public async Task RecordBrowserCallEnded(string callId, bool connected)
    {
        if (string.IsNullOrEmpty(callId))
        {
            return;
        }

        await ShellScope.UsingChildScopeAsync(async scope =>
        {
            if (!await AuthorizeAsync(scope.ServiceProvider))
            {
                LogHubActionUnauthorized("RecordBrowserCallEnded");
                return;
            }

            var recorder = scope.ServiceProvider.GetService<ClientRecordedCallRecorder>();

            if (recorder is not null)
            {
                await recorder.SettleAsync(Context.UserIdentifier, callId, connected, Context.ConnectionAborted);
            }
        });
    }

    /// <summary>
    /// Records that the caller's soft phone still has these calls up. The phone reports its calls every so often while
    /// they last; a call it stops reporting (a page that crashed, an app closed mid-call) is settled by the
    /// reconciliation sweep rather than left "in progress" for good.
    /// </summary>
    /// <param name="callIds">The client call identifiers of the calls still up.</param>
    /// <param name="connectedCallIds">Those of them that have connected.</param>
    public async Task ReportBrowserCallsAlive(string[] callIds, string[] connectedCallIds)
    {
        if (callIds is null || callIds.Length == 0)
        {
            return;
        }

        await ShellScope.UsingChildScopeAsync(async scope =>
        {
            if (!await AuthorizeAsync(scope.ServiceProvider))
            {
                LogHubActionUnauthorized("ReportBrowserCallsAlive");
                return;
            }

            var recorder = scope.ServiceProvider.GetService<ClientRecordedCallRecorder>();

            if (recorder is not null)
            {
                await recorder.MarkAliveAsync(Context.UserIdentifier, callIds, connectedCallIds ?? [], Context.ConnectionAborted);
            }
        });
    }
}
