using CrestApps.OrchardCore.Telephony.Services;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Tests.Telephony.Doubles;

/// <summary>
/// A telephony user accessor that returns a preset user and records persistence calls.
/// </summary>
internal sealed class FakeTelephonyUserAccessor : ITelephonyUserAccessor
{
    private readonly IUser _user;

    public FakeTelephonyUserAccessor(IUser user)
    {
        _user = user;
    }

    public int UpdateCount { get; private set; }

    public Task<IUser> GetCurrentUserAsync() => Task.FromResult(_user);

    public Task UpdateUserAsync(IUser user)
    {
        UpdateCount++;

        return Task.CompletedTask;
    }
}
