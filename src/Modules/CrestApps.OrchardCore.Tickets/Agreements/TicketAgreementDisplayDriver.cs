using CrestApps.OrchardCore.Agreements;
using CrestApps.OrchardCore.Agreements.Services;
using CrestApps.OrchardCore.Tickets.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Tickets.Agreements;

/// <summary>
/// The agreement a ticket works under, beside it, with how the agreement's hours stand, for those who may see agreements.
/// </summary>
internal sealed class TicketAgreementDisplayDriver : ContentPartDisplayDriver<TicketPart>
{
    private readonly IAgreementManager _agreements;
    private readonly IAgreementBalanceService _balances;
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TicketAgreementDisplayDriver(IAgreementManager agreements, IAgreementBalanceService balances, IAuthorizationService authorizationService, IHttpContextAccessor httpContextAccessor)
    {
        _agreements = agreements;
        _balances = balances;
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
    }

    public override async Task<IDisplayResult> DisplayAsync(TicketPart part, BuildPartDisplayContext context)
    {
        if (context.DisplayType != TicketsConstants.DisplayTypes.DetailAdmin
            || string.IsNullOrEmpty(part.AgreementId)
            || _httpContextAccessor.HttpContext?.User is not { } user
            || !await _authorizationService.AuthorizeAsync(user, AgreementsPermissions.ViewAgreements))
        {
            return null;
        }

        var cancellationToken = _httpContextAccessor.HttpContext.RequestAborted;

        if (await _agreements.FindByIdAsync(part.AgreementId, cancellationToken) is not { } agreement)
        {
            return null;
        }

        var balance = await _balances.GetBalanceAsync(agreement, cancellationToken: cancellationToken);

        return Initialize<TicketAgreementViewModel>("TicketPart_Agreement", model =>
        {
            model.Agreement = agreement;
            model.Balance = balance;
        }).Location(TicketsConstants.DisplayTypes.DetailAdmin, "Sidebar:1.6");
    }
}
