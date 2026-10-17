using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Checkout;

/// <summary>
/// Where a customer goes back to from a checkout, and what the checkout calls itself, as the module that started
/// it asked through <see cref="Services.StartCheckoutRequest"/>. It is stored in the checkout session property bag.
/// </summary>
/// <remarks>
/// A checkout is usually started from a page of its own: a family portal's payments list, a registration form, a
/// pay link. Without this the customer finishes on a generic confirmation with no way back, and one who abandons
/// the payment step has no link back either.
/// </remarks>
public sealed class CheckoutReturnInfo
{
    /// <summary>
    /// Gets or sets the local URL the customer returns to. It is shown as a <b>Continue</b> button on the
    /// confirmation page and as a <b>Back</b> link on the payment step and on the page of a canceled or expired
    /// checkout.
    /// </summary>
    public string ReturnUrl { get; set; }

    /// <summary>
    /// Gets or sets the text of the link to <see cref="ReturnUrl"/>, for example <c>Back to my payments</c>. When
    /// empty, the page uses its own wording.
    /// </summary>
    public string ReturnText { get; set; }

    /// <summary>
    /// Gets or sets the heading of the confirmation page, for example <c>Payment received</c>, so a checkout that is
    /// not an order is not headed "Order confirmation".
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets the return information stored on a session, or <see langword="null"/> when it has none. A stored URL that
    /// is not local is ignored, so a link to another site is never rendered even if one reached the store.
    /// </summary>
    /// <param name="session">The checkout session.</param>
    public static CheckoutReturnInfo GetFrom(ICheckoutFlowSession session)
    {
        if (session is null || !session.TryGet<CheckoutReturnInfo>(out var info) || info is null)
        {
            return null;
        }

        if (!string.IsNullOrEmpty(info.ReturnUrl) && !IsLocalUrl(info.ReturnUrl))
        {
            info.ReturnUrl = null;
        }

        return info;
    }

    /// <summary>
    /// Gets whether <paramref name="url"/> is local to this site: a path that starts with <c>/</c> or <c>~/</c>, but
    /// not a protocol-relative <c>//</c> or <c>/\</c> address, and with no control characters. It applies the same
    /// rule as ASP.NET Core's <c>IUrlHelper.IsLocalUrl</c>, which a service that has no request cannot call.
    /// </summary>
    /// <param name="url">The URL to check.</param>
    public static bool IsLocalUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return false;
        }

        if (url[0] == '/')
        {
            if (url.Length == 1)
            {
                return true;
            }

            return url[1] != '/' && url[1] != '\\' && !HasControlCharacter(url, 1);
        }

        if (url[0] == '~' && url.Length > 1 && url[1] == '/')
        {
            if (url.Length == 2)
            {
                return true;
            }

            return url[2] != '/' && url[2] != '\\' && !HasControlCharacter(url, 2);
        }

        return false;
    }

    private static bool HasControlCharacter(string value, int startIndex)
    {
        for (var i = startIndex; i < value.Length; i++)
        {
            if (char.IsControl(value[i]))
            {
                return true;
            }
        }

        return false;
    }
}
