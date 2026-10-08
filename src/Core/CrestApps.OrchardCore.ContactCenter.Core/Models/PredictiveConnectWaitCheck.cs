namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// The result of <see cref="PredictiveConnectWait.Check"/>.
/// </summary>
/// <param name="Verdict">Whether the wait is allowed.</param>
/// <param name="MaxAllowedMilliseconds">The longest wait the measurement allows, 0 without a measurement.</param>
/// <param name="P95ConnectLatency">The measured 95th percentile connect time, when there is one.</param>
public sealed record PredictiveConnectWaitCheck(PredictiveConnectWaitVerdict Verdict, int MaxAllowedMilliseconds, TimeSpan? P95ConnectLatency);
