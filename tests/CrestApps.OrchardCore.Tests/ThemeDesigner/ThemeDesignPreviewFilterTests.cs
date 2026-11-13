using CrestApps.OrchardCore.SiteResources.Models;
using CrestApps.OrchardCore.ThemeDesigner.Filters;
using CrestApps.OrchardCore.ThemeDesigner.Models;

namespace CrestApps.OrchardCore.Tests.ThemeDesigner;

public sealed class ThemeDesignPreviewFilterTests
{
    [Fact]
    public void ChangesTheAdmin_ADraftOfFrontEndChangesOnly_IsNotAnnouncedOnAdminPages()
    {
        // Arrange
        var draft = new ThemeDesignDraft
        {
            Templates = [new TemplateDraft { Scope = ThemeDesignScope.Site, Name = "Layout" }],
            Resources = [new SiteResourceDraft { Name = "brand", Resource = new SiteResource { Name = "brand", Target = SiteResourceTarget.Frontend } }],
        };

        // Act & Assert
        Assert.False(ThemeDesignPreviewFilter.ChangesTheAdmin(draft));
    }

    [Fact]
    public void ChangesTheAdmin_AnAdminTemplateOrAResourceTheAdminLoads_IsAnnounced()
    {
        // Arrange
        var withAdminTemplate = new ThemeDesignDraft
        {
            Templates = [new TemplateDraft { Scope = ThemeDesignScope.Admin, Name = "Layout" }],
        };
        var withEverywhereResource = new ThemeDesignDraft
        {
            Resources = [new SiteResourceDraft { Name = "brand", Resource = new SiteResource { Name = "brand", Target = SiteResourceTarget.Everywhere } }],
        };

        // Act & Assert
        Assert.True(ThemeDesignPreviewFilter.ChangesTheAdmin(withAdminTemplate));
        Assert.True(ThemeDesignPreviewFilter.ChangesTheAdmin(withEverywhereResource));
    }
}
