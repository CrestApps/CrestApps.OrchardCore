using System.Net;
using CrestApps.OrchardCore.TenantHierarchy.Core;
using CrestApps.OrchardCore.TenantHierarchy.Core.Guards;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Tests.TenantHierarchy;

public sealed class EgressGuardTests
{
    [Theory]
    [InlineData("127.0.0.1", true)]
    [InlineData("10.1.2.3", true)]
    [InlineData("172.16.0.1", true)]
    [InlineData("172.31.255.255", true)]
    [InlineData("192.168.1.1", true)]
    [InlineData("169.254.169.254", true)]
    [InlineData("100.64.0.1", true)]
    [InlineData("0.0.0.0", true)]
    [InlineData("224.0.0.1", true)]
    [InlineData("::1", true)]
    [InlineData("fe80::1", true)]
    [InlineData("fd00::1", true)]
    [InlineData("::ffff:10.0.0.1", true)]
    [InlineData("64:ff9b::a00:1", true)]
    [InlineData("8.8.8.8", false)]
    [InlineData("172.32.0.1", false)]
    [InlineData("2001:4860:4860::8888", false)]
    [InlineData("64:ff9b::808:808", false)]
    public void IsInternal_ClassifiesTheAddress(string address, bool expected)
    {
        // Act
        var isInternal = EgressAddressClassifier.IsInternal(IPAddress.Parse(address));

        // Assert
        Assert.Equal(expected, isInternal);
    }

    [Theory]
    [InlineData("firma.platform.com", "firma.platform.com", 443, true)]
    [InlineData("FIRMA.platform.com", "firma.platform.com", 443, true)]
    [InlineData("firma.localhost:5320", "firma.localhost", 5320, true)]
    [InlineData("firma.localhost:5320", "firma.localhost", 80, false)]
    [InlineData("*.platform.com", "anything.platform.com", 443, true)]
    [InlineData("firma.platform.com", "other.platform.com", 443, false)]
    [InlineData("", "firma.platform.com", 443, false)]
    public void MatchesHost_ComparesTenantHostsWithTheRequest(string tenantHost, string host, int port, bool expected)
    {
        // Act
        var matches = EgressGuard.MatchesHost(tenantHost, host, port);

        // Assert
        Assert.Equal(expected, matches);
    }

    [Fact]
    public void EnsureAllowed_InAChild_RefusesInternalAddressesAndTenantHosts()
    {
        // Arrange
        var parent = TestShellSettings.Parent("firma");
        var guard = CreateGuard(TestShellSettings.Child("child", parent), new TenantHierarchyEgressOptions(), parent);

        // Act + Assert
        Assert.Throws<HttpRequestException>(() => guard.EnsureAllowed("internal.example.com", 443, [IPAddress.Parse("10.0.0.5")]));
        Assert.Throws<HttpRequestException>(() => guard.EnsureAllowed("firma.platform.com", 443, [IPAddress.Parse("8.8.8.8")]));
        guard.EnsureAllowed("api.example.com", 443, [IPAddress.Parse("8.8.8.8")]);
    }

    [Fact]
    public void EnsureAllowed_AllowListedHost_IsAlwaysSent()
    {
        // Arrange
        var parent = TestShellSettings.Parent("firma");
        var guard = CreateGuard(TestShellSettings.Child("child", parent), new TenantHierarchyEgressOptions { AllowedHosts = ["ollama.local"] }, parent);

        // Act + Assert
        guard.EnsureAllowed("ollama.local", 11434, [IPAddress.Loopback]);
    }

    [Fact]
    public void EnsureAllowed_PrivateNetworksAllowed_RefusesOnlyTenantHosts()
    {
        // Arrange
        var parent = TestShellSettings.Parent("firma");
        var guard = CreateGuard(parent, new TenantHierarchyEgressOptions { BlockPrivateNetworks = false }, parent);

        // Act + Assert
        guard.EnsureAllowed("internal.example.com", 443, [IPAddress.Parse("10.0.0.5")]);
        Assert.Throws<HttpRequestException>(() => guard.EnsureAllowed("firma.platform.com", 443, [IPAddress.Parse("10.0.0.5")]));
    }

    [Fact]
    public void EnsureAllowed_InAnOrdinaryTenantOrWhenOff_IsNotActive()
    {
        // Arrange
        var plain = TestShellSettings.Ordinary("plain");
        var parent = TestShellSettings.Parent("firma");

        // Act
        var ordinary = CreateGuard(plain, new TenantHierarchyEgressOptions(), plain);
        var off = CreateGuard(parent, new TenantHierarchyEgressOptions { Enabled = false }, parent);

        // Assert
        Assert.False(ordinary.IsActive);
        Assert.False(off.IsActive);
        ordinary.EnsureAllowed("internal.example.com", 443, [IPAddress.Loopback]);
        off.EnsureAllowed("internal.example.com", 443, [IPAddress.Loopback]);
    }

    private static EgressGuard CreateGuard(ShellSettings current, TenantHierarchyEgressOptions egress, params ShellSettings[] tenants)
    {
        var shellHost = new Mock<IShellHost>();
        shellHost.Setup(host => host.GetAllSettings()).Returns(tenants);

        return new EgressGuard(
            current,
            shellHost.Object,
            Options.Create(new TenantHierarchyOptions { Egress = egress }),
            NullLogger<EgressGuard>.Instance);
    }
}
