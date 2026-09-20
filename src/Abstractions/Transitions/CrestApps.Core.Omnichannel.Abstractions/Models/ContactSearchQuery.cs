namespace CrestApps.Core.Omnichannel.Models;

/// <summary>
/// Which contacts to return, and which page of them.
/// </summary>
/// <remarks>
/// Every criterion is optional, and one left unset does not narrow the result. A query with nothing
/// set therefore returns every contact, which is what a batch loader asks for.
/// </remarks>
public sealed class ContactSearchQuery
{
    /// <summary>
    /// Gets or sets the kinds of contact to include. Empty includes every kind.
    /// </summary>
    public IList<string> DefinitionNames { get; set; } = [];

    /// <summary>
    /// Gets or sets free text to match against the contact's name.
    /// </summary>
    public string Term { get; set; }

    /// <summary>
    /// Gets or sets a phone number to match, in E.164 form.
    /// </summary>
    public string PhoneNumber { get; set; }

    /// <summary>
    /// Gets or sets an email address to match.
    /// </summary>
    public string Email { get; set; }

    /// <summary>
    /// Gets or sets a value that, when set, keeps only contacts whose "do not call" preference matches.
    /// </summary>
    public bool? DoNotCall { get; set; }

    /// <summary>
    /// Gets or sets a value that, when set, keeps only contacts whose "do not SMS" preference matches.
    /// </summary>
    public bool? DoNotSms { get; set; }

    /// <summary>
    /// Gets or sets a value that, when set, keeps only contacts whose "do not email" preference matches.
    /// </summary>
    public bool? DoNotEmail { get; set; }

    /// <summary>
    /// Gets or sets the one-based page number.
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// Gets or sets how many contacts a page holds.
    /// </summary>
    public int PageSize { get; set; } = 50;
}
