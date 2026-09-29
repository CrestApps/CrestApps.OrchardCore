using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Lists.Models;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

/// <summary>
/// Lets a contact or an opportunity be placed in an account, moved to another one, or taken out of one. Orchard
/// Core's list part only places an item in a list when it is created from inside the list, so this editor writes
/// the same <see cref="ContainedPart"/> the list part uses rather than keeping a relationship of its own.
/// </summary>
internal sealed class AccountPickerDisplayDriver : ContentDisplayDriver
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly IContentManager _contentManager;
    private readonly IHttpContextAccessor _httpContextAccessor;

    private readonly Dictionary<string, ContentTypeDefinition> _definitions = new(StringComparer.Ordinal);

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AccountPickerDisplayDriver"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    /// <param name="contentManager">The content manager.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AccountPickerDisplayDriver(
        IContentDefinitionManager contentDefinitionManager,
        IContentManager contentManager,
        IHttpContextAccessor httpContextAccessor,
        IStringLocalizer<AccountPickerDisplayDriver> stringLocalizer)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _contentManager = contentManager;
        _httpContextAccessor = httpContextAccessor;
        S = stringLocalizer;
    }

    public override async Task<IDisplayResult> DisplayAsync(ContentItem contentItem, BuildDisplayContext context)
    {
        if (!contentItem.TryGet<ContainedPart>(out var contained) ||
            string.IsNullOrEmpty(contained.ListContentItemId) ||
            !await IsAccountTypeAsync(contained.ListContentType) ||
            !await IsAccountChildAsync(contentItem.ContentType))
        {
            return null;
        }

        return Initialize<AccountPickerViewModel>("AccountPicker_SummaryAdmin", async model =>
        {
            var account = await _contentManager.GetAsync(contained.ListContentItemId, VersionOptions.Latest);

            model.AccountContentItemId = contained.ListContentItemId;
            model.AccountDisplayText = account?.DisplayText;
        }).Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:3");
    }

    public override async Task<IDisplayResult> EditAsync(ContentItem contentItem, BuildEditorContext context)
    {
        if (!await IsAccountChildAsync(contentItem.ContentType) || !await HasAccountTypesAsync())
        {
            return null;
        }

        var (accountId, accountType) = GetCurrentAccount(contentItem);

        // An item kept in a list that is not an account belongs to that list; this editor leaves it alone.
        if (!string.IsNullOrEmpty(accountType) && !await IsAccountTypeAsync(accountType))
        {
            return null;
        }

        return Initialize<AccountPickerViewModel>("AccountPicker_Edit", async model =>
        {
            model.AccountContentItemId = accountId;

            if (!string.IsNullOrEmpty(accountId))
            {
                var account = await _contentManager.GetAsync(accountId, VersionOptions.Latest);

                model.AccountDisplayText = account?.DisplayText ?? accountId;
            }
        }).Location("Parts:0");
    }

    public override async Task<IDisplayResult> UpdateAsync(ContentItem contentItem, UpdateEditorContext context)
    {
        if (!await IsAccountChildAsync(contentItem.ContentType) || !await HasAccountTypesAsync())
        {
            return null;
        }

        var (_, currentAccountType) = GetCurrentAccount(contentItem);

        if (!string.IsNullOrEmpty(currentAccountType) && !await IsAccountTypeAsync(currentAccountType))
        {
            return null;
        }

        var model = new AccountPickerViewModel();

        if (!await context.Updater.TryUpdateModelAsync(model, Prefix) || !model.Rendered)
        {
            return await EditAsync(contentItem, context);
        }

        var accountId = string.IsNullOrWhiteSpace(model.AccountContentItemId)
            ? null
            : model.AccountContentItemId.Trim();

        if (accountId is null)
        {
            if (contentItem.Has<ContainedPart>())
            {
                ((System.Text.Json.Nodes.JsonObject)contentItem.Content).Remove(nameof(ContainedPart));
            }

            return await EditAsync(contentItem, context);
        }

        var account = await _contentManager.GetAsync(accountId, VersionOptions.Latest);

        if (account is null || !await IsAccountTypeAsync(account.ContentType))
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.AccountContentItemId), S["The selected account no longer exists."]);

            return await EditAsync(contentItem, context);
        }

        contentItem.Alter<ContainedPart>(part =>
        {
            if (!string.Equals(part.ListContentItemId, account.ContentItemId, StringComparison.Ordinal))
            {
                part.Order = 0;
            }

            part.ListContentItemId = account.ContentItemId;
            part.ListContentType = account.ContentType;
        });

        return await EditAsync(contentItem, context);
    }

    private (string AccountId, string AccountType) GetCurrentAccount(ContentItem contentItem)
    {
        if (contentItem.TryGet<ContainedPart>(out var contained) && !string.IsNullOrEmpty(contained.ListContentItemId))
        {
            return (contained.ListContentItemId, contained.ListContentType);
        }

        // A new item created from inside an account carries the account in the query string until it is saved.
        var query = _httpContextAccessor.HttpContext?.Request.Query;
        var containerId = query?["ListPart.ContainerId"].ToString();

        if (!string.IsNullOrEmpty(containerId))
        {
            return (containerId, query["ListPart.ContainerContentType"].ToString());
        }

        return (null, null);
    }

    private async Task<bool> IsAccountChildAsync(string contentType)
        => OmnichannelRecordKinds.IsAccountChild(await GetDefinitionAsync(contentType));

    private async Task<bool> IsAccountTypeAsync(string contentType)
        => OmnichannelRecordKinds.IsAccount(await GetDefinitionAsync(contentType));

    private async Task<bool> HasAccountTypesAsync()
        => (await _contentDefinitionManager.ListTypeDefinitionsAsync()).Any(OmnichannelRecordKinds.IsAccount);

    private async Task<ContentTypeDefinition> GetDefinitionAsync(string contentType)
    {
        if (string.IsNullOrEmpty(contentType))
        {
            return null;
        }

        if (!_definitions.TryGetValue(contentType, out var definition))
        {
            definition = await _contentDefinitionManager.GetTypeDefinitionAsync(contentType);
            _definitions[contentType] = definition;
        }

        return definition;
    }
}
