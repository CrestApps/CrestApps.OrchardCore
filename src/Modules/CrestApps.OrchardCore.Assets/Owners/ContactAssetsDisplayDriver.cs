using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Assets.Core.Indexes;
using CrestApps.OrchardCore.Assets.Core.Services;
using CrestApps.OrchardCore.Assets.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Assets.Owners;

/// <summary>
/// Shows the assets a CRM contact owns on its page, with a button to add one for it.
/// </summary>
internal sealed class ContactAssetsDisplayDriver : OwnerAssetsDisplayDriver
{
    public ContactAssetsDisplayDriver(ISession session, AssetTypeManager types, IAuthorizationService authorizationService, IHttpContextAccessor httpContextAccessor)
        : base(session, types, authorizationService, httpContextAccessor, AssetOwnerKind.Contact, "Content:22")
    {
    }

    public override bool CanHandleModel(ContentItem contentItem)
        => contentItem.Has<OmnichannelContactPart>();
}

/// <summary>
/// Shows the assets an account owns on its page, with a button to add one for it.
/// </summary>
internal sealed class AccountAssetsDisplayDriver : OwnerAssetsDisplayDriver
{
    public AccountAssetsDisplayDriver(ISession session, AssetTypeManager types, IAuthorizationService authorizationService, IHttpContextAccessor httpContextAccessor)
        : base(session, types, authorizationService, httpContextAccessor, AssetOwnerKind.Account, "Content:22")
    {
    }

    public override bool CanHandleModel(ContentItem contentItem)
        => contentItem.Has<AccountPart>();
}

/// <summary>
/// Shows the assets a contact or an account owns on its page.
/// </summary>
internal abstract class OwnerAssetsDisplayDriver : ContentDisplayDriver
{
    private const int MaxRows = 10;

    private readonly ISession _session;
    private readonly AssetTypeManager _types;
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AssetOwnerKind _ownerKind;
    private readonly string _location;

    protected OwnerAssetsDisplayDriver(ISession session, AssetTypeManager types, IAuthorizationService authorizationService, IHttpContextAccessor httpContextAccessor, AssetOwnerKind ownerKind, string location)
    {
        _session = session;
        _types = types;
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
        _ownerKind = ownerKind;
        _location = location;
    }

    public override async Task<IDisplayResult> DisplayAsync(ContentItem contentItem, BuildDisplayContext context)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (context.DisplayType != OrchardCoreConstants.DisplayType.DetailAdmin || !await _authorizationService.AuthorizeAsync(user, AssetsPermissions.ViewAssets))
        {
            return null;
        }

        var canManage = await _authorizationService.AuthorizeAsync(user, AssetsPermissions.ManageAssets);

        return Initialize<ContactAssetsViewModel>("ContactAssets", async model =>
        {
            var ownerKind = _ownerKind;
            var kind = ownerKind.ToString();
            var contactId = contentItem.ContentItemId;
            var query = _session.Query<Asset, AssetIndex>(index => index.OwnerKind == kind && index.OwnerId == contactId, collection: AssetsConstants.CollectionName);
            var types = await _types.GetAllAsync();

            model.ContactId = contactId;
            model.OwnerKind = ownerKind;
            model.Count = await query.CountAsync();
            model.Assets = [.. await query.OrderBy(index => index.Name).Take(MaxRows).ListAsync()];
            model.TypeNames = types.ToDictionary(type => type.ItemId, type => type.Name, StringComparer.Ordinal);
            model.CanManage = canManage;
        }).Location(OrchardCoreConstants.DisplayType.DetailAdmin, _location);
    }
}

/// <summary>
/// The assets a contact owns.
/// </summary>
public class ContactAssetsViewModel
{
    /// <summary>
    /// Gets or sets the contact.
    /// </summary>
    public string ContactId { get; set; }

    /// <summary>
    /// Gets or sets whether the owner is a contact or an account.
    /// </summary>
    public AssetOwnerKind OwnerKind { get; set; } = AssetOwnerKind.Contact;

    /// <summary>
    /// Gets or sets how many assets the contact owns.
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Gets or sets the first assets by name.
    /// </summary>
    public IList<Asset> Assets { get; set; } = [];

    /// <summary>
    /// Gets or sets the asset types' names by id.
    /// </summary>
    public IReadOnlyDictionary<string, string> TypeNames { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// Gets or sets whether the user can add assets.
    /// </summary>
    public bool CanManage { get; set; }
}
