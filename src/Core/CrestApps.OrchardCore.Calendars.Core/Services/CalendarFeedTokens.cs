using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Core.Security;
using Microsoft.AspNetCore.DataProtection;

namespace CrestApps.OrchardCore.Calendars.Core.Services;

/// <summary>
/// Issues, shows and checks a calendar's private feed secret. The calendar keeps only the secret's hash, which the feed is
/// found by, and the secret protected with the tenant's data protection keys, so its address can be shown again; anyone
/// who reads the database or a backup can open no feed. A secret given before it was hashed
/// (<see cref="Calendar.FeedToken"/>) keeps working.
/// </summary>
public static class CalendarFeedTokens
{
    private const string ProtectionPurpose = "CrestApps.Calendars.FeedToken";

    /// <summary>
    /// Gives the calendar a new feed secret, replacing the old one, and returns it.
    /// </summary>
    /// <param name="calendar">The calendar.</param>
    /// <param name="dataProtectionProvider">The tenant's data protection provider.</param>
    public static string Issue(Calendar calendar, IDataProtectionProvider dataProtectionProvider)
    {
        ArgumentNullException.ThrowIfNull(calendar);
        ArgumentNullException.ThrowIfNull(dataProtectionProvider);

        var token = SecretTokens.Create(24);

        calendar.FeedToken = null;
        calendar.FeedTokenHash = SecretTokens.Hash(token);
        calendar.ProtectedFeedToken = dataProtectionProvider.CreateProtector(ProtectionPurpose).Protect(token);

        return token;
    }

    /// <summary>
    /// Removes the calendar's feed secret, so its address stops working.
    /// </summary>
    /// <param name="calendar">The calendar.</param>
    public static void Remove(Calendar calendar)
    {
        ArgumentNullException.ThrowIfNull(calendar);

        calendar.FeedToken = null;
        calendar.FeedTokenHash = null;
        calendar.ProtectedFeedToken = null;
    }

    /// <summary>
    /// Returns the calendar's feed secret, to show its address, or <see langword="null"/> when it has none.
    /// </summary>
    /// <param name="calendar">The calendar.</param>
    /// <param name="dataProtectionProvider">The tenant's data protection provider.</param>
    public static string Read(Calendar calendar, IDataProtectionProvider dataProtectionProvider)
    {
        ArgumentNullException.ThrowIfNull(calendar);
        ArgumentNullException.ThrowIfNull(dataProtectionProvider);

        if (string.IsNullOrEmpty(calendar.ProtectedFeedToken))
        {
            return string.IsNullOrEmpty(calendar.FeedToken) ? null : calendar.FeedToken;
        }

        try
        {
            return dataProtectionProvider.CreateProtector(ProtectionPurpose).Unprotect(calendar.ProtectedFeedToken);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return null;
        }
    }

    /// <summary>
    /// Returns the hash the calendar's feed is found by: the stored hash, or the hash of a secret given before it was
    /// hashed.
    /// </summary>
    /// <param name="calendar">The calendar.</param>
    public static string GetHash(Calendar calendar)
        => calendar?.FeedTokenHash ?? SecretTokens.Hash(calendar?.FeedToken);

    /// <summary>
    /// Fills in the hash and the protected secret of a calendar whose secret was given before it was hashed. The plain
    /// secret is kept for one release, so the previous release still opens the feed if it is rolled back.
    /// </summary>
    /// <param name="calendar">The calendar.</param>
    /// <param name="dataProtectionProvider">The tenant's data protection provider.</param>
    /// <returns><see langword="true"/> when the calendar changed.</returns>
    public static bool Backfill(Calendar calendar, IDataProtectionProvider dataProtectionProvider)
    {
        ArgumentNullException.ThrowIfNull(calendar);
        ArgumentNullException.ThrowIfNull(dataProtectionProvider);

        if (string.IsNullOrEmpty(calendar.FeedToken) || !string.IsNullOrEmpty(calendar.FeedTokenHash))
        {
            return false;
        }

        calendar.FeedTokenHash = SecretTokens.Hash(calendar.FeedToken);
        calendar.ProtectedFeedToken = dataProtectionProvider.CreateProtector(ProtectionPurpose).Protect(calendar.FeedToken);

        return true;
    }
}
