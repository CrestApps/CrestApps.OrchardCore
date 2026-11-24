using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.ContactCenter.Drivers;

internal sealed class VoiceMediaItemDisplayDriver : DisplayDriver<VoiceMediaItem>
{
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
        // The row's classes and attributes are rendered on its row element, in both admin list layouts.
        if (context.DisplayType is OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid
            && context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = item.Name?.ToLowerInvariant() ?? string.Empty;
        }

        return CombineAsync(
            View("VoiceMediaItem_Fields_SummaryAdmin", item)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("VoiceMediaItem_Description_SummaryAdmin", item)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:5")
                .RenderWhen(() => Task.FromResult(!string.IsNullOrWhiteSpace(item.Description))),
            View("VoiceMediaItem_DefaultTags_SummaryAdmin", item)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Audio:5"),
            View("VoiceMediaItem_Buttons_SummaryAdmin", item).Location("Actions:5"),
            View("VoiceMediaItem_DefaultMeta_SummaryAdmin", item)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Modified:5")
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
        VoiceMediaAudioUploadResult result;

        await using (var stream = audio.OpenReadStream())
        {
            result = await VoiceMediaAudioUpload.UploadAsync(_provisioning, item, stream, audio.Length, audio.ContentType, VoiceMediaUploadFormat.Resolve(audio), CancellationToken.None);
        }

        switch (result.Status)
        {
            case VoiceMediaAudioUploadStatus.TooLarge:
                context.Updater.ModelState.AddModelError(Prefix, nameof(VoiceMediaItemViewModel.Audio), S["The audio is too large. It must be 20 MB or less."]);

                return;
            case VoiceMediaAudioUploadStatus.UnsupportedFormat:
                context.Updater.ModelState.AddModelError(Prefix, nameof(VoiceMediaItemViewModel.Audio), S["Unsupported audio format."]);

                return;
            case VoiceMediaAudioUploadStatus.Unavailable:
                context.Updater.ModelState.AddModelError(Prefix, nameof(VoiceMediaItemViewModel.Audio), S["Audio uploads are unavailable because no media storage and no telephony provider capable of hosting media is configured."]);

                return;
            case VoiceMediaAudioUploadStatus.Failed:
                context.Updater.ModelState.AddModelError(Prefix, nameof(VoiceMediaItemViewModel.Audio), S["The audio could not be uploaded. Please try again."]);

                return;
        }

        if (result.Store.ProviderUploadFailed)
        {
            await _notifier.WarningAsync(H["The audio was stored, but {0} did not accept a copy. The provider is given one when the clip is next played, or use Re-sync media.", result.Store.ProviderName]);
        }
        else if (!result.Store.MasterStored)
        {
            await _notifier.WarningAsync(H["The audio was sent to {0}, but no master copy could be kept. Keep the original file: the clip has to be uploaded again if the provider changes.", result.Store.ProviderName]);
        }
    }
}
