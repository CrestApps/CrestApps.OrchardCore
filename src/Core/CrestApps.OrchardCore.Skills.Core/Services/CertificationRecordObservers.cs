using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Skills.Core.Services;

/// <summary>
/// Calls the certification record observers one by one, logging a failure instead of passing it on.
/// </summary>
internal static class CertificationRecordObservers
{
    public static async Task NotifyAsync(
        IEnumerable<ICertificationRecordObserver> observers,
        Func<ICertificationRecordObserver, Task> notify,
        CertificationRecord record,
        ILogger logger)
    {
        foreach (var observer in observers)
        {
            try
            {
                await notify(observer);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "The certification record observer '{Observer}' failed for the certificate '{RecordId}'.", observer.GetType().Name, record.ItemId);
            }
        }
    }
}
