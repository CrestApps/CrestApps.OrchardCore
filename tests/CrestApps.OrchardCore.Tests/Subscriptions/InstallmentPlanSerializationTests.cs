using System.Text.Json;
using CrestApps.OrchardCore.Subscriptions.Models;
using OrchardCore.Json;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

/// <summary>
/// A plan is stored as a document and read back on every request, so a round trip must give back exactly what was
/// saved. The plan's computed properties return payments it already holds; reading their serialized copies back into
/// those same payments would add to their lists on every save, until one plan grew to hundreds of megabytes.
/// </summary>
public sealed class InstallmentPlanSerializationTests
{
    public static TheoryData<string> SerializerOptions => ["JOptions.Default", "DocumentJsonSerializerOptions"];

    [Theory]
    [MemberData(nameof(SerializerOptions))]
    public void RoundTrip_KeepsEachPaymentsCheckoutSessionsAsSaved(string optionsName)
    {
        // Arrange
        var options = optionsName == "JOptions.Default"
            ? JOptions.Default
            : new DocumentJsonSerializerOptions().SerializerOptions;

        var plan = new InstallmentPlan
        {
            ItemId = "plan-1",
            Payments =
            {
                new InstallmentPlanPayment { Number = 0, Amount = 100m, Status = InstallmentPaymentStatus.Paid },
                new InstallmentPlanPayment { Number = 1, Amount = 100m, Status = InstallmentPaymentStatus.Scheduled, CheckoutSessionIds = { "session-1" } },
                new InstallmentPlanPayment { Number = 2, Amount = 100m, Status = InstallmentPaymentStatus.Scheduled },
            },
        };

        // Act: saved and loaded several times, as a plan is over its life.
        for (var i = 0; i < 4; i++)
        {
            plan = JsonSerializer.Deserialize<InstallmentPlan>(JsonSerializer.Serialize(plan, options), options);
        }

        // Assert
        Assert.Equal(3, plan.Payments.Count);
        Assert.Equal(["session-1"], plan.Payments[1].CheckoutSessionIds);
        Assert.Empty(plan.Payments[0].CheckoutSessionIds);
        Assert.Same(plan.Payments[1], plan.NextPayment);
    }

    [Fact]
    public void Serialize_LeavesOutWhatIsComputedFromThePayments()
    {
        // Arrange
        var plan = new InstallmentPlan
        {
            Payments = { new InstallmentPlanPayment { Number = 0, Amount = 50m } },
        };

        // Act
        var json = JsonSerializer.SerializeToNode(plan, JOptions.Default).AsObject();

        // Assert
        Assert.False(json.ContainsKey(nameof(InstallmentPlan.NextPayment)));
        Assert.False(json.ContainsKey(nameof(InstallmentPlan.DownPayment)));
        Assert.False(json.ContainsKey(nameof(InstallmentPlan.AmountPaid)));
        Assert.False(json.ContainsKey(nameof(InstallmentPlan.AmountOutstanding)));
    }
}
