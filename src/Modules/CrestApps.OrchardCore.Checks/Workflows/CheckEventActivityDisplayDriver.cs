using OrchardCore.Workflows.Display;

namespace CrestApps.OrchardCore.Checks.Workflows;

/// <summary>
/// Displays a Checks workflow event in the workflow editor.
/// </summary>
/// <typeparam name="TActivity">The event.</typeparam>
public sealed class CheckEventActivityDisplayDriver<TActivity> : ActivityDisplayDriver<TActivity>
    where TActivity : CheckEventActivity
{
}
