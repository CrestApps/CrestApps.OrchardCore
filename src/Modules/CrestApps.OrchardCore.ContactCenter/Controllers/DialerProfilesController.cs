using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Admin;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Controllers;

/// <summary>
/// Provides administration of dialer profiles.
/// </summary>
[Admin]
[Feature(ContactCenterConstants.Feature.Dialer)]
public sealed class DialerProfilesController : Controller
{
    private readonly IDialerProfileManager _manager;
    private readonly IAuthorizationService _authorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialerProfilesController"/> class.
    /// </summary>
    /// <param name="manager">The dialer profile manager.</param>
    /// <param name="authorizationService">The authorization service.</param>
    public DialerProfilesController(
        IDialerProfileManager manager,
        IAuthorizationService authorizationService)
    {
        _manager = manager;
        _authorizationService = authorizationService;
    }

    /// <summary>
    /// Lists the dialer profiles.
    /// </summary>
    /// <returns>The list view.</returns>
    [Admin("contact-center/dialers", "ContactCenterDialersIndex")]
    public async Task<IActionResult> Index()
    {
        if (!await _authorizationService.AuthorizeAsync(User, ContactCenterPermissions.ManageDialer))
        {
            return Forbid();
        }

        var profiles = await _manager.GetAllAsync();

        return View(profiles);
    }

    /// <summary>
    /// Displays the dialer profile create form.
    /// </summary>
    /// <returns>The create view.</returns>
    [Admin("contact-center/dialers/create", "ContactCenterDialersCreate")]
    public async Task<IActionResult> Create()
    {
        if (!await _authorizationService.AuthorizeAsync(User, ContactCenterPermissions.ManageDialer))
        {
            return Forbid();
        }

        return View(new DialerProfileViewModel());
    }

    /// <summary>
    /// Persists a new dialer profile.
    /// </summary>
    /// <param name="model">The submitted profile.</param>
    /// <returns>A redirect to the list or the form when invalid.</returns>
    [HttpPost]
    [ActionName(nameof(Create))]
    public async Task<IActionResult> CreatePost(DialerProfileViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ContactCenterPermissions.ManageDialer))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var profile = await _manager.NewAsync();
        Apply(profile, model);
        await _manager.CreateAsync(profile);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Displays the dialer profile edit form.
    /// </summary>
    /// <param name="id">The profile identifier.</param>
    /// <returns>The edit view.</returns>
    [Admin("contact-center/dialers/edit/{id}", "ContactCenterDialersEdit")]
    public async Task<IActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ContactCenterPermissions.ManageDialer))
        {
            return Forbid();
        }

        var profile = await _manager.FindByIdAsync(id);

        if (profile is null)
        {
            return NotFound();
        }

        return View(new DialerProfileViewModel
        {
            Id = profile.ItemId,
            Name = profile.Name,
            CampaignId = profile.CampaignId,
            QueueId = profile.QueueId,
            Mode = profile.Mode,
            ProviderName = profile.ProviderName,
            CallsPerAgent = profile.CallsPerAgent,
            MaxAttempts = profile.MaxAttempts,
            CallerId = profile.CallerId,
            Enabled = profile.Enabled,
        });
    }

    /// <summary>
    /// Persists changes to a dialer profile.
    /// </summary>
    /// <param name="model">The submitted profile.</param>
    /// <returns>A redirect to the list or the form when invalid.</returns>
    [HttpPost]
    [ActionName(nameof(Edit))]
    public async Task<IActionResult> EditPost(DialerProfileViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ContactCenterPermissions.ManageDialer))
        {
            return Forbid();
        }

        var profile = await _manager.FindByIdAsync(model.Id);

        if (profile is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        Apply(profile, model);
        await _manager.UpdateAsync(profile);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Deletes a dialer profile.
    /// </summary>
    /// <param name="id">The profile identifier.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ContactCenterPermissions.ManageDialer))
        {
            return Forbid();
        }

        var profile = await _manager.FindByIdAsync(id);

        if (profile is not null)
        {
            await _manager.DeleteAsync(profile);
        }

        return RedirectToAction(nameof(Index));
    }

    private static void Apply(Core.Models.DialerProfile profile, DialerProfileViewModel model)
    {
        profile.Name = model.Name;
        profile.CampaignId = model.CampaignId;
        profile.QueueId = model.QueueId;
        profile.Mode = model.Mode;
        profile.ProviderName = model.ProviderName;
        profile.CallsPerAgent = model.CallsPerAgent;
        profile.MaxAttempts = model.MaxAttempts;
        profile.CallerId = model.CallerId;
        profile.Enabled = model.Enabled;
    }
}
