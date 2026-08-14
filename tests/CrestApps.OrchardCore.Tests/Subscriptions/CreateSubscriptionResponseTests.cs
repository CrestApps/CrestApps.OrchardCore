using CrestApps.OrchardCore.Stripe.Core.Models;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

public class CreateSubscriptionResponseTests
{
    [Fact]
    public void RequiresAction_IsTrue_WhenIncompleteAndClientSecretPresent()
    {
        var response = new CreateSubscriptionResponse
        {
            Id = "sub_1",
            Status = "incomplete",
            ClientSecret = "pi_secret_123",
        };

        Assert.True(response.RequiresAction);
    }

    [Fact]
    public void RequiresAction_IsCaseInsensitiveOnStatus()
    {
        var response = new CreateSubscriptionResponse
        {
            Status = "Incomplete",
            ClientSecret = "pi_secret_123",
        };

        Assert.True(response.RequiresAction);
    }

    [Fact]
    public void RequiresAction_IsFalse_WhenIncompleteButNoClientSecret()
    {
        var response = new CreateSubscriptionResponse
        {
            Status = "incomplete",
            ClientSecret = null,
        };

        Assert.False(response.RequiresAction);
    }

    [Theory]
    [InlineData("active")]
    [InlineData("trialing")]
    [InlineData("past_due")]
    [InlineData("canceled")]
    public void RequiresAction_IsFalse_WhenSubscriptionIsNotIncomplete(string status)
    {
        var response = new CreateSubscriptionResponse
        {
            Status = status,
            ClientSecret = "pi_secret_123",
        };

        Assert.False(response.RequiresAction);
    }

    [Fact]
    public void RequiresAction_IsFalse_WhenStatusIsNull()
    {
        var response = new CreateSubscriptionResponse
        {
            Status = null,
            ClientSecret = "pi_secret_123",
        };

        Assert.False(response.RequiresAction);
    }
}
