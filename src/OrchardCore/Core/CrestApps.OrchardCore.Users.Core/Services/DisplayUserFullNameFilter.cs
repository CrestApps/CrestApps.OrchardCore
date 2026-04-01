using Fluid;
using Fluid.Values;
using OrchardCore.Liquid;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Users.Core.Services;

public sealed class DisplayUserFullNameFilter : ILiquidFilter
{
    private readonly IDisplayNameProvider _displayNameProvider;

    public DisplayUserFullNameFilter(IDisplayNameProvider displayNameProvider)
    {
        _displayNameProvider = displayNameProvider;
    }

    public async ValueTask<FluidValue> ProcessAsync(FluidValue input, FilterArguments arguments, LiquidTemplateContext context)
    {
        if (input.ToObjectValue() is IUser user)
        {
            return new StringValue(await _displayNameProvider.GetAsync(user));
        }

        return StringValue.Empty;
    }
}
