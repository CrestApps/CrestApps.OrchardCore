using System.Diagnostics.Metrics;
using CrestApps.OrchardCore.Diagnostics;

namespace CrestApps.OrchardCore.MailingLists.Core.Services;

/// <summary>
/// The meter of mailing lists, <c>CrestApps.OrchardCore.MailingLists</c>: sign-ups, verifications, unsubscribes and
/// provider sync failures. Addresses, member ids and list ids never go on a metric.
/// </summary>
public static class MailingListsTelemetry
{
    /// <summary>
    /// The tag that records what a sign-up did.
    /// </summary>
    public const string OutcomeTag = CrestAppsOrchardCoreTelemetry.OutcomeTag;

    /// <summary>
    /// The tag that records the name of a list's provider.
    /// </summary>
    public const string ProviderTag = CrestAppsOrchardCoreTelemetry.ProviderTag;

    /// <summary>
    /// The tag that records how a change was made (see <c>MailingListMemberSources</c>).
    /// </summary>
    public const string SourceTag = "source";

    /// <summary>
    /// The meter of mailing lists.
    /// </summary>
    public static Meter Meter { get; } = CrestAppsOrchardCoreTelemetry.CreateMeter("MailingLists");

    /// <summary>
    /// Gets the sign-ups, by what they did.
    /// </summary>
    public static Counter<long> SignUps { get; } = Meter.CreateCounter<long>(
        "crestapps.mailing_lists.sign_ups",
        "{sign_up}",
        "The sign-ups to mailing lists, by what they did.");

    /// <summary>
    /// Gets the verified addresses.
    /// </summary>
    public static Counter<long> Verifications { get; } = Meter.CreateCounter<long>(
        "crestapps.mailing_lists.verifications",
        "{verification}",
        "The addresses verified by a confirmation or claim link, an account or the provider.");

    /// <summary>
    /// Gets the unsubscribed memberships, by how they were unsubscribed.
    /// </summary>
    public static Counter<long> Unsubscribes { get; } = Meter.CreateCounter<long>(
        "crestapps.mailing_lists.unsubscribes",
        "{membership}",
        "The memberships unsubscribed or cleaned, by how.");

    /// <summary>
    /// Gets the member changes a provider did not take.
    /// </summary>
    public static Counter<long> SyncFailures { get; } = Meter.CreateCounter<long>(
        "crestapps.mailing_lists.sync_failures",
        "{change}",
        "The member changes a provider did not take, by provider and whether they will be tried again.");
}
