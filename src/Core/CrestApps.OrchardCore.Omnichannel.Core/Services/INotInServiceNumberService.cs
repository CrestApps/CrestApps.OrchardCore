using CrestApps.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Keeps the list of phone numbers known not to be in service, and answers whether a number may still be dialed.
/// </summary>
/// <remarks>
/// One list read by every path that decides what to dial: loading contacts into a campaign, the dialer's pre-dial
/// check and the automated caller. Numbers are compared in E.164 form, so a number stored with formatting still
/// matches the mark made from the provider's plain E.164 destination.
/// </remarks>
public interface INotInServiceNumberService
{
    /// <summary>
    /// Whether the number is marked as not in service.
    /// </summary>
    /// <param name="phoneNumber">The number, in any form the phone number service can read.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<bool> IsNotInServiceAsync(string phoneNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Which of these numbers are marked as not in service.
    /// </summary>
    /// <param name="phoneNumbers">The numbers, in any form the phone number service can read.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The numbers from <paramref name="phoneNumbers"/> that are marked, exactly as they were passed in.</returns>
    Task<IReadOnlySet<string>> GetNotInServiceAsync(IEnumerable<string> phoneNumbers, CancellationToken cancellationToken = default);

    /// <summary>
    /// The mark on a number, or <see langword="null"/> when it is not marked.
    /// </summary>
    /// <param name="phoneNumber">The number, in any form the phone number service can read.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<NotInServiceNumber> FindAsync(string phoneNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a number as not in service, or records another detection on a number that already is.
    /// </summary>
    /// <param name="mark">What was found, and by what.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The mark, or <see langword="null"/> when the number could not be read.</returns>
    Task<NotInServiceNumber> MarkAsync(NotInServiceMark mark, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the mark from a number, so it is dialed again.
    /// </summary>
    /// <param name="phoneNumber">The number, in any form the phone number service can read.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when a mark was removed.</returns>
    Task<bool> ClearAsync(string phoneNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// A page of marked numbers, most recently detected first.
    /// </summary>
    /// <param name="page">The one-based page number.</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="search">Digits the number must contain, or <see langword="null"/> for every number.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<PageResult<NotInServiceNumber>> PageAsync(int page, int pageSize, string search, CancellationToken cancellationToken = default);

    /// <summary>
    /// The number in the form the list stores and compares, or <see langword="null"/> when it cannot be read.
    /// </summary>
    /// <param name="phoneNumber">The number, in any form the phone number service can read.</param>
    string Normalize(string phoneNumber);
}
