using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using CrestApps.OrchardCore.Telephony;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.ContactCenter.Drivers;

internal sealed class VoiceMediaItemDisplayDriver : DisplayDriver<VoiceMediaItem>
{
    // Telnyx Media Storage caps media at 20 MB; a greeting or hold-music clip sits well under this, so anything
    // larger is not voice media.
    private const long MaxAudioBytes = 20L * 1024 * 1024;

    // Labels the stored clip by purpose so provider media storage stays identifiable.
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

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="VoiceMediaItemDisplayDriver"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider used to resolve the optional media provisioner.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public VoiceMediaItemDisplayDriver(
        IServiceProvider serviceProvider,
        ILogger<VoiceMediaItemDisplayDriver> logger,
        IStringLocalizer<VoiceMediaItemDisplayDriver> stringLocalizer)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
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
        var canUpload = _serviceProvider.GetService<IVoiceMediaProvisioner>() is not null;

        return Initialize<VoiceMediaItemViewModel>("VoiceMediaItemFields_Edit", model =>
        {
            model.Id = item.ItemId;
            model.Name = item.Name;
            model.Description = item.Description;
            model.HasMedia = !string.IsNullOrWhiteSpace(item.MediaReference);
            model.ProviderName = item.ProviderName;
            model.Format = item.Format;
            model.CanUpload = canUpload;
        }).Location("Content:1");
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

    private async Task UploadAudioAsync(VoiceMediaItem item, Microsoft.AspNetCore.Http.IFormFile audio, UpdateEditorContext context)
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

        var provisioner = _serviceProvider.GetService<IVoiceMediaProvisioner>();

        if (provisioner is null)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(VoiceMediaItemViewModel.Audio), S["Audio uploads are not supported by the configured telephony provider."]);

            return;
        }

        string mediaReference;

        await using (var stream = audio.OpenReadStream())
        {
            mediaReference = await provisioner.UploadAsync(stream, audio.ContentType, MediaNamePrefix, CancellationToken.None);
        }

        if (string.IsNullOrWhiteSpace(mediaReference))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(VoiceMediaItemViewModel.Audio), S["The audio could not be uploaded. Please try again."]);

            return;
        }

        // Swap in the new clip, then schedule a best-effort delete of the previous provider media so it does not
        // linger. The deletion runs after the entry is persisted so a failed save never orphans the new upload.
        var previousReference = item.MediaReference;
        var previousProvider = item.ProviderName;

        item.MediaReference = mediaReference;
        item.ProviderName = provisioner.ProviderTechnicalName;
        item.Format = ResolveFormat(audio);

        if (!string.IsNullOrWhiteSpace(previousReference) &&
            !string.Equals(previousReference, mediaReference, StringComparison.Ordinal) &&
            string.Equals(previousProvider, provisioner.ProviderTechnicalName, StringComparison.Ordinal))
        {
            ShellScope.AddDeferredTask(scope => DeletePreviousMediaAsync(scope, previousReference));
        }
    }

    private static string ResolveFormat(Microsoft.AspNetCore.Http.IFormFile audio)
    {
        var extension = Path.GetExtension(audio.FileName);

        if (!string.IsNullOrWhiteSpace(extension))
        {
            return extension.TrimStart('.').ToLowerInvariant();
        }

        var contentType = audio.ContentType;

        if (!string.IsNullOrWhiteSpace(contentType))
        {
            var slashIndex = contentType.IndexOf('/');

            if (slashIndex >= 0 && slashIndex < contentType.Length - 1)
            {
                return contentType[(slashIndex + 1)..].Trim().ToLowerInvariant();
            }
        }

        return null;
    }

    private static async Task DeletePreviousMediaAsync(ShellScope scope, string mediaReference)
    {
        var provisioner = scope.ServiceProvider.GetService<IVoiceMediaProvisioner>();

        if (provisioner is null)
        {
            return;
        }

        var logger = scope.ServiceProvider.GetRequiredService<ILogger<VoiceMediaItemDisplayDriver>>();

        try
        {
            await provisioner.DeleteAsync(mediaReference);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete the replaced voice media {MediaReference}.", mediaReference.SanitizeLogValue());
        }
    }
}
