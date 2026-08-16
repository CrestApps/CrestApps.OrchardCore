using System.Text.Json;
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
/// Adds user registration to subscription flows and creates or cleans up subscriber accounts.
/// </summary>
public sealed class UserRegistrationSubscriptionHandler : SubscriptionHandlerBase
{
    private readonly UserManager<IUser> _userManager;
    private readonly SignInManager<IUser> _signInManager;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly SubscriptionPaymentSession _subscriptionPaymentSession;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly ISiteService _siteService;
    private readonly DocumentJsonSerializerOptions _documentJsonSerializerOptions;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserRegistrationSubscriptionHandler"/> class.
    /// </summary>
    /// <param name="userManager">The user manager used to create and delete subscriber accounts.</param>
    /// <param name="signInManager">The sign-in manager used to validate and sign in subscriber accounts.</param>
    /// <param name="httpContextAccessor">The accessor used to read the current user and request features.</param>
    /// <param name="documentJsonSerializerOptions">The JSON serializer options used for persisted registration step data.</param>
    /// <param name="subscriptionPaymentSession">The cache that stores protected registration passwords during the flow.</param>
    /// <param name="dataProtectionProvider">The data protection provider used to unprotect cached registration passwords.</param>
    /// <param name="siteService">The site service used to read subscription role settings.</param>
    /// <param name="stringLocalizer">The localizer used for subscription flow step text.</param>
    public UserRegistrationSubscriptionHandler(
        UserManager<IUser> userManager,
        SignInManager<IUser> signInManager,
        IHttpContextAccessor httpContextAccessor,
        IOptions<DocumentJsonSerializerOptions> documentJsonSerializerOptions,
        SubscriptionPaymentSession subscriptionPaymentSession,
        IDataProtectionProvider dataProtectionProvider,
        ISiteService siteService,
        IStringLocalizer<PaymentSubscriptionHandler> stringLocalizer)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _httpContextAccessor = httpContextAccessor;
        _subscriptionPaymentSession = subscriptionPaymentSession;
        _dataProtectionProvider = dataProtectionProvider;
        _siteService = siteService;
        _documentJsonSerializerOptions = documentJsonSerializerOptions.Value;
        S = stringLocalizer;
    }

    /// <summary>
    /// Adds the registration step to the subscription flow and conceals it for authenticated users.
    /// </summary>
    /// <param name="context">The context for the subscription flow that is being activated.</param>
    public override Task ActivatingAsync(SubscriptionFlowActivatingContext context)
    {
        context.Session.Steps.Add(new SubscriptionFlowStep()
        {
            Title = S["Registration"],
            Description = S["Manage Your Subscription by Creating an Account."],
            Key = SubscriptionConstants.StepKey.UserRegistration,
            Order = 2,
            CollectData = true,
            Conceal = _httpContextAccessor.HttpContext.User?.Identity?.IsAuthenticated ?? false,
        });

        return Task.CompletedTask;
    }

    /// <summary>
    /// Updates the registration step visibility when an existing session is initialized.
    /// </summary>
    /// <param name="context">The context for the subscription flow that is initializing.</param>
    public override Task InitializingAsync(SubscriptionFlowInitializingContext context)
    {
        foreach (var step in context.Session.Steps)
        {
            if (step.Key != SubscriptionConstants.StepKey.UserRegistration)
            {
                continue;
            }

            // When a user is already authentication, we need to conceal this step.
            step.Conceal = _httpContextAccessor.HttpContext.User?.Identity?.IsAuthenticated ?? false;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Creates the subscriber account from the collected registration step data before the flow completes.
    /// </summary>
    /// <param name="context">The context for the subscription flow that is completing.</param>
    public override async Task CompletingAsync(SubscriptionFlowCompletingContext context)
    {
        if (_httpContextAccessor.HttpContext.User?.Identity?.IsAuthenticated == true)
        {
            return;
        }

        if (!context.Flow.Session.SavedSteps.TryGetPropertyValue(SubscriptionConstants.StepKey.UserRegistration, out var node))
        {
            return;
        }

        var registrationStep = node.Deserialize<UserRegistrationStep>(_documentJsonSerializerOptions.SerializerOptions);

        if (registrationStep.IsGuest)
        {
            return;
        }

        var settings = await _siteService.GetSettingsAsync<SubscriptionRoleSettings>();

        if (settings.RoleNames != null)
        {
            foreach (var roleName in settings.RoleNames)
            {
                registrationStep.User.RoleNames.Add(roleName);
            }
        }

        var password = await _subscriptionPaymentSession.GetUserPasswordAsync(context.Flow.Session.SessionId, _dataProtectionProvider);

        var result = await _userManager.CreateAsync(registrationStep.User, password);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException("Unable to create a user account.");
        }

        _httpContextAccessor.HttpContext.Features.Set(new CustomerCreatedDuringSubscriptionFlow()
        {
            User = registrationStep.User,
            Password = password,
        });

        // Since we just created a new user, let's set the user id as the owner of this session.
        context.Flow.Session.OwnerId = registrationStep.User.UserId;
    }

    /// <summary>
    /// Removes cached registration secrets and signs in the user created during the flow.
    /// </summary>
    /// <param name="context">The context for the subscription flow that completed.</param>
    public override async Task CompletedAsync(SubscriptionFlowCompletedContext context)
    {
        var subscriber = _httpContextAccessor.HttpContext.Features.Get<CustomerCreatedDuringSubscriptionFlow>();

        if (subscriber == null)
        {
            return;
        }

        await _subscriptionPaymentSession.RemoveUserPasswordAsync(context.Flow.Session.SessionId);

        await _signInManager.PasswordSignInAsync(subscriber.User, subscriber.Password, isPersistent: false, lockoutOnFailure: true);
    }

    /// <summary>
    /// Deletes the user account created during the flow when completion fails.
    /// </summary>
    /// <param name="context">The context for the subscription flow that failed.</param>
    public override async Task FailedAsync(SubscriptionFlowFailedContext context)
    {
        var subscriber = _httpContextAccessor.HttpContext.Features.Get<CustomerCreatedDuringSubscriptionFlow>();

        if (subscriber == null)
        {
            return;
        }

        // If the session creation fails, we need to delete the user account.
        // To ensure safety, we will first verify that we can log in with the expected password before proceeding with the deletion.
        var result = await _signInManager.CheckPasswordSignInAsync(subscriber.User, subscriber.Password, false);

        if (result.Succeeded)
        {
            await _userManager.DeleteAsync(subscriber.User);
        }
    }
}
