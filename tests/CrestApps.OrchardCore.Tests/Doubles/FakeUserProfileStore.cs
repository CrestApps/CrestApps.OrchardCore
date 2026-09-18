using CrestApps.Core.Security;

namespace CrestApps.OrchardCore.Tests.Doubles;

/// <summary>
/// An in-memory <see cref="IUserProfileStore"/> for one signed-in user.
/// </summary>
/// <remarks>
/// Keeps each stored shape as its own instance, so a test can assert that a change was persisted
/// rather than only applied to the copy the caller happened to hold.
/// </remarks>
internal sealed class FakeUserProfileStore : IUserProfileStore
{
    private readonly Dictionary<Type, object> _values = [];

    /// <summary>
    /// Gets or sets whether a current user exists. When false, an update fails as it would in a host
    /// with nobody signed in.
    /// </summary>
    public bool HasCurrentUser { get; set; } = true;

    /// <summary>
    /// Gets how many times a change was actually committed.
    /// </summary>
    public int PersistCount { get; private set; }

    /// <summary>
    /// Gets how many times the cached user was discarded.
    /// </summary>
    public int ReloadCount { get; private set; }

    public Task<T> FindAsync<T>(CancellationToken cancellationToken = default)
        where T : class, new()
        => Task.FromResult(_values.TryGetValue(typeof(T), out var value) ? (T)value : null);

    public Task UpdateAsync<T>(Func<T, bool> mutate, CancellationToken cancellationToken = default)
        where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(mutate);

        if (!HasCurrentUser)
        {
            throw new UserPersistenceException("There is no current user whose profile could be updated.");
        }

        var value = _values.TryGetValue(typeof(T), out var stored) ? (T)stored : new T();

        if (!mutate(value))
        {
            return Task.CompletedTask;
        }

        _values[typeof(T)] = value;
        PersistCount++;

        return Task.CompletedTask;
    }

    public Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        ReloadCount++;

        return Task.CompletedTask;
    }
}
