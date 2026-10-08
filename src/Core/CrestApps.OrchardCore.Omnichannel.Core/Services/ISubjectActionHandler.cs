using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Runs a subject action type contributed by a feature, next to the built-in Finish, Try Again and New Activity
/// types. The action type must also be registered through <see cref="SubjectActionOptions"/> so it can be chosen.
/// </summary>
public interface ISubjectActionHandler
{
    /// <summary>
    /// Gets the action type this handler runs.
    /// </summary>
    string ActionType { get; }

    /// <summary>
    /// Gets the position of this action type among the actions of one disposition. Actions with a lower order run
    /// first; the built-in types run at 0. An action that changes which record the activity is about, such as
    /// converting a lead into a contact, runs first so the actions after it work on the new record.
    /// </summary>
    int Order => 0;

    /// <summary>
    /// Runs the action.
    /// </summary>
    /// <param name="action">The action being run.</param>
    /// <param name="context">The completion that triggered the action. A handler that changes the record the
    /// activity is about updates <see cref="SubjectActionExecutionContext.Contact"/>.</param>
    Task ExecuteAsync(SubjectAction action, SubjectActionExecutionContext context);
}
