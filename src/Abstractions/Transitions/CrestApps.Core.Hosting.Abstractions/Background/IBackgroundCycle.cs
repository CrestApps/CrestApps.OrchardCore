namespace CrestApps.Core.Hosting.Background;

/// <summary>
/// One pass of recurring work.
/// </summary>
/// <remarks>
/// <para>
/// A cycle is the work itself, with no opinion about what makes it run: a host with a scheduler of
/// its own drives it from that, and a host without one uses the runner this framework ships. That
/// separation is what lets the same sweep run under a CMS, a plain web host, and a unit test.
/// </para>
/// <para>
/// A cycle does one pass and returns. It is resolved from a fresh scope each time, so it may hold
/// scoped services, and it must honour the cancellation token so a shutdown does not wait for it.
/// </para>
/// </remarks>
public interface IBackgroundCycle
{
    /// <summary>
    /// Runs one pass.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task RunAsync(CancellationToken cancellationToken = default);
}
