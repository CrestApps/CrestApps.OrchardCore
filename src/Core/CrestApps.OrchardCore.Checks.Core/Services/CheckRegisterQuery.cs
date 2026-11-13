using CrestApps.OrchardCore.Checks.Models;
using CrestApps.OrchardCore.SourceAccess;

namespace CrestApps.OrchardCore.Checks.Core.Services;

/// <summary>
/// What a page of the check register shows.
/// </summary>
public sealed class CheckRegisterQuery
{
    /// <summary>
    /// Gets or sets the bank account, or <see langword="null"/> for every account.
    /// </summary>
    public string BankAccountId { get; set; }

    /// <summary>
    /// Gets or sets the status, or <see langword="null"/> for every status.
    /// </summary>
    public CheckStatus? Status { get; set; }

    /// <summary>
    /// Gets or sets a check number, or text the payee's name contains.
    /// </summary>
    public string Search { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the oldest checks come first; the newest do by default.
    /// </summary>
    public bool OldestFirst { get; set; }

    /// <summary>
    /// Gets or sets the checks the reader may see.
    /// </summary>
    public SourceAccessFilter Access { get; set; } = SourceAccessFilter.Unrestricted;
}
