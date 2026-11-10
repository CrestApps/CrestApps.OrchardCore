using CrestApps.OrchardCore.AgenticCommerce.Core;
using CrestApps.OrchardCore.AgenticCommerce.Core.Models;
using CrestApps.OrchardCore.AgenticCommerce.Core.Services;
using CrestApps.OrchardCore.Core.Controllers;
using CrestApps.OrchardCore.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Routing;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AgenticCommerce.Controllers;

/// <summary>
/// Commerce › AI agents: the platforms whose agents may buy from the store, their API keys and webhook secrets.
/// </summary>
[Admin]
[Feature(AgenticCommerceConstants.Features.AgenticCommerce)]
public sealed class AgentPlatformsController : CatalogAdminController<AgentPlatform>
{
    private readonly IDataProtector _protector;
    private readonly IClock _clock;

    public AgentPlatformsController(
        IAgentPlatformManager manager,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<AgentPlatform> displayManager,
        INotifier notifier,
        IDataProtectionProvider dataProtectionProvider,
        IClock clock,
        IHtmlLocalizer<AgentPlatformsController> htmlLocalizer,
        IStringLocalizer<AgentPlatformsController> stringLocalizer)
        : base(manager, authorizationService, updateModelAccessor, displayManager, notifier, htmlLocalizer, stringLocalizer)
    {
        _protector = dataProtectionProvider.CreateProtector(AgenticCommerceStorage.SecretProtectionPurpose);
        _clock = clock;
    }

    protected override Permission ManagePermission
        => AgenticCommercePermissions.ManageAgentPlatforms;

    protected override LocalizedString CreateDisplayName
        => S["AI agent platform"];

    protected override LocalizedString NewDisplayName
        => S["New AI agent platform"];

    protected override LocalizedHtmlString CreatedNotification
        => H["The platform has been added. Give it an API key, or link its OAuth client, so its agents can sign in."];

    protected override LocalizedHtmlString UpdatedNotification
        => H["The platform has been updated."];

    protected override LocalizedHtmlString DeletedNotification
        => H["The platform has been removed. Its agents can't buy any more."];

    [Admin("commerce/ai-agents", "AgentPlatformsIndex")]
    public Task<IActionResult> Index(
        CatalogEntryOptions options,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
        => IndexAsync(options, pagerParameters, pagerOptions, shapeFactory);

    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("commerce/ai-agents", "AgentPlatformsIndex")]
    public Task<IActionResult> IndexFilterPost(ListCatalogEntryViewModel model, PagerParameters pagerParameters)
        => IndexFilterPostAsync(model, pagerParameters);

    [Admin("commerce/ai-agents/create", "AgentPlatformsCreate")]
    public Task<IActionResult> Create()
        => CreateAsync();

    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("commerce/ai-agents/create", "AgentPlatformsCreate")]
    public Task<IActionResult> CreatePost()
        => CreatePostAsync();

    [Admin("commerce/ai-agents/edit/{id}", "AgentPlatformsEdit")]
    public Task<IActionResult> Edit(string id)
        => EditAsync(id);

    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("commerce/ai-agents/edit/{id}", "AgentPlatformsEdit")]
    public Task<IActionResult> EditPost(string id)
        => EditPostAsync(id);

    [HttpPost]
    [Admin("commerce/ai-agents/delete/{id}", "AgentPlatformsDelete")]
    public Task<IActionResult> Delete(string id)
        => DeleteAsync(id);

    /// <summary>
    /// Gives the platform a new API key, which replaces the old one, and shows it once.
    /// </summary>
    /// <param name="id">The platform's id.</param>
    [HttpPost]
    [Admin("commerce/ai-agents/api-key/{id}", "AgentPlatformsApiKey")]
    public async Task<IActionResult> ApiKey(string id)
    {
        if (!await AuthorizationService.AuthorizeAsync(User, ManagePermission))
        {
            return Forbid();
        }

        if (await Manager.FindByIdAsync(id) is not { } platform)
        {
            return NotFound();
        }

        var key = AgentPlatformApiKeys.Create();

        platform.ApiKeyHash = AgentPlatformApiKeys.Hash(key);
        platform.ApiKeyHint = AgentPlatformApiKeys.Hint(key);
        platform.ModifiedUtc = _clock.UtcNow;

        await Manager.UpdateAsync(platform);
        await Notifier.WarningAsync(H["The platform's new API key is <code>{0}</code>. Copy it now and give it to the platform: it isn't shown again, and the old key no longer works.", key]);

        return RedirectToAction(nameof(Edit), new { id });
    }

    /// <summary>
    /// Gives the platform a new secret its order updates are signed with, and shows it once.
    /// </summary>
    /// <param name="id">The platform's id.</param>
    [HttpPost]
    [Admin("commerce/ai-agents/webhook-secret/{id}", "AgentPlatformsWebhookSecret")]
    public async Task<IActionResult> WebhookSecret(string id)
    {
        if (!await AuthorizationService.AuthorizeAsync(User, ManagePermission))
        {
            return Forbid();
        }

        if (await Manager.FindByIdAsync(id) is not { } platform)
        {
            return NotFound();
        }

        var secret = "whsec_" + Convert.ToHexStringLower(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24));

        platform.ProtectedWebhookSecret = _protector.Protect(secret);
        platform.ModifiedUtc = _clock.UtcNow;

        await Manager.UpdateAsync(platform);
        await Notifier.WarningAsync(H["The platform's order updates are signed with <code>{0}</code> from now on. Copy it now and give it to the platform: it isn't shown again.", secret]);

        return RedirectToAction(nameof(Edit), new { id });
    }
}
