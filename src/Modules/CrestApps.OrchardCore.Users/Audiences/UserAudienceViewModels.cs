using Microsoft.AspNetCore.Mvc.ModelBinding;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Users.Audiences;

/// <summary>
/// A row of the audiences list.
/// </summary>
public class UserAudienceRow
{
    /// <summary>
    /// Gets or sets the technical name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the name shown.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets whether an administrator defined it, rather than a module.
    /// </summary>
    public bool IsDefined { get; set; }

    /// <summary>
    /// Gets or sets how many rules it has.
    /// </summary>
    public int RuleCount { get; set; }

    /// <summary>
    /// Gets or sets whether a person must match every rule.
    /// </summary>
    public bool MatchAll { get; set; }
}

/// <summary>
/// The audiences list.
/// </summary>
public class UserAudienceListViewModel
{
    /// <summary>
    /// Gets or sets the search text.
    /// </summary>
    public string Search { get; set; }

    /// <summary>
    /// Gets or sets the rows.
    /// </summary>
    public AdminListContext<UserAudienceRow> List { get; set; }
}

/// <summary>
/// The editor of an audience.
/// </summary>
public class UserAudienceEditViewModel
{
    /// <summary>
    /// Gets or sets whether the audience exists already; its name then stays.
    /// </summary>
    public bool IsNew { get; set; }

    /// <summary>
    /// Gets or sets the technical name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the name shown.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets how the rules combine.
    /// </summary>
    public UserAudienceJoin Join { get; set; }

    /// <summary>
    /// Gets or sets the rules.
    /// </summary>
    public List<UserAudienceRuleEditViewModel> Rules { get; set; } = [];

    /// <summary>
    /// Gets or sets the kinds of rules to choose from.
    /// </summary>
    public IReadOnlyList<UserAudienceSourceViewModel> Sources { get; set; } = [];

    /// <summary>
    /// Gets or sets the editor shape, built by the display drivers of the view model: one card per section.
    /// </summary>
    [BindNever]
    public IShape Editor { get; set; }
}

/// <summary>
/// A rule in the editor.
/// </summary>
public class UserAudienceRuleEditViewModel
{
    /// <summary>
    /// Gets or sets the rule source.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets the values chosen.
    /// </summary>
    public List<string> Values { get; set; } = [];

    /// <summary>
    /// Gets or sets the number.
    /// </summary>
    public int? Number { get; set; }
}

/// <summary>
/// A kind of rule to choose from.
/// </summary>
public class UserAudienceSourceViewModel
{
    /// <summary>
    /// Gets or sets the technical name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the name shown.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets what the number means, or <see langword="null"/>.
    /// </summary>
    public string NumberLabel { get; set; }

    /// <summary>
    /// Gets or sets the values to choose from.
    /// </summary>
    public IReadOnlyList<UserAudienceRuleOption> Options { get; set; } = [];
}

/// <summary>
/// A person in an audience.
/// </summary>
public class UserAudienceMemberRow
{
    /// <summary>
    /// Gets or sets the user id.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the user name.
    /// </summary>
    public string UserName { get; set; }

    /// <summary>
    /// Gets or sets the name shown.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the e-mail.
    /// </summary>
    public string Email { get; set; }
}

/// <summary>
/// The people in an audience.
/// </summary>
public class UserAudienceMembersViewModel
{
    /// <summary>
    /// Gets or sets the technical name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the name shown.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets how many people are in it.
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Gets or sets whether an administrator defined it.
    /// </summary>
    public bool IsDefined { get; set; }

    /// <summary>
    /// Gets or sets the rows.
    /// </summary>
    public AdminListContext<UserAudienceMemberRow> List { get; set; }
}
