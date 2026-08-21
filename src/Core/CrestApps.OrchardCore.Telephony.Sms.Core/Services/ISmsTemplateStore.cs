using CrestApps.Core.Services;
using CrestApps.OrchardCore.Telephony.Sms.Core.Models;

namespace CrestApps.OrchardCore.Telephony.Sms.Core.Services;

/// <summary>
/// The persistence contract for <see cref="SmsTemplate"/>.
/// </summary>
public interface ISmsTemplateStore : ICatalog<SmsTemplate>
{
    /// <summary>
    /// Lists every enabled template, ordered by name.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The enabled templates.</returns>
    Task<IReadOnlyCollection<SmsTemplate>> GetEnabledAsync(CancellationToken cancellationToken = default);
}
