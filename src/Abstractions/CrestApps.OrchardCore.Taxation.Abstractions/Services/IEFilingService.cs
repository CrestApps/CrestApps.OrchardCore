using System.Security.Claims;
using System.Text.Json.Serialization;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Taxation.Models;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Taxation.Services;

/// <summary>
/// Describes a kind of document that can be filed electronically and is told when the agency accepted one. Tax Returns
/// adds the one for tax returns; another module (such as payroll filings) adds its own. Register one with
/// <c>services.AddEFileTargetProvider&lt;T&gt;()</c>.
/// </summary>
public interface IEFileTargetProvider
{
    /// <summary>
    /// Gets the kind of document it describes, such as <c>TaxReturn</c>.
    /// </summary>
    string TargetType { get; }

    /// <summary>
    /// Finds a document, or <see langword="null"/>.
    /// </summary>
    /// <param name="id">The document's id.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The document as a target.</returns>
    Task<EFileTarget> FindAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets whether a user may file a document electronically, such as one holding the permission to file tax returns.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="target">The document.</param>
    /// <returns><see langword="true"/> when the user may.</returns>
    Task<bool> CanFileAsync(ClaimsPrincipal user, EFileTarget target);

    /// <summary>
    /// Called once the agency accepted a submission of the document, so it is marked as filed.
    /// </summary>
    /// <param name="target">The document.</param>
    /// <param name="submission">The accepted submission.</param>
    /// <param name="filedOn">The day it counts as filed.</param>
    /// <param name="user">Who recorded the acceptance, or <see langword="null"/> for the system.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The errors, or none.</returns>
    Task<IReadOnlyList<string>> AcceptedAsync(EFileTarget target, EFileSubmission submission, DateOnly filedOn, ClaimsPrincipal user, CancellationToken cancellationToken = default);
}

/// <summary>
/// Writes a document into a file an agency or a channel takes, such as a state's XML schema. Tax Returns has a JSON
/// format of any return; a pack adds a jurisdiction's own. Register one with <c>services.AddEFileFormat&lt;T&gt;()</c>.
/// </summary>
public interface IEFileFormat
{
    /// <summary>
    /// Gets the stored name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets what people see.
    /// </summary>
    [JsonIgnore]
    LocalizationSource DisplayName { get; }

    /// <summary>
    /// Gets whether the format writes a document.
    /// </summary>
    /// <param name="target">The document.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    bool CanWrite(EFileTarget target);

    /// <summary>
    /// Writes a document.
    /// </summary>
    /// <param name="target">The document.</param>
    /// <param name="submission">The submission being made, not saved yet: its <see cref="EFileSubmission.Options"/> hold
    /// the choices made when it was submitted, and <see cref="EFileSubmission.IsTest"/> whether it is a test.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The file, with the problems that keep it from being sent.</returns>
    Task<EFilePayload> WriteAsync(EFileTarget target, EFileSubmission submission, CancellationToken cancellationToken = default);
}

/// <summary>
/// A filing channel: how a file reaches the agency, such as a state's gateway, or by hand. Tax Returns has a manual
/// channel that hands over the file to file on the agency's site; a pack adds a jurisdiction's own. Register one with
/// <c>services.AddEFileTransport&lt;T&gt;()</c>.
/// </summary>
public interface IEFileTransport
{
    /// <summary>
    /// Gets the stored name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets what people see.
    /// </summary>
    [JsonIgnore]
    LocalizationSource DisplayName { get; }

    /// <summary>
    /// Gets a value indicating whether the file is filed by a person: the channel hands it over, and the person records
    /// the agency's answer. A manual channel is never asked for an acknowledgement.
    /// </summary>
    bool IsManual { get; }

    /// <summary>
    /// Gets a value indicating whether the channel takes test submissions: it sends a submission marked
    /// <see cref="EFileSubmission.IsTest"/> to the agency's test system, whose acceptance files nothing.
    /// </summary>
    bool SupportsTestSubmissions { get; }

    /// <summary>
    /// Gets whether the channel sends a document in a format, such as a gateway that takes only its state's schema.
    /// </summary>
    /// <param name="target">The document.</param>
    /// <param name="format">The format's name.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    bool CanSend(EFileTarget target, string format);

    /// <summary>
    /// Sends a submission's file.
    /// </summary>
    /// <param name="submission">The submission, with its file, its <see cref="EFileSubmission.Options"/> and whether it is
    /// a test.</param>
    /// <param name="payload">The file.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>What happened: submitted, accepted at once, rejected or failed.</returns>
    Task<EFileTransportResult> SendAsync(EFileSubmission submission, EFilePayload payload, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asks the agency for its answer to a submitted file.
    /// </summary>
    /// <param name="submission">The submission.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>What happened; <see cref="EFileSubmissionStatus.Submitted"/> while the agency has not answered.</returns>
    Task<EFileTransportResult> CheckAsync(EFileSubmission submission, CancellationToken cancellationToken = default);
}

/// <summary>
/// Stores e-file submissions.
/// </summary>
public interface IEFileSubmissionManager : ICatalogManager<EFileSubmission>
{
    /// <summary>
    /// Lists the submissions of a document, newest first.
    /// </summary>
    /// <param name="targetType">The kind of document.</param>
    /// <param name="targetId">The document's id.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The submissions.</returns>
    Task<IReadOnlyList<EFileSubmission>> ListAsync(string targetType, string targetId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the submissions waiting for the agency's answer.
    /// </summary>
    /// <param name="count">The most to list.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The submissions, oldest checked first.</returns>
    Task<IReadOnlyList<EFileSubmission>> ListPendingAsync(int count, CancellationToken cancellationToken = default);
}

/// <summary>
/// Files documents electronically: builds the file in a format, sends it through a channel, and records the agency's
/// answer, marking the document filed once it is accepted.
/// </summary>
public interface IEFilingService
{
    /// <summary>
    /// Finds a document through its target provider, or <see langword="null"/>.
    /// </summary>
    /// <param name="targetType">The kind of document.</param>
    /// <param name="targetId">The document's id.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The target.</returns>
    Task<EFileTarget> FindTargetAsync(string targetType, string targetId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the formats that write a document, each with the channels that send it.
    /// </summary>
    /// <param name="target">The document.</param>
    /// <returns>The pairs of format and channel offered.</returns>
    IReadOnlyList<(IEFileFormat Format, IEFileTransport Transport)> GetOptions(EFileTarget target);

    /// <summary>
    /// Gets a channel by name, or <see langword="null"/>.
    /// </summary>
    /// <param name="name">The channel's name.</param>
    /// <returns>The channel.</returns>
    IEFileTransport GetTransport(string name);

    /// <summary>
    /// Builds a document's file in a format and sends it through a channel, with the choices and the test flag of the
    /// request kept on the submission.
    /// </summary>
    /// <param name="request">The document, the format, the channel, whether it is a test, and the choices.</param>
    /// <param name="user">Who submits it.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The submission, or the errors.</returns>
    Task<EFilingResult> SubmitAsync(EFileSubmissionRequest request, ClaimsPrincipal user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asks a submission's channel for the agency's answer and records it.
    /// </summary>
    /// <param name="submission">The submission.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The result.</returns>
    Task<EFilingResult> CheckAsync(EFileSubmission submission, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records the agency's answer to a submission filed by hand.
    /// </summary>
    /// <param name="submission">The submission.</param>
    /// <param name="accepted">Whether the agency accepted the document.</param>
    /// <param name="confirmationNumber">The agency's confirmation number of an accepted document.</param>
    /// <param name="filedOn">The day the document was filed.</param>
    /// <param name="message">The agency's message, such as the reason of a rejection, or <see langword="null"/>.</param>
    /// <param name="user">Who records it.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The result.</returns>
    Task<EFilingResult> RecordAnswerAsync(EFileSubmission submission, bool accepted, string confirmationNumber, DateOnly filedOn, string message, ClaimsPrincipal user, CancellationToken cancellationToken = default);
}

/// <summary>
/// Shortcuts for <see cref="IEFilingService"/>.
/// </summary>
public static class EFilingServiceExtensions
{
    /// <summary>
    /// Builds a document's file in a format and sends it through a channel, with no choices and not as a test.
    /// </summary>
    /// <param name="eFiling">The e-filing service.</param>
    /// <param name="target">The document.</param>
    /// <param name="format">The format's name.</param>
    /// <param name="transport">The channel's name.</param>
    /// <param name="user">Who submits it.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The submission, or the errors.</returns>
    public static Task<EFilingResult> SubmitAsync(this IEFilingService eFiling, EFileTarget target, string format, string transport, ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eFiling);

        return eFiling.SubmitAsync(new EFileSubmissionRequest
        {
            Target = target,
            Format = format,
            Transport = transport,
        }, user, cancellationToken);
    }
}

/// <summary>
/// The result of an e-filing action.
/// </summary>
public sealed class EFilingResult
{
    /// <summary>
    /// Gets or sets the submission.
    /// </summary>
    public EFileSubmission Submission { get; set; }

    /// <summary>
    /// Gets the errors.
    /// </summary>
    public IList<string> Errors { get; } = [];

    /// <summary>
    /// Gets a value indicating whether it succeeded.
    /// </summary>
    public bool Succeeded
        => Errors.Count == 0;
}
