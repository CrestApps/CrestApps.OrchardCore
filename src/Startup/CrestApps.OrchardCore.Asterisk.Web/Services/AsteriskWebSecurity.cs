using Microsoft.Extensions.Hosting;

namespace CrestApps.OrchardCore.Asterisk.Web.Services;

internal static class AsteriskWebSecurity
{
    public static void EnsureDevelopmentOnly(string environmentName)
    {
        if (!string.Equals(environmentName, Environments.Development, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The Asterisk Web sample is development-only and cannot start outside the Development environment.");
        }
    }
}
