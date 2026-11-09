using CrestApps.OrchardCore.Consent;
using CrestApps.OrchardCore.Consent.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Doubles;

/// <summary>
/// Builds the consent service of a site whose only consent source is the CRM's: the contacts' flags and the opt-outs
/// recorded against an address, as the opt-out resolver reads them.
/// </summary>
public static class ConsentDoubles
{
    /// <summary>
    /// Returns the consent service over <paramref name="optOutResolver"/>.
    /// </summary>
    /// <param name="optOutResolver">The opt-out resolver.</param>
    /// <param name="contentManager">The content manager that loads and updates a contact, or <see langword="null"/>.</param>
    /// <param name="clock">The clock, or <see langword="null"/> for the current time.</param>
    public static IConsentService Over(IContactOptOutResolver optOutResolver, IContentManager contentManager = null, IClock clock = null)
        => new DefaultConsentService([new ContactPreferencesConsentSource(optOutResolver, contentManager ?? Mock.Of<IContentManager>(), clock ?? Clock())]);

    private static IClock Clock()
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(() => DateTime.UtcNow);

        return clock.Object;
    }
}
