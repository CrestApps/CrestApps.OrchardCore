using CrestApps.OrchardCore.Learning.Packages.Models;

namespace CrestApps.OrchardCore.Learning.Packages.Core.Scorm;

/// <summary>
/// Checks the status values of a runtime state that did not come through the data model, such as one a recipe imports:
/// they must be words of the package's standard, which keeps them within what the runtime state index stores.
/// </summary>
public static class PackageStateValues
{
    private static readonly string[] _lessonStatuses = ["passed", "completed", "failed", "incomplete", "browsed", "not attempted"];
    private static readonly string[] _completionStatuses = ["completed", "incomplete", "not attempted", "unknown"];
    private static readonly string[] _successStatuses = ["passed", "failed", "unknown"];

    /// <summary>
    /// Names the first value of a state that is not a word of its standard.
    /// </summary>
    /// <param name="state">The state.</param>
    /// <returns>The element, or <see langword="null"/> when every value is one of the standard's.</returns>
    public static string Problem(PackageRuntimeState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.Cmi is { } cmi && !IsOneOf(cmi.LessonStatus, _lessonStatuses))
        {
            return "cmi.core.lesson_status";
        }

        if (state.Cmi2004 is { } cmi2004)
        {
            if (!IsOneOf(cmi2004.CompletionStatus, _completionStatuses))
            {
                return "cmi.completion_status";
            }

            if (!IsOneOf(cmi2004.SuccessStatus, _successStatuses))
            {
                return "cmi.success_status";
            }
        }

        return null;
    }

    private static bool IsOneOf(string value, string[] words)
        => string.IsNullOrEmpty(value) || Array.IndexOf(words, value) >= 0;
}
