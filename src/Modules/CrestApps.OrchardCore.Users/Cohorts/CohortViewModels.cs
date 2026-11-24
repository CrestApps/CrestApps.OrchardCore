using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Users.Cohorts;

/// <summary>
/// A row of the cohorts list.
/// </summary>
public class CohortRow
{
    /// <summary>
    /// Gets or sets the cohort id.
    /// </summary>
    public string CohortId { get; set; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets how many people are in it.
    /// </summary>
    public int MemberCount { get; set; }
}

/// <summary>
/// The cohorts list.
/// </summary>
public class CohortListViewModel
{
    /// <summary>
    /// Gets or sets the search text.
    /// </summary>
    public string Search { get; set; }

    /// <summary>
    /// Gets or sets the rows.
    /// </summary>
    public AdminListContext<CohortRow> List { get; set; }
}

/// <summary>
/// The editor of a cohort.
/// </summary>
public class CohortEditViewModel
{
    /// <summary>
    /// Gets or sets the cohort id; empty for a new cohort.
    /// </summary>
    public string CohortId { get; set; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; }
}

/// <summary>
/// A person in a cohort.
/// </summary>
public class CohortMemberRow
{
    /// <summary>
    /// Gets or sets the cohort id.
    /// </summary>
    public string CohortId { get; set; }

    /// <summary>
    /// Gets or sets the user id.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the user name, or <see langword="null"/> when the user is gone.
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

    /// <summary>
    /// Gets or sets when the person was added, in UTC; shown in the site's time zone.
    /// </summary>
    public DateTime AddedUtc { get; set; }
}

/// <summary>
/// The people in a cohort, and the form that adds more.
/// </summary>
public class CohortMembersViewModel
{
    /// <summary>
    /// Gets or sets the cohort id.
    /// </summary>
    public string CohortId { get; set; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets how many people are in it.
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Gets or sets the rows.
    /// </summary>
    public AdminListContext<CohortMemberRow> List { get; set; }
}

/// <summary>
/// The people to add to a cohort.
/// </summary>
public class CohortAddMembersViewModel
{
    /// <summary>
    /// Gets or sets the user ids chosen in the user picker.
    /// </summary>
    public string[] UserIds { get; set; } = [];

    /// <summary>
    /// Gets or sets user names or e-mail addresses, one per line, to add many people at once.
    /// </summary>
    public string People { get; set; }
}
