using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.ContactCenter.Drivers;

internal sealed class VoiceMediaItemDisplayDriver : DisplayDriver<VoiceMediaItem>
{
    // Telnyx Media Storage caps media at 20 MB; a greeting or hold-music clip sits well under this, so anything
    // larger is not voice media.
    private const long MaxAudioBytes = 20L * 1024 * 1024;

    // Labels the provider's copy by purpose so provider media storage stays identifiable.
    private const string MediaNamePrefix = "cc-voice-media";

    private static readonly string[] _allowedContentTypes =
    [
        "audio/mpeg",
        "audio/mp3",
        "audio/wav",
        "audio/x-wav",
        "audio/wave",
        "audio/webm",
        "audio/ogg",
    ];

    private readonly IVoiceMediaProvisioningService _provisioning;
    private readonly INotifier _notifier;
    private readonly IHtmlLocalizer H;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="VoiceMediaItemDisplayDriver"/> class.
    /// </summary>
    /// <param name="provisioning">The service that keeps the master copy and gives the provider its copy.</param>
    /// <param name="notifier">The notifier a warning about a kept but undelivered upload is shown through.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public VoiceMediaItemDisplayDriver(
        IVoiceMediaProvisioningService provisioning,
        INotifier notifier,
        IHtmlLocalizer<VoiceMediaItemDisplayDriver> htmlLocalizer,
        IStringLocalizer<VoiceMediaItemDisplayDriver> stringLocalizer)
    {
        _provisioning = provisioning;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override Task<IDisplayResult> DisplayAsync(VoiceMediaItem item, BuildDisplayContext context)
    {
        return CombineAsync(
            View("VoiceMediaItem_Fields_SummaryAdmin", item)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1"),
            View("VoiceMediaItem_Buttons_SummaryAdmin", item)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Actions:5"),
            View("VoiceMediaItem_DefaultMeta_SummaryAdmin", item)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
        );
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(VoiceMediaItem item, BuildEditorContext context)
    {
        return Initialize<VoiceMediaItemViewModel>("VoiceMediaItemFields_Edit", model =>
        {
            var providerNames = item.ProviderReferences?.Keys.ToList() ?? [];

            if (providerNames.Count == 0 && !string.IsNullOrWhiteSpace(item.ProviderName) && !string.IsNullOrWhiteSpace(item.MediaReference))
            {
                providerNames.Add(item.ProviderName);
            }

            model.Id = item.ItemId;
            model.Name = item.Name;
            model.Description = item.Description;
            model.HasMasterCopy = !string.IsNullOrWhiteSpace(item.MasterStorageReference);
            model.HasMedia = model.HasMasterCopy || providerNames.Count > 0;
            model.ProviderNames = providerNames;
            model.Format = item.Format;
            model.CanUpload = _provisioning.CanUpload;
        }).Location("Content:1%General;1");
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(VoiceMediaItem item, UpdateEditorContext context)
    {
        var model = new VoiceMediaItemViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        item.Name = model.Name?.Trim();
        item.Description = string.IsNullOrWhiteSpace(model.Description)
            ? null
            : model.Description.Trim();

        var audio = model.Audio;

        if (audio is not null && audio.Length > 0)
        {
            await UploadAudioAsync(item, audio, context);
        }

        return Edit(item, context);
    }

    private async Task UploadAudioAsync(VoiceMediaItem item, IFormFile audio, UpdateEditorContext context)
    {
        if (audio.Length > MaxAudioBytes)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(VoiceMediaItemViewModel.Audio), S["The audio is too large. It must be 20 MB or less."]);

            return;
        }

        if (!_allowedContentTypes.Contains(audio.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(VoiceMediaItemViewModel.Audio), S["Unsupported audio format."]);

            return;
        }

        if (!_provisioning.CanUpload)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(VoiceMediaItemViewModel.Audio), S["Audio uploads are unavailable because no media storage and no telephony provider capable of hosting media is configured."]);

            return;
        }

        VoiceMediaStoreResult result;

        await using (var stream = audio.OpenReadStream())
        {
            result = await _provisioning.StoreAsync(item, stream, audio.ContentType, VoiceMediaUploadFormat.Resolve(audio), MediaNamePrefix, CancellationToken.None);
        }

        if (!result.Succeeded)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(VoiceMediaItemViewModel.Audio), S["The audio could not be uploaded. Please try again."]);

            return;
        }

        // The single provider copy recorded before copies were tracked per provider follows the copy the active
        // provider was just given, so a reader of those members still finds a copy that plays.
        item.ProviderName = result.ProviderReference is null ? null : result.ProviderName;
        item.MediaReference = result.ProviderReference;

        if (result.ProviderUploadFailed)
        {
            await _notifier.WarningAsync(H["The audio was stored, but {0} did not accept a copy. The provider is given one when the clip is next played, or use Re-sync media.", result.ProviderName]);
        }
        else if (!result.MasterStored)
        {
            await _notifier.WarningAsync(H["The audio was sent to {0}, but no master copy could be kept. Keep the original file: the clip has to be uploaded again if the provider changes.", result.ProviderName]);
        }

        // The copies this upload replaced are deleted once the entry is saved, and the new copies are deleted
        // instead when it is not: the reconciliation runs after the request's unit of work, so neither a failed save
        // nor a refused form leaves the stored entry pointing at audio that is gone.
        ShellScope.AddDeferredTask(scope => ReconcileReplacedAudioAsync(scope, item.ItemId, VoiceMediaAssetCopies.Snapshot(item), result.Replaced));
    }

    private static async Task ReconcileReplacedAudioAsync(ShellScope scope, string itemId, VoiceMediaAsset uploaded, IVoiceMediaAsset replaced)
    {
        var provisioning = scope.ServiceProvider.GetRequiredService<IVoiceMediaProvisioningService>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<VoiceMediaItemDisplayDriver>>();

        try
        {
            var store = scope.ServiceProvider.GetRequiredService<IVoiceMediaItemStore>();
            var stored = string.IsNullOrEmpty(itemId) ? null : await store.FindByIdAsync(itemId);

            if (VoiceMediaAssetCopies.HoldsSameAudio(stored, uploaded))
            {
                if (replaced is not null)
                {
                    await provisioning.DeleteEverywhereAsync(replaced);
                }

                return;
            }

            // The entry was not saved with the new audio, so the copies made for it would never be played.
            await provisioning.DeleteEverywhereAsync(uploaded);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete the voice media audio replaced by an upload to the entry {ItemId}.", itemId.SanitizeLogValue());
        }
    }
}
