using System.Threading.Tasks;
using CrestApps.OrchardCore.Commerce.FinancialDocuments;
using CrestApps.OrchardCore.Commerce.Services;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Commerce;

public sealed class ReceiptsOnlyFinancialDocumentPolicyTests
{
    [Theory]
    [InlineData(FinancialDocumentReason.PaymentSettled)]
    [InlineData(FinancialDocumentReason.PartiallyPaid)]
    [InlineData(FinancialDocumentReason.Refunded)]
    [InlineData(FinancialDocumentReason.ChargedBack)]
    [InlineData(FinancialDocumentReason.WrittenOff)]
    public async Task Evaluate_AlwaysIssuesReceiptOnly_NeverPersistsOrNumbers(FinancialDocumentReason reason)
    {
        // Arrange
        var policy = new ReceiptsOnlyFinancialDocumentPolicy();
        var context = new FinancialDocumentContext("Order", "order-1", "USD", reason);

        // Act
        var result = await policy.EvaluateAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal([FinancialDocumentKind.Receipt], result.Documents);
        Assert.False(result.PersistImmutableCopy);
        Assert.False(result.RequiresFormalNumber);
    }

    [Fact]
    public async Task Evaluate_Throws_WhenContextIsNull()
    {
        // Arrange
        var policy = new ReceiptsOnlyFinancialDocumentPolicy();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => policy.EvaluateAsync(null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void PolicyResult_DefaultsDocumentsToEmpty_WhenNullPassed()
    {
        // Arrange & Act
        var result = new FinancialDocumentPolicyResult(null, persistImmutableCopy: false, requiresFormalNumber: false);

        // Assert
        Assert.NotNull(result.Documents);
        Assert.Empty(result.Documents);
    }
}
