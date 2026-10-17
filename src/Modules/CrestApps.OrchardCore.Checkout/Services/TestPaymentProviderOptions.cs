using Microsoft.Extensions.Hosting;

namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// The configuration of the <b>Test payments</b> feature, read from the <c>CrestApps:Checkout:TestProvider</c>
/// section of the tenant configuration.
/// </summary>
public sealed class TestPaymentProviderOptions
{
    /// <summary>
    /// The configuration section the options are read from.
    /// </summary>
    public const string SectionName = "CrestApps:Checkout:TestProvider";

    /// <summary>
    /// The full configuration key of <see cref="AllowOutsideDevelopment"/>.
    /// </summary>
    public const string AllowOutsideDevelopmentKey = SectionName + ":" + nameof(AllowOutsideDevelopment);

    /// <summary>
    /// Gets or sets a value indicating whether the test payment method is offered outside the Development
    /// environment, for example on a shared staging site or a CI host that runs as Production.
    /// </summary>
    /// <remarks>
    /// The test method confirms every payment without moving money, so anyone who can open a checkout could take
    /// a purchase for free. That is why it is off unless someone turns it on deliberately.
    /// </remarks>
    public bool AllowOutsideDevelopment { get; set; }

    /// <summary>
    /// Gets whether the test payment method may be registered in the given host environment.
    /// </summary>
    /// <param name="hostEnvironment">The host environment.</param>
    public bool IsActive(IHostEnvironment hostEnvironment)
    {
        ArgumentNullException.ThrowIfNull(hostEnvironment);

        return hostEnvironment.IsDevelopment() || AllowOutsideDevelopment;
    }
}
