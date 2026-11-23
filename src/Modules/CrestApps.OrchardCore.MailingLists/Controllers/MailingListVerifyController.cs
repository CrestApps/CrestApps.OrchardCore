using CrestApps.OrchardCore.MailingLists.Core.Services;
using CrestApps.OrchardCore.MailingLists.Models;
using CrestApps.OrchardCore.MailingLists.Services;
using CrestApps.OrchardCore.MailingLists.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.MailingLists.Controllers;

/// <summary>
/// The page a confirmation or claim link opens. Opening the link changes nothing: the page shows what will be confirmed,
/// and only the person's click on <b>Confirm</b> posts the change, so mail scanners that open every link (and run its
/// scripts) never subscribe anyone. The signed token in the link is the authorization, as for an unsubscribe link.
/// </summary>
public sealed class MailingListVerifyController : Controller
{
    private const string ShapeType = "MailingListVerify";

    private readonly IMailingListMemberService _members;
    private readonly IShapeFactory _shapeFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="MailingListVerifyController"/> class.
    /// </summary>
    /// <param name="members">The member service.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    public MailingListVerifyController(
        IMailingListMemberService members,
        IShapeFactory shapeFactory)
    {
        _members = members;
        _shapeFactory = shapeFactory;
    }

    /// <summary>
    /// Shows what the link confirms, with a <b>Confirm</b> button.
    /// </summary>
    /// <param name="token">The token from the link.</param>
    /// <returns>The page.</returns>
    [AllowAnonymous]
    // The token travels in the query string (?token=): IIS (HTTP.sys) refuses a path segment longer than 260
    // characters. The path form is kept for links that carry it.
    [HttpGet("mailing-lists/verify", Name = MailingListsConstants.RouteNames.Verify)]
    [HttpGet("mailing-lists/verify/{token}")]
    public async Task<IActionResult> Index(string token)
    {
        var result = await _members.ReadVerificationAsync(token, HttpContext.RequestAborted);

        return await PageAsync(token, result, resent: false, busy: false);
    }

    /// <summary>
    /// Verifies the address after the person's click, and shows the result with the welcome coupon when one was issued.
    /// </summary>
    /// <param name="token">The token from the link.</param>
    /// <returns>The page.</returns>
    [AllowAnonymous]
    [HttpPost("mailing-lists/verify")]
    [HttpPost("mailing-lists/verify/{token}")]
    [ActionName(nameof(Index))]
    public async Task<IActionResult> IndexPost(string token)
    {
        try
        {
            var result = await _members.VerifyAsync(token, HttpContext.RequestAborted);

            return await PageAsync(token, result, resent: false, busy: false);
        }
        catch (TimeoutException)
        {
            return await PageAsync(token, await _members.ReadVerificationAsync(token, HttpContext.RequestAborted), resent: false, busy: true);
        }
    }

    /// <summary>
    /// Sends a new link to the address an expired link was sent to. The page says the same whether or not a link left.
    /// </summary>
    /// <param name="token">The token from the expired link.</param>
    /// <returns>The page.</returns>
    [AllowAnonymous]
    [HttpPost("mailing-lists/verify/resend", Name = MailingListsConstants.RouteNames.VerifyResend)]
    [HttpPost("mailing-lists/verify/{token}/resend")]
    public async Task<IActionResult> Resend(string token)
    {
        var result = await _members.ReadVerificationAsync(token, HttpContext.RequestAborted);

        if (result.Outcome == MailingListVerifyOutcome.Expired && MailingListVerification.CanResend(result.Member))
        {
            try
            {
                await _members.ResendConfirmationAsync(result.Member.ItemId, HttpContext.RequestAborted);
            }
            catch (TimeoutException)
            {
                return await PageAsync(token, result, resent: false, busy: true);
            }
        }

        return await PageAsync(token, result, resent: true, busy: false);
    }

    private async Task<IActionResult> PageAsync(string token, MailingListVerifyResult result, bool resent, bool busy)
        => View(nameof(Index), await _shapeFactory.CreateAsync<MailingListVerifyViewModel>(ShapeType, model =>
        {
            model.Token = token;
            model.Outcome = result.Outcome;
            model.ListName = result.List?.DisplayText;
            model.Email = result.Member?.Contact?.Email ?? result.Member?.NormalizedEmail;
            model.AlreadySubscribed = result.Member?.Status == MailingListMemberStatus.Subscribed && result.Outcome == MailingListVerifyOutcome.Ready;
            model.CanResend = result.Outcome == MailingListVerifyOutcome.Expired && MailingListVerification.CanResend(result.Member);
            model.Resent = resent;
            model.Busy = busy;
            model.Coupon = result.Coupon;
        }));
}
