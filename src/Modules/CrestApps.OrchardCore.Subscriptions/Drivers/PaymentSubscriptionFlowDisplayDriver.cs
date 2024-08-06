using CrestApps.OrchardCore.Subscriptions.Core.Handlers;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.ViewModels;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Subscriptions.Drivers;

public sealed class UserRegistrationSubscriptionFlowDisplayDriver : DisplayDriver<SubscriptionFlow>
{
    private readonly IDisplayManager<RegisterUserForm> _registerUserDisplayManager;

    public UserRegistrationSubscriptionFlowDisplayDriver(IDisplayManager<RegisterUserForm> registerUserDisplayManager)
    {
        _registerUserDisplayManager = registerUserDisplayManager;
    }

    public override async Task<IDisplayResult> EditAsync(SubscriptionFlow flow, BuildEditorContext context)
    {
        if (!flow.CurrentStepEquals(UserRegistrationSubscriptionHandler.StepKey))
        {
            return null;
        }

        return Factory("RegisterUserFormSubscription", async (buildContext) =>
        {
            var model = new RegisterUserForm();

            var shape = await _registerUserDisplayManager.BuildEditorAsync(model, context.Updater, false, string.Empty, UserRegistrationSubscriptionHandler.StepKey);

            return shape;
        });
    }

    public override async Task<IDisplayResult> UpdateAsync(SubscriptionFlow flow, UpdateEditorContext context)
    {
        var model = new RegisterUserForm();

        var shape = await _registerUserDisplayManager.UpdateEditorAsync(model, context.Updater, false, string.Empty, UserRegistrationSubscriptionHandler.StepKey);


        return await EditAsync(flow, context);
    }
}

public sealed class PaymentSubscriptionFlowDisplayDriver : DisplayDriver<SubscriptionFlow>
{
    public override Task<IDisplayResult> EditAsync(SubscriptionFlow flow, BuildEditorContext context)
    {
        if (!flow.CurrentStepEquals(PaymentSubscriptionHandler.StepKey))
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        return Task.FromResult<IDisplayResult>(
            View("SubscriptionPayments_Edit", flow.Session.As<Invoice>())
            .Location("Content")
        );
    }

    public override async Task<IDisplayResult> UpdateAsync(SubscriptionFlow model, UpdateEditorContext context)
    {
        var vm = new SubscriptionFlowNavigation();

        // Don't use a prefix for Direction.
        await context.Updater.TryUpdateModelAsync(vm, prefix: string.Empty);

        model.Direction = vm.Direction;

        return await EditAsync(model, context);
    }
}
