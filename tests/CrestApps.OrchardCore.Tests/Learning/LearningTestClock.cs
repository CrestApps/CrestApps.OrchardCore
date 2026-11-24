using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Learning;

/// <summary>
/// A clock a Learning test moves.
/// </summary>
internal sealed class LearningTestClock : IClock
{
    public DateTime UtcNow { get; set; } = LearningTestHarness.Now;

    public ITimeZone[] GetTimeZones() => [];

    public ITimeZone GetTimeZone(string timeZoneId) => null;

    public ITimeZone GetSystemTimeZone() => null;

    public DateTimeOffset ConvertToTimeZone(DateTimeOffset dateTimeOffset, ITimeZone timeZone) => dateTimeOffset;

    public void Use(IServiceCollection services)
    {
        services.RemoveAll<IClock>();
        services.AddSingleton<IClock>(this);
    }
}
