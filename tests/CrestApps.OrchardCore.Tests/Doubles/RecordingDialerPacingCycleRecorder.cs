using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;

namespace CrestApps.OrchardCore.Tests.Doubles;

/// <summary>
/// Keeps every pacing cycle record it is handed, so a test can read how each cycle ended.
/// </summary>
internal sealed class RecordingDialerPacingCycleRecorder : IDialerPacingCycleRecorder
{
    private readonly Lock _gate = new();
    private readonly List<DialerPacingCycle> _cycles = [];

    public IReadOnlyList<DialerPacingCycle> Cycles
    {
        get
        {
            lock (_gate)
            {
                return [.. _cycles];
            }
        }
    }

    public int ImmediateCount { get; private set; }

    public Task RecordAsync(DialerPacingCycle cycle)
    {
        lock (_gate)
        {
            _cycles.Add(cycle);
        }

        return Task.CompletedTask;
    }

    public Task RecordNowAsync(DialerPacingCycle cycle)
    {
        lock (_gate)
        {
            ImmediateCount++;
            _cycles.Add(cycle);
        }

        return Task.CompletedTask;
    }
}
