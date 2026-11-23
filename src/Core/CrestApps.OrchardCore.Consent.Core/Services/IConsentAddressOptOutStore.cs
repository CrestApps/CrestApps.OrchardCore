using CrestApps.OrchardCore.Consent.Core.Models;

namespace CrestApps.OrchardCore.Consent.Core.Services;

/// <summary>
/// Keeps the <see cref="ConsentAddressOptOut"/> records: opt-outs for one purpose recorded against an address. Every
/// method takes the address as it was given and normalizes it for the channel (<see cref="ConsentAddresses"/>).
/// </summary>
public interface IConsentAddressOptOutStore
{
    /// <summary>
    /// Returns whether an opt-out of <paramref name="purpose"/> is recorded for the address on the channel.
    /// </summary>
    /// <param name="channel">The channel, one of <see cref="ConsentChannels"/>.</param>
    /// <param name="address">The address.</param>
    /// <param name="purpose">The purpose, such as <see cref="ConsentPurposes.Marketing"/>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<bool> ExistsAsync(string channel, string address, string purpose, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the opt-outs recorded for the address on the channel, of every purpose.
    /// </summary>
    /// <param name="channel">The channel, one of <see cref="ConsentChannels"/>.</param>
    /// <param name="address">The address.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<ConsentAddressOptOut>> ListAsync(string channel, string address, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves an opt-out: adds it, or, when one of the same channel, address and purpose is already recorded, updates
    /// that one's source and time. Returns the record that was saved.
    /// </summary>
    /// <param name="optOut">The opt-out. Its channel, address and purpose are required.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The saved record, or <see langword="null"/> when the opt-out has no channel, address or purpose.</returns>
    Task<ConsentAddressOptOut> SaveAsync(ConsentAddressOptOut optOut, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes every opt-out of <paramref name="purpose"/> recorded for the address on the channel.
    /// </summary>
    /// <param name="channel">The channel, one of <see cref="ConsentChannels"/>.</param>
    /// <param name="address">The address.</param>
    /// <param name="purpose">The purpose.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of records deleted.</returns>
    Task<int> DeleteAsync(string channel, string address, string purpose, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns one page of every opt-out, in the order they were stored, for an export.
    /// </summary>
    /// <param name="afterId">The document id of the last record of the previous page, or <c>0</c> for the first page.</param>
    /// <param name="take">The most records to return.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The page, with the document id to pass for the next page (<c>0</c> when the page is the last one).</returns>
    Task<(IReadOnlyList<ConsentAddressOptOut> Records, long NextId)> PageAsync(long afterId, int take, CancellationToken cancellationToken = default);
}
