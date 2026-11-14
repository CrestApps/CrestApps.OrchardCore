using Microsoft.Extensions.Options;
using OrchardCore.AuditTrail.Services.Models;

namespace CrestApps.OrchardCore.Disbursements.AuditTrail;

/// <summary>
/// The Audit Trail events of Disbursements.
/// </summary>
internal sealed class DisbursementsAuditTrailEvents : IConfigureOptions<AuditTrailOptions>
{
    public const string CategoryName = "Disbursements";
    public const string FileSent = "DisbursementBankFileSent";

    public void Configure(AuditTrailOptions options)
    {
        options.For<DisbursementsAuditTrailEvents>(CategoryName, S => S["Disbursements"])
            .WithEvent(FileSent, S => S["Bank file sent"], S => S["A bank file, which holds every payee's full account number, was downloaded or delivered by a data pipeline."], enableByDefault: true);
    }
}

/// <summary>
/// What the Audit Trail keeps of a bank file that left the site: never a number.
/// </summary>
public sealed class DisbursementFileAuditData
{
    /// <summary>
    /// Gets or sets the batch's id.
    /// </summary>
    public string BatchId { get; set; }

    /// <summary>
    /// Gets or sets the provider.
    /// </summary>
    public string Provider { get; set; }

    /// <summary>
    /// Gets or sets the file's name.
    /// </summary>
    public string FileName { get; set; }

    /// <summary>
    /// Gets or sets how many disbursements it holds.
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Gets or sets how it left: downloaded, or delivered by a pipeline.
    /// </summary>
    public string How { get; set; }
}
