namespace CrestApps.OrchardCore.Users;

/// <summary>
/// An audience an administrator defines: a named set of rules, joined by all or any, that decide who is in it, such as
/// "every staff member of the Field team hired more than 30 days ago".
/// </summary>
public sealed class UserAudienceDefinition
{
    /// <summary>
    /// Gets or sets the technical name, unique among audiences.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the name shown to people.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets a description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets whether a person must match every rule, or any one.
    /// </summary>
    public UserAudienceJoin Join { get; set; }

    /// <summary>
    /// Gets or sets the rules.
    /// </summary>
    public List<UserAudienceRule> Rules { get; set; } = [];
}

/// <summary>
/// How the rules of an audience combine.
/// </summary>
public enum UserAudienceJoin
{
    /// <summary>
    /// A person must match every rule.
    /// </summary>
    All,

    /// <summary>
    /// A person must match any one rule.
    /// </summary>
    Any,
}

/// <summary>
/// One rule of an audience: a rule source with the values it is given.
/// </summary>
public sealed class UserAudienceRule
{
    /// <summary>
    /// Gets or sets the rule source, such as <c>Role</c> or <c>Team</c>.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets the values chosen, such as role names or team ids; a person matches any of them.
    /// </summary>
    public List<string> Values { get; set; } = [];

    /// <summary>
    /// Gets or sets a number the source asks for, such as days since hire.
    /// </summary>
    public int? Number { get; set; }
}
