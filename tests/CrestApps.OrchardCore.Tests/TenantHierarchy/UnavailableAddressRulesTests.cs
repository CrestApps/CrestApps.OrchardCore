using CrestApps.OrchardCore.TenantHierarchy.Core.Guards;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Tests.TenantHierarchy;

public sealed class UnavailableAddressRulesTests
{
    private static readonly ShellSettings _parent = TestShellSettings.Parent("firm");
    private static readonly ShellSettings _child = TestShellSettings.Child("acme", _parent);

    private static readonly ShellSettings[] _tenants =
    [
        TestShellSettings.Default(),
        _parent,
        _child,
        WithHost(TestShellSettings.Ordinary("blog"), "blog.example.com"),
    ];

    [Theory]
    [InlineData("acme.firm.platform.com")]
    [InlineData("ACME.Firm.Platform.com")]
    [InlineData("firm.platform.com")]
    [InlineData("acme.firm.platform.com:8443")]
    public void HostOfAHierarchyTenant_IsAHierarchyAddress(string host)
    {
        // Act
        var result = UnavailableAddressRules.IsHierarchyAddress(host, _tenants);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void AddressUnderAParent_WithNoTenant_IsAHierarchyAddress()
    {
        // Act: a removed child leaves its address under the parent host.
        var result = UnavailableAddressRules.IsHierarchyAddress("removed-client.firm.platform.com", _tenants);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData("platform.com")]
    [InlineData("localhost")]
    [InlineData("blog.example.com")]
    [InlineData("notfirm.platform.com")]
    [InlineData("firm.platform.com.evil.example")]
    [InlineData("")]
    [InlineData(null)]
    public void OtherHosts_AreNotHierarchyAddresses(string host)
    {
        // Act
        var result = UnavailableAddressRules.IsHierarchyAddress(host, _tenants);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void AddressUnderAChild_IsNotAHierarchyAddress()
    {
        // Act: only a parent owns the addresses under its host.
        var result = UnavailableAddressRules.IsHierarchyAddress("deeper.acme.firm.platform.com", [_child]);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void HostWithAPort_MatchesOnlyThatPort()
    {
        // Arrange
        var parent = WithHost(TestShellSettings.Parent("ported"), "ported.localhost:5000");

        // Act + Assert
        Assert.True(UnavailableAddressRules.IsHierarchyAddress("ported.localhost:5000", [parent]));
        Assert.True(UnavailableAddressRules.IsHierarchyAddress("client.ported.localhost:5000", [parent]));
        Assert.False(UnavailableAddressRules.IsHierarchyAddress("ported.localhost:5001", [parent]));
        Assert.False(UnavailableAddressRules.IsHierarchyAddress("client.ported.localhost", [parent]));
    }

    [Fact]
    public void DefaultTenantHost_IsNeverAHierarchyAddress()
    {
        // Arrange: even a Default tenant marked as part of a hierarchy keeps its own address.
        var platform = WithHost(TestShellSettings.Default(), "platform.com");

        // Act
        var result = UnavailableAddressRules.IsHierarchyAddress("platform.com", [platform]);

        // Assert
        Assert.False(result);
    }

    private static ShellSettings WithHost(ShellSettings settings, string host)
    {
        settings.RequestUrlHost = host;

        return settings;
    }
}
