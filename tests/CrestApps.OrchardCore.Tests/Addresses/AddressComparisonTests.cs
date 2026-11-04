using CrestApps.OrchardCore.Addresses;
using CrestApps.OrchardCore.Addresses.Models;

namespace CrestApps.OrchardCore.Tests.Addresses;

/// <summary>
/// Two addresses are the same place when their street, city, region, postal code and country match, whatever the case
/// and the spaces around them, and whoever they are addressed to.
/// </summary>
public sealed class AddressComparisonTests
{
    [Fact]
    public void TheSamePlace_IsTheSameAddress_WhateverTheCaseSpacesAndRecipient()
    {
        var left = new Address { Name = "Ann", AddressLine1 = "400 Pine St", City = "Seattle", Region = "WA", PostalCode = "98101", Country = "US" };
        var right = new Address { Name = "Bob", AddressLine1 = " 400 PINE ST ", City = "seattle", Region = "wa", PostalCode = "98101", Country = "us" };

        Assert.True(AddressComparison.IsSameAddress(left, right));
    }

    [Fact]
    public void AnotherStreetOrAMissingAddress_IsNotTheSame()
    {
        var left = new Address { AddressLine1 = "400 Pine St", City = "Seattle", Country = "US" };

        Assert.False(AddressComparison.IsSameAddress(left, new Address { AddressLine1 = "401 Pine St", City = "Seattle", Country = "US" }));
        Assert.False(AddressComparison.IsSameAddress(left, null));
        Assert.False(AddressComparison.IsSameAddress(null, null));
    }
}
