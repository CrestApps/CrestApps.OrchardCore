using CrestApps.OrchardCore.Accounts;
using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Agreements.Core.Indexes;
using CrestApps.OrchardCore.Agreements.Models;
using CrestApps.OrchardCore.Agreements.Services;
using CrestApps.OrchardCore.Agreements.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Agreements.Drivers;

/// <summary>
/// An account's agreements on its page, with how their hours stand.
/// </summary>
internal sealed class AccountAgreementsDisplayDriver : ContentDisplayDriver
{
    private const int MaxRows = 25;

    private readonly ISession _session;
    private readonly IAgreementManager _agreements;
    private readonly IAgreementBalanceService _balances;
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AccountAgreementsDisplayDriver(
        ISession session,
        IAgreementManager agreements,
        IAgreementBalanceService balances,
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor)
    {
        _session = session;
        _agreements = agreements;
        _balances = balances;
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
    }

    public override bool CanHandleModel(ContentItem contentItem)
        => contentItem.Has<AccountPart>();

    public override async Task<IDisplayResult> DisplayAsync(ContentItem contentItem, BuildDisplayContext context)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (context.DisplayType != OrchardCoreConstants.DisplayType.DetailAdmin || !await _authorizationService.AuthorizeAsync(user, AgreementsPermissions.ViewAgreements))
        {
            return null;
        }

        var canManage = await _authorizationService.AuthorizeAsync(user, AgreementsPermissions.ManageAgreements);

        return Initialize<AccountAgreementsViewModel>("AccountAgreements", async model =>
        {
            var cancellationToken = _httpContextAccessor.HttpContext?.RequestAborted ?? default;
            var accountId = contentItem.ContentItemId;
            var ids = (await _session.QueryIndex<AgreementIndex>(index => index.AccountId == accountId, collection: AgreementsConstants.CollectionName)
                .OrderByDescending(index => index.Number)
                .Take(MaxRows)
                .ListAsync(cancellationToken))
                .Select(index => index.ItemId)
                .ToList();
            var rows = new List<(Agreement, AgreementBalance)>();

            foreach (var id in ids)
            {
                if (await _agreements.FindByIdAsync(id, cancellationToken) is { } agreement)
                {
                    rows.Add((agreement, agreement.Status == AgreementStatus.Active ? await _balances.GetBalanceAsync(agreement, cancellationToken: cancellationToken) : null));
                }
            }

            model.AccountId = accountId;
            model.CanManage = canManage;
            model.Agreements = rows;
        }).Location(OrchardCoreConstants.DisplayType.DetailAdmin, AccountPageTabs.Location(AccountPageTabs.Service, "2"));
    }
}
