using Fluid;
using Fluid.Values;
using OrchardCore.Liquid;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Users.Core.Services;

/// <summary>
/// A Liquid filter that resolves an <see cref="IUser"/> to its display name.
/// </summary>
public sealed class DisplayUserFullNameFilter : ILiquidFilter
{
    private readonly IDisplayNameProvider _displayNameProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="DisplayUserFullNameFilter"/> class.
    /// </summary>
    /// <param name="displayNameProvider">The provider used to resolve the user's display name.</param>
    public DisplayUserFullNameFilter(IDisplayNameProvider displayNameProvider)
    {
        _displayNameProvider = displayNameProvider;
    }

    /// <summary>
    /// Processes the Liquid filter by resolving the input user to its display name.
    /// </summary>
    /// <param name="input">The input Fluid value, expected to be an <see cref="IUser"/>.</param>
    /// <param name="arguments">The filter arguments (unused).</param>
    /// <param name="context">The Liquid template context.</param>
    public async ValueTask<FluidValue> ProcessAsync(FluidValue input, FilterArguments arguments, LiquidTemplateContext context)
    {
        if (input.ToObjectValue() is IUser user)
        {
            return new StringValue(await _displayNameProvider.GetAsync(user));
        }

        return StringValue.Empty;
    }
}
