namespace CrestApps.OrchardCore.OrTools;

/// <summary>
/// The constants of the OR-Tools module.
/// </summary>
public static class OrToolsConstants
{
    /// <summary>
    /// The name of the OR-Tools solvers, shown with their results.
    /// </summary>
    public const string SolverName = "OR-Tools";

    /// <summary>
    /// The feature identifiers.
    /// </summary>
    public static class Features
    {
        /// <summary>
        /// The CP-SAT shift schedule solver.
        /// </summary>
        public const string ShiftScheduling = "CrestApps.OrchardCore.OrTools.ShiftScheduling";

        /// <summary>
        /// The routing optimizer for dispatch.
        /// </summary>
        public const string Dispatch = "CrestApps.OrchardCore.OrTools.Dispatch";
    }
}
