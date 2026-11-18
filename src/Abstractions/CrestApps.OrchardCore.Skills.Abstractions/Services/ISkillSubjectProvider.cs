using System.Security.Claims;

namespace CrestApps.OrchardCore.Skills.Services;

/// <summary>
/// Describes the people of one subject kind (see <see cref="Models.SkillSubjectKind"/>) to Skills: their names, and whether a
/// signed-in person may change their skills and certificates. Skills provides the one for users; the module that owns
/// another kind, such as Staff, adds its own, so Skills never needs that module's types.
/// </summary>
public interface ISkillSubjectProvider
{
    /// <summary>
    /// Gets the subject kind this provider describes, such as <see cref="Models.SkillSubjectKind.User"/>.
    /// </summary>
    string Kind { get; }

    /// <summary>
    /// Gets the names of the subjects that exist among the given ids.
    /// </summary>
    /// <param name="ids">The subject ids.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The names by id; an id that names nobody is left out.</returns>
    Task<IReadOnlyDictionary<string, string>> GetNamesAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets whether a signed-in person may change the skills and certificates of a subject, beyond the Skills permission
    /// they already hold, such as a manager who may only change the staff they manage.
    /// </summary>
    /// <param name="user">The signed-in person.</param>
    /// <param name="id">The subject id.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="true"/> when allowed.</returns>
    Task<bool> CanManageAsync(ClaimsPrincipal user, string id, CancellationToken cancellationToken = default);
}
