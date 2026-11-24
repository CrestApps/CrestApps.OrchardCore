using System.Security.Claims;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Payroll.ViewModels;
using CrestApps.OrchardCore.Staff;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Payroll.Drivers;

/// <summary>
/// My Pay, in the Pay section of the person's own staff profile: coming pay to review, pay statements with their stubs,
/// tax forms, and the pay details they may change. Only the person sees it, once they confirmed their password.
/// </summary>
internal sealed class MyPayProfileDisplayDriver : DisplayDriver<StaffMember>
{
    private readonly MyPayPageBuilder _builder;
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public MyPayProfileDisplayDriver(MyPayPageBuilder builder, IAuthorizationService authorizationService, IHttpContextAccessor httpContextAccessor)
    {
        _builder = builder;
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
    }

    public override async Task<IDisplayResult> DisplayAsync(StaffMember staff, BuildDisplayContext context)
    {
        if (context.DisplayType != StaffProfileSections.DisplayType || _httpContextAccessor.HttpContext is not { } httpContext)
        {
            return null;
        }

        var user = httpContext.User;

        // Someone else's profile never shows My Pay, even to payroll: their cards show what they may see.
        if (string.IsNullOrEmpty(staff.UserId)
            || staff.UserId != user.FindFirstValue(ClaimTypes.NameIdentifier)
            || !await _authorizationService.AuthorizeAsync(user, PayrollPermissions.ViewOwnPay))
        {
            return null;
        }

        // Pay is private: the person types their password each time they open it; until then the card is the one prompt, or nothing.
        switch (await StaffPayPrivacy.DecideAsync(httpContext, staff))
        {
            case StaffPayVisibility.Hide:
                return null;
            case StaffPayVisibility.Prompt:
                return Dynamic(StaffPayPrivacy.PromptShape, shape => { shape.ConfirmUrl = StaffPayPrivacy.ConfirmUrl(httpContext); })
                    .Location(StaffProfileSections.DisplayType, StaffProfileSections.Location(StaffProfileSections.Pay, "0"));
        }

        var model = await _builder.BuildAsync(staff, user, httpContext.RequestAborted);

        return Initialize<MyPayIndexViewModel>("MyPayProfile", view =>
        {
            view.IsStaff = model.IsStaff;
            view.Statements = model.Statements;
            view.Profiles = model.Profiles;
            view.CanManageDetails = model.CanManageDetails;
            view.ToReview = model.ToReview;
            view.TaxForms = model.TaxForms;
            view.ComingPay = model.ComingPay;
            view.PayDetails = model.PayDetails;
            view.CanManageBankAccounts = model.CanManageBankAccounts;
            view.HasTaxForms = model.HasTaxForms;
        }).Location(StaffProfileSections.DisplayType, StaffProfileSections.Location(StaffProfileSections.Pay, "2"));
    }
}
