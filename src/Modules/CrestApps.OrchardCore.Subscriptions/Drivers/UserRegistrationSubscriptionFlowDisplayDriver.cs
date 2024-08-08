using CrestApps.OrchardCore.Subscriptions.Core.Handlers;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Subscriptions.Drivers;

public sealed class UserRegistrationSubscriptionFlowDisplayDriver : DisplayDriver<SubscriptionFlow>
{
    private readonly IDisplayManager<RegisterUserForm> _registerUserDisplayManager;

    public UserRegistrationSubscriptionFlowDisplayDriver(IDisplayManager<RegisterUserForm> registerUserDisplayManager)
    {
        _registerUserDisplayManager = registerUserDisplayManager;
    }

    public override IDisplayResult Edit(SubscriptionFlow flow, BuildEditorContext context)
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
