using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell.Removing;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Telephony.Services;

internal sealed class RecordingMediaTenantEvents : ModularTenantEvents
{
    private readonly IRecordingMediaStore _mediaStore;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    public RecordingMediaTenantEvents(
        IRecordingMediaStore mediaStore,
        IStringLocalizer<RecordingMediaTenantEvents> localizer,
        ILogger<RecordingMediaTenantEvents> logger)
    {
        _mediaStore = mediaStore;
        S = localizer;
        _logger = logger;
    }

    public override async Task RemovingAsync(ShellRemovingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (_mediaStore is not ISupportsTenantMediaPurge purgeableMediaStore)
        {
            _logger.LogError(
                "Tenant removal was blocked because the configured recording media store does not support tenant-wide media cleanup.");
            context.ErrorMessage = S["Tenant removal was blocked because the configured recording media store does not support tenant-wide media cleanup."];

            return;
        }

        try
        {
            if (await purgeableMediaStore.TryPurgeAllAsync(CancellationToken.None))
            {
                return;
            }

            _logger.LogError("Tenant removal was blocked because recording media cleanup did not complete.");
            context.ErrorMessage = S["Tenant removal was blocked because recording media cleanup did not complete."];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tenant removal was blocked because recording media cleanup failed.");
            context.ErrorMessage = S["Tenant removal was blocked because recording media cleanup failed."];
            context.Error = ex;
        }
    }
}
