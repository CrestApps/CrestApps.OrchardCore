using CrestApps.Core.Hosting.Background;

namespace CrestApps.OrchardCore.Asterisk.Services;

/// <summary>
/// Periodically ingests completed conversation recordings from Asterisk into the encrypted media store.
/// </summary>
/// <remarks>
/// The work itself, with no opinion about what makes it run. A host with a scheduler of its
/// own drives this from that; a host without one uses the framework runner.
/// </remarks>
internal interface IAsteriskRecordingIngestCycle : IBackgroundCycle;
