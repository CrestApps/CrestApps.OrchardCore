using System.Text.Json;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Handlers;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Json;
using OrchardCore.Settings;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Subscriptions.Core.Handlers;

/// <summary>
/// Lets somebody who is not signed in buy a subscription, by creating and signing in their account as part
/// of the checkout.
/// </summary>
/// <remarks>
/// The account is created in <see cref="CompletingAsync"/>, before any money moves, and deleted again in
/// <see cref="FailedAsync"/> when the checkout does not complete. Creating it after payment would leave a
/// paying customer with no account whenever account creation failed; creating it and leaving it behind on a
/// failure would let a visitor accumulate accounts by abandoning checkouts.
/// </remarks>
public sealed class UserRegistrationCheckoutHandler : CheckoutHandlerBase
{
    /// <summary>
    /// The cache purpose under which the registration password is held for the life of the checkout.
    /// </summary>
    public const string PasswordPurpose = "SubscriptionUserRegistrationPassword";

    private readonly UserManager<IUser> _userManager;
    private readonly SignInManager<IUser> _signInManager;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly PaymentSessionCache _paymentSessionCache;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly ISiteService _siteService;
    private readonly DocumentJsonSerializerOptions _documentJsonSerializerOptions;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserRegistrationCheckoutHandler"/> class.
    /// </summary>
    /// <param name="userManager">The user manager.</param>
    /// <param name="signInManager">The sign-in manager.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="paymentSessionCache">The cache that holds the password outside the database.</param>
    /// <param name="dataProtectionProvider">The data protection provider used to protect the password.</param>
    /// <param name="siteService">The site service used to read the subscription role settings.</param>
    /// <param name="documentJsonSerializerOptions">The document serializer options.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public UserRegistrationCheckoutHandler(
        UserManager<IUser> userManager,
        SignInManager<IUser> signInManager,
        IHttpContextAccessor httpContextAccessor,
        PaymentSessionCache paymentSessionCache,
        IDataProtectionProvider dataProtectionProvider,
        ISiteService siteService,
        IOptions<DocumentJsonSerializerOptions> documentJsonSerializerOptions,
        IStringLocalizer<UserRegistrationCheckoutHandler> stringLocalizer)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _httpContextAccessor = httpContextAccessor;
        _paymentSessionCache = paymentSessionCache;
        _dataProtectionProvider = dataProtectionProvider;
        _siteService = siteService;
        _documentJsonSerializerOptions = documentJsonSerializerOptions.Value;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override Task ActivatingAsync(CheckoutFlowActivatingContext context)
    {
        if (!SubscriptionCheckout.IsSubscriptionReference(context.Session.ReferenceType))
        {
            return Task.CompletedTask;
        }

        context.Session.Steps.Add(new CheckoutFlowStep
        {
            Key = SubscriptionConstants.StepKey.UserRegistration,
            Title = S["Registration"],
            Description = S["Manage your subscription by creating an account."],
            Order = 2,
            CollectData = true,
            Conceal = HasOwner(context.Session),
        });

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task InitializingAsync(CheckoutFlowInitializingContext context)
    {
        // Whether the step applies is a fact about the checkout, not about the request that loaded it. A
        // provider webhook and the reconciliation sweep both complete checkouts with no signed-in user at
        // all; deciding from the request would make them see a registration step the signed-in buyer never
        // had, and refuse to complete a purchase that was already paid for.
        foreach (var step in context.Flow.Session.Steps)
        {
            if (string.Equals(step.Key, SubscriptionConstants.StepKey.UserRegistration, StringComparison.Ordinal))
            {
                step.Conceal = HasOwner(context.Flow.Session);
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override async Task CompletingAsync(CheckoutFlowCompletingContext context)
    {
        var session = context.Flow.Session;

        // A checkout that already has an owner needs no account: the buyer was signed in when it began, or
        // an earlier attempt at completing it already created one.
        if (HasOwner(session))
        {
            return;
        }

        if (!session.SavedSteps.TryGetPropertyValue(SubscriptionConstants.StepKey.UserRegistration, out var node))
        {
            return;
        }

        var step = node.Deserialize<UserRegistrationStep>(_documentJsonSerializerOptions.SerializerOptions);

        if (step is null || step.IsGuest || step.User is null)
        {
            return;
        }

        var roleSettings = await _siteService.GetSettingsAsync<SubscriptionRoleSettings>();

        if (roleSettings.RoleNames is not null)
        {
            foreach (var roleName in roleSettings.RoleNames)
            {
                step.User.RoleNames.Add(roleName);
            }
        }

        var password = await GetPasswordAsync(session.SessionId);

        var result = await _userManager.CreateAsync(step.User, password);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException("Unable to create a user account for the subscriber.");
        }

        // Only a request can sign the new account in afterwards. A completion driven by the sweep or a
        // webhook has no request, and the account is simply there for the customer next time they visit.
        _httpContextAccessor.HttpContext?.Features.Set(new CustomerCreatedDuringSubscriptionFlow
        {
            User = step.User,
            Password = password,
        });

        // The account that was just created owns this checkout, so everything the completion writes is
        // attributed to the subscriber rather than left ownerless.
        session.OwnerId = step.User.UserId;
    }

    /// <inheritdoc/>
    public override async Task CompletedAsync(CheckoutFlowCompletedContext context)
    {
        var created = _httpContextAccessor.HttpContext?.Features.Get<CustomerCreatedDuringSubscriptionFlow>();

        if (created is null)
        {
            return;
        }

        await _paymentSessionCache.RemoveAsync(context.Flow.Session.SessionId, PasswordPurpose);

        await _signInManager.PasswordSignInAsync(created.User, created.Password, isPersistent: false, lockoutOnFailure: true);
    }

    /// <inheritdoc/>
    public override async Task FailedAsync(CheckoutFlowFailedContext context)
    {
        var created = _httpContextAccessor.HttpContext?.Features.Get<CustomerCreatedDuringSubscriptionFlow>();

        if (created is null)
        {
            return;
        }

        // Only delete the account this checkout created. Checking the password first is what proves it is
        // that account and not one that already existed under the same name.
        var result = await _signInManager.CheckPasswordSignInAsync(created.User, created.Password, lockoutOnFailure: false);

        if (result.Succeeded)
        {
            await _userManager.DeleteAsync(created.User);
        }
    }

    private async Task<string> GetPasswordAsync(string sessionId)
    {
        var protectedPassword = await _paymentSessionCache.GetAsync<string>(sessionId, PasswordPurpose);

        if (string.IsNullOrEmpty(protectedPassword))
        {
            throw new InvalidOperationException("The registration password is no longer available for this checkout.");
        }

        return _dataProtectionProvider
            .CreateProtector(SubscriptionConstants.ProtectorPurposes.UserRegistrationStep)
            .Unprotect(protectedPassword);
    }

    private static bool HasOwner(ICheckoutFlowSession session)
        => !string.IsNullOrEmpty(session?.OwnerId);
}
