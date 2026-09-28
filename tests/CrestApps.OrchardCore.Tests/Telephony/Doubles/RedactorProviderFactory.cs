using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.Compliance.Redaction;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Telephony.Doubles;

/// <summary>
/// Builds the redactor provider the telephony services resolve, configured the way the modules configure it.
/// </summary>
internal static class RedactorProviderFactory
{
    /// <summary>
    /// Creates a redactor provider that erases values classified as an address, matching the registration the
    /// telephony modules make so a test observes the same redaction behavior as production.
    /// </summary>
    /// <returns>A redactor provider for use in tests.</returns>
    public static IRedactorProvider Create()
    {
        return new ServiceCollection()
            .AddRedaction(builder => builder.SetRedactor<ErasingRedactor>(LogDataClassifications.AddressSet))
            .BuildServiceProvider()
            .GetRequiredService<IRedactorProvider>();
    }
}
