namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Turns user ids into user names and back, so recipes carry people by user name.
/// </summary>
internal interface ISurveyUserLookup
{
    /// <summary>
    /// Gets the user names of users by id.
    /// </summary>
    /// <param name="userIds">The user ids.</param>
    /// <returns>The user names by user id; unknown users are left out.</returns>
    Task<IDictionary<string, string>> GetUserNamesAsync(IEnumerable<string> userIds);

    /// <summary>
    /// Finds the id of a user by user name.
    /// </summary>
    /// <param name="userName">The user name.</param>
    /// <returns>The user id, or <see langword="null"/>.</returns>
    Task<string> FindUserIdAsync(string userName);
}
