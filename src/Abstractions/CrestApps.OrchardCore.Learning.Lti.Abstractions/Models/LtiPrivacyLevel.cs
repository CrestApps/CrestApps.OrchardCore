namespace CrestApps.OrchardCore.Learning.Lti.Models;

/// <summary>
/// What a tool learns about a person. The subject (<c>sub</c>) is always an id made for the tool, never the user id.
/// </summary>
public enum LtiPrivacyLevel
{
    /// <summary>
    /// Only the id made for the tool.
    /// </summary>
    Anonymous = 0,

    /// <summary>
    /// The id and the person's name.
    /// </summary>
    Name = 1,

    /// <summary>
    /// The id, the name and the e-mail address.
    /// </summary>
    NameAndEmail = 2,
}
