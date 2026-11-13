namespace CrestApps.OrchardCore.Disbursements.Core.Nacha;

/// <summary>
/// What a business's bank needs on the bank files (Nacha) sent from one of the business's own accounts: kept on the
/// <c>BankAccount</c> in its properties and edited on the account page.
/// </summary>
public sealed class NachaOriginSettings
{
    /// <summary>
    /// Gets or sets a value indicating whether bank files are sent from the account.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the routing number of the bank that receives the file; empty for the account's own bank.
    /// </summary>
    public string ImmediateDestination { get; set; }

    /// <summary>
    /// Gets or sets the name of the bank that receives the file.
    /// </summary>
    public string ImmediateDestinationName { get; set; }

    /// <summary>
    /// Gets or sets the origin the bank gave the business, ten characters at most, often its tax id with a leading 1.
    /// </summary>
    public string ImmediateOrigin { get; set; }

    /// <summary>
    /// Gets or sets the origin's name.
    /// </summary>
    public string ImmediateOriginName { get; set; }

    /// <summary>
    /// Gets or sets the company name the payee's bank statement shows, sixteen characters at most.
    /// </summary>
    public string CompanyName { get; set; }

    /// <summary>
    /// Gets or sets the company identification the bank gave the business, ten characters at most.
    /// </summary>
    public string CompanyIdentification { get; set; }

    /// <summary>
    /// Gets or sets the description of the entries, ten characters at most, such as PAYROLL.
    /// </summary>
    public string CompanyEntryDescription { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether each batch ends with a debit of the business's account for its total
    /// (an offset entry), which some banks ask for.
    /// </summary>
    public bool IncludeOffset { get; set; }
}
