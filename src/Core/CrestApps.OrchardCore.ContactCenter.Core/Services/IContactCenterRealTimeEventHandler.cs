namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Marks a <see cref="IContactCenterEventHandler"/> whose work is a push to a screen someone is watching: the agent bar,
/// the agent workspace or the soft phone. These handlers run as soon as the change that raised the event commits, for
/// every event of that change, before any other handler runs for any of them.
/// </summary>
/// <remarks>
/// <para>
/// Every other handler runs one after another for each event, each in its own scope and commit, and some of them do
/// real work (pacing the dialer, triggering workflows, recording metrics). Run among them, a push waited for all of that,
/// and for every handler of the events published before it in the same change: an agent connected to a call saw its
/// record seconds after hearing the customer.
/// </para>
/// <para>
/// A handler marked this way must be cheap, must only read the state the change committed (or write its own
/// projection of it), and must be <see cref="ContactCenterHandlerReplaySafety.NaturallyIdempotent"/>: when it fails
/// here it runs again with the event's other handlers, and a redelivered event runs it again.
/// </para>
/// </remarks>
public interface IContactCenterRealTimeEventHandler : IContactCenterEventHandler
{
}
