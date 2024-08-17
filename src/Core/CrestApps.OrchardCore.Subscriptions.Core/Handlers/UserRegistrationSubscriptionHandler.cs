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

        await _subscriptionPaymentSession.RemoveUserPasswordAsync(context.Flow.Session.SessionId);

        // Since we just created a new user, let's set the user id as the owner of this session.
        context.Flow.Session.OwnerId = registrationStep.User.UserId;
    }

    public override async Task CompletedAsync(SubscriptionFlowCompletedContext context)
    {
        var subscriber = _httpContextAccessor.HttpContext.Features.Get<CustomerCreatedDuringSubscriptionFlow>();

        if (subscriber == null)
        {
            return;
        }

        await _signInManager.PasswordSignInAsync(subscriber.User, subscriber.Password, isPersistent: false, lockoutOnFailure: true);
    }
}
