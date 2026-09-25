using CrestApps.Core.Support;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telephony.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.Telephony.Hubs;

/// <summary>
/// Names the people behind extensions: the directory the soft phone reads to show "Jane Doe" beside extension 2, and
/// the name a colleague's phone shows when this agent rings their extension.
/// </summary>
public sealed partial class TelephonyHub
{
    /// <summary>
    /// Gets every extension of the phone system with the name of the person it rings.
    /// </summary>
    /// <returns>The extensions, or why they could not be read.</returns>
    public async Task<TelephonyExtensionDirectoryResult> GetExtensionDirectory()
    {
        var result = new TelephonyExtensionDirectoryResult
        {
            Succeeded = false,
            Error = S["Unable to load the extensions."].Value,
        };
        LogHubActionStart("GetExtensionDirectory");

        await ShellScope.UsingChildScopeAsync(async scope =>
        {
            if (!await AuthorizeAsync(scope.ServiceProvider))
            {
                LogHubActionUnauthorized("GetExtensionDirectory");
                result.Error = S["You are not authorized to use the soft phone."].Value;

                return;
            }

            var directory = scope.ServiceProvider.GetService<ITelephonyExtensionDirectory>();

            result = new TelephonyExtensionDirectoryResult
            {
                Succeeded = true,
                Entries = directory is null ? [] : await directory.ListAsync(Context.ConnectionAborted),
            };
        });

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Telephony hub action {Action} completed for user {UserId}. Succeeded={Succeeded}, Returned={ReturnedCount}, Error={Error}.",
                "GetExtensionDirectory",
                RedactedUserId(),
                result.Succeeded,
                result.Entries.Count,
                result.Succeeded ? null : result.Error.SanitizeLogValue());
        }

        return result;
    }

    // Names this agent to the colleague whose extension they ring as the site names its users. Runs before the dial;
    // it never refuses one, so a name that cannot be read leaves the sign-in name the hub already set.
    private async Task<TelephonyResult> StampCallerDisplayNameAsync(IServiceProvider services, ExtensionDialRequest request, CancellationToken cancellationToken)
    {
        var userId = Context.UserIdentifier;
        var directory = services.GetService<ITelephonyExtensionDirectory>();

        if (request is null || string.IsNullOrEmpty(userId) || directory is null)
        {
            return null;
        }

        try
        {
            var name = await directory.GetUserNameAsync(userId, cancellationToken);

            if (!string.IsNullOrWhiteSpace(name))
            {
                request.CallerDisplayName = name;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "The caller's display name could not be read for an extension call; the sign-in name is shown instead.");
        }

        return null;
    }
}
