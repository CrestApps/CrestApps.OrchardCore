using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Transactions;
using CrestApps.OrchardCore.Transactions.Core;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Transactions;

/// <summary>
/// The record payment form suggests the common payment methods by their labels and stores their stable keys, so a report
/// groups payments the same whatever the language of the person who recorded them; any other text is kept as typed.
/// </summary>
public sealed class OfflinePaymentMethodTextTests
{
    private readonly TransactionDisplayText _text = new(
        [],
        Options.Create(new TransactionSourceOptions()),
        new PassThroughStringLocalizer<TransactionDisplayText>(),
        new SourceTextLocalizerFactory());

    [Theory]
    [InlineData("Bank transfer", TransactionsConstants.OfflinePaymentMethods.BankTransfer)]
    [InlineData("  bank TRANSFER ", TransactionsConstants.OfflinePaymentMethods.BankTransfer)]
    [InlineData("bank-transfer", TransactionsConstants.OfflinePaymentMethods.BankTransfer)]
    [InlineData("Cash", TransactionsConstants.OfflinePaymentMethods.Cash)]
    [InlineData("Check", TransactionsConstants.OfflinePaymentMethods.Check)]
    [InlineData("Card terminal", TransactionsConstants.OfflinePaymentMethods.CardTerminal)]
    [InlineData("Other", TransactionsConstants.OfflinePaymentMethods.Other)]
    [InlineData(" Western Union ", "Western Union")]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public void Normalize_StoresTheKeyOfACommonMethod_AndKeepsOtherText(string entered, string expected)
        => Assert.Equal(expected, _text.NormalizeOfflinePaymentMethod(entered));

    [Fact]
    public void Name_ShowsTheLabelOfAKey_AndTheTextOfAnythingElse()
    {
        Assert.Equal("Bank transfer", _text.OfflinePaymentMethodName(TransactionsConstants.OfflinePaymentMethods.BankTransfer));
        Assert.Equal("Western Union", _text.OfflinePaymentMethodName("Western Union"));
        Assert.All(TransactionsConstants.OfflinePaymentMethods.All, key => Assert.Equal(key, _text.NormalizeOfflinePaymentMethod(_text.OfflinePaymentMethodName(key))));
    }
}
