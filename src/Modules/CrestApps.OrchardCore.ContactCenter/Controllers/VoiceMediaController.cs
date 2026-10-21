using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Navigation;
using OrchardCore.Routing;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.ContactCenter.Controllers;

/// <summary>
/// Provides administration of the reusable voice media library (hold music, greetings, and IVR prompts).
/// </summary>
[Admin]
public sealed class VoiceMediaController : ContactCenterCatalogController<VoiceMediaItem>
{
    private readonly IVoiceMediaItemManager _manager;
    private readonly IVoiceMediaProvisioningService _voiceMediaProvisioning;
    private readonly IAuthorizationService _authorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="VoiceMediaController"/> class.
    /// </summary>
    /// <param name="manager">The voice media item manager.</param>
    /// <param name="voiceMediaProvisioning">The service that keeps the master copy and gives each provider its copy.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="updateModelAccessor">The update model accessor.</param>
    /// <param name="displayManager">The display manager.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public VoiceMediaController(
        IVoiceMediaItemManager manager,
        IVoiceMediaProvisioningService voiceMediaProvisioning,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<VoiceMediaItem> displayManager,
        INotifier notifier,
        IHtmlLocalizer<VoiceMediaController> htmlLocalizer,
        IStringLocalizer<VoiceMediaController> stringLocalizer)
        : base(manager, authorizationService, updateModelAccessor, displayManager, notifier, htmlLocalizer, stringLocalizer)
    {
        _manager = manager;
        _voiceMediaProvisioning = voiceMediaProvisioning;
        _authorizationService = authorizationService;
    }

    /// <inheritdoc/>
    protected override Permission ManagePermission
        => ContactCenterPermissions.ManageVoiceMedia;

    /// <inheritdoc/>
    protected override LocalizedString CreateDisplayName
        => S["Voice Media"];

    /// <inheritdoc/>
    protected override LocalizedString NewDisplayName
        => S["New Voice Media"];

    /// <inheritdoc/>
    protected override LocalizedHtmlString CreatedNotification
        => H["A new voice media clip has been created successfully."];

    /// <inheritdoc/>
    protected override LocalizedHtmlString UpdatedNotification
        => H["The voice media clip has been updated successfully."];

    /// <inheritdoc/>
    protected override LocalizedHtmlString DeletedNotification
        => H["The voice media clip has been deleted successfully."];

    /// <inheritdoc/>
    protected override void SetName(VoiceMediaItem entry, string name)
        => entry.Name = name;

    /// <summary>
    /// Lists the voice media clips.
    /// </summary>
    /// <param name="options">The catalog entry options.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The voice media list view.</returns>
    [Admin("contact-center/voice-media", "ContactCenterVoiceMediaIndex")]
    public Task<IActionResult> Index(
        CatalogEntryOptions options,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
        => IndexAsync(options, pagerParameters, pagerOptions, shapeFactory);

    /// <summary>
    /// Applies the voice media list filter.
    /// </summary>
    /// <param name="model">The submitted list model.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <returns>A redirect to the filtered list.</returns>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("contact-center/voice-media", "ContactCenterVoiceMediaIndex")]
    public Task<IActionResult> IndexFilterPost(ListCatalogEntryViewModel model, PagerParameters pagerParameters)
        => IndexFilterPostAsync(model, pagerParameters);

    /// <summary>
    /// Displays the voice media create form.
    /// </summary>
    /// <returns>The create view.</returns>
    [Admin("contact-center/voice-media/create", "ContactCenterVoiceMediaCreate")]
    public Task<IActionResult> Create()
        => CreateAsync();

    /// <summary>
    /// Persists a new voice media clip.
    /// </summary>
    /// <returns>A redirect to the list or the form when invalid.</returns>
    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("contact-center/voice-media/create", "ContactCenterVoiceMediaCreate")]
    public Task<IActionResult> CreatePost()
        => CreatePostAsync();

    /// <summary>
    /// Displays the voice media edit form.
    /// </summary>
    /// <param name="id">The voice media clip identifier.</param>
    /// <returns>The edit view.</returns>
    [Admin("contact-center/voice-media/edit/{id}", "ContactCenterVoiceMediaEdit")]
    public Task<IActionResult> Edit(string id)
        => EditAsync(id);

    /// <summary>
    /// Persists changes to a voice media clip.
    /// </summary>
    /// <param name="id">The voice media clip identifier.</param>
    /// <returns>A redirect to the list or the form when invalid.</returns>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("contact-center/voice-media/edit/{id}", "ContactCenterVoiceMediaEdit")]
    public Task<IActionResult> EditPost(string id)
        => EditPostAsync(id);

    /// <summary>
    /// Deletes a voice media clip.
    /// </summary>
    /// <param name="id">The voice media clip identifier.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    [Admin("contact-center/voice-media/delete/{id}", "ContactCenterVoiceMediaDelete")]
    public Task<IActionResult> Delete(string id)
        => DeleteAsync(id);

    /// <summary>
    /// Checks that every enabled telephony provider that hosts media still holds its copy of a clip, and sends the
    /// clip from its master copy to any that does not.
    /// </summary>
    /// <param name="id">The voice media clip identifier.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Admin("contact-center/voice-media/resync/{id}", "ContactCenterVoiceMediaResync")]
    public async Task<IActionResult> Resync(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ManagePermission))
        {
            return Forbid();
        }

        var item = await _manager.FindByIdAsync(id);

        if (item is null)
        {
            return NotFound();
        }

        var result = await _voiceMediaProvisioning.ResyncAsync(item, HttpContext.RequestAborted);

        if (!result.HasMasterCopy)
        {
            await Notifier.WarningAsync(H["'{0}' has no master copy, so it cannot be re-synced. Upload its audio again to protect it.", item.Name]);
        }
        else if (result.Providers.Count == 0)
        {
            await Notifier.WarningAsync(H["No enabled telephony provider can host media, so there was nothing to re-sync."]);
        }

        foreach (var (providerName, outcome) in result.Providers)
        {
            switch (outcome)
            {
                case VoiceMediaResyncOutcome.Verified:
                    await Notifier.InformationAsync(H["{0} already holds '{1}'.", providerName, item.Name]);
                    break;
                case VoiceMediaResyncOutcome.Uploaded:
                    await Notifier.SuccessAsync(H["'{1}' was sent to {0}.", providerName, item.Name]);
                    break;
                default:
                    await Notifier.ErrorAsync(H["{0} did not accept '{1}'. Check the provider's configuration and try again.", providerName, item.Name]);
                    break;
            }
        }

        return RedirectToAction(nameof(Index));
    }
}
