namespace CrestApps.Core.Locking;

/// <summary>
/// A held distributed lock. Disposing it releases the lock.
/// </summary>
public interface ILocker : IDisposable, IAsyncDisposable
{
}
