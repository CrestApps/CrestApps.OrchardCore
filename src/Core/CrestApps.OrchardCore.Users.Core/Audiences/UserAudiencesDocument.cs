using OrchardCore.Data.Documents;

namespace CrestApps.OrchardCore.Users.Core.Audiences;

/// <summary>
/// The audiences administrators define.
/// </summary>
public sealed class UserAudiencesDocument : Document
{
    /// <summary>
    /// Gets or sets the audiences; names are unique, compared without case.
    /// </summary>
    public List<UserAudienceDefinition> Audiences { get; set; } = [];

    /// <summary>
    /// Finds an audience by name, without case.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <returns>The audience, or <see langword="null"/>.</returns>
    public UserAudienceDefinition Find(string name)
        => Audiences.FirstOrDefault(audience => string.Equals(audience.Name, name, StringComparison.OrdinalIgnoreCase));
}
