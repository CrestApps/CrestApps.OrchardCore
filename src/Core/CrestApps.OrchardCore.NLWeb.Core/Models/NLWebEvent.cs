namespace CrestApps.OrchardCore.NLWeb.Models;

/// <summary>
/// One step of an NLWeb response. The ask pipeline produces a stream of events, and each transport writes them in its
/// own form: one JSON object, server-sent events, or an MCP tool result.
/// </summary>
public abstract class NLWebEvent
{
}
