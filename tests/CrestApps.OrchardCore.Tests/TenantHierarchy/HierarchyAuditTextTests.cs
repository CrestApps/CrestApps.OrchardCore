using CrestApps.OrchardCore.TenantHierarchy.Core.Services;
using CrestApps.OrchardCore.TenantHierarchy.Models;
using CrestApps.OrchardCore.TenantHierarchy.Services;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Tests.TenantHierarchy;

public sealed class HierarchyAuditTextTests
{
    private static readonly HierarchyLabels _labels = new()
    {
        Parent = "Practice",
        Child = "Client",
        Children = "Clients",
    };

    private static readonly IHtmlLocalizer _localizer = new FormattingLocalizer();

    [Theory]
    [InlineData(DelegatedSessionRules.ChildSignOut, "The user left the client")]
    [InlineData(DelegatedSessionRules.SignedOutEverywhere, "The user signed out of every client")]
    [InlineData(DelegatedSessionRules.GrantRemoved, "The user's access was removed")]
    [InlineData(DelegatedSessionRules.IdleTimeout, "Ended after a period without activity")]
    [InlineData("SomethingNew", "SomethingNew")]
    public void DescribeDetails_OfAnEndedSession_PutsTheReasonInWords(string reason, string expected)
    {
        // Arrange
        var data = new HierarchyAuditEvent { Name = HierarchyAuditEventNames.SessionEnded, Details = reason };

        // Act
        var text = HierarchyAuditText.DescribeDetails(_localizer, data, _labels);

        // Assert
        Assert.Equal(expected, text);
    }

    [Fact]
    public void DescribeDetails_OfAnEntry_NamesTheRoles()
    {
        // Arrange
        var data = new HierarchyAuditEvent { Name = HierarchyAuditEventNames.Entered, Details = "Administrator, Editor" };

        // Act
        var text = HierarchyAuditText.DescribeDetails(_localizer, data, _labels);

        // Assert
        Assert.Equal("Roles: Administrator, Editor", text);
    }

    [Fact]
    public void DescribeDetails_OfAScheduledRemoval_ShowsTheDateUntilWhichItCanBeRestored()
    {
        // Arrange
        var data = new HierarchyAuditEvent { Name = HierarchyAuditEventNames.RemovalScheduled, Details = "2026-10-16 17:06:24Z" };
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("en-US");

        try
        {
            // Act
            var text = HierarchyAuditText.DescribeDetails(_localizer, data, _labels);

            // Assert
            Assert.Equal("Can be restored until 10/16/2026", text);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void DescribeDetails_OfAnotherEvent_KeepsTheDetails()
    {
        // Arrange
        var data = new HierarchyAuditEvent { Name = HierarchyAuditEventNames.FeatureEnabled, Details = "OrchardCore.Apis.GraphQL" };

        // Act
        var text = HierarchyAuditText.DescribeDetails(_localizer, data, _labels);

        // Assert
        Assert.Equal("OrchardCore.Apis.GraphQL", text);
    }

    [Fact]
    public void DescribeSignIn_PutsTheMethodsInWordsOnce()
    {
        // Act
        var text = HierarchyAuditText.DescribeSignIn(_localizer, ["pwd", "mfa", "hwk", "swk"]);

        // Assert
        Assert.Equal("Signed in with password, two-factor authentication, security key", text);
    }

    [Fact]
    public void DescribeSignIn_WithoutMethods_ReturnsNull()
    {
        // Act + Assert
        Assert.Null(HierarchyAuditText.DescribeSignIn(_localizer, []));
        Assert.Null(HierarchyAuditText.DescribeSignIn(_localizer, null));
    }

    private sealed class FormattingLocalizer : IHtmlLocalizer
    {
        public LocalizedHtmlString this[string name] => new(name, name);

        public LocalizedHtmlString this[string name, params object[] arguments] => new(name, name, false, arguments);

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];

        public LocalizedString GetString(string name) => new(name, name);

        public LocalizedString GetString(string name, params object[] arguments) => new(name, string.Format(name, arguments));
    }
}
