using CrestApps.OrchardCore.AI.Core;

namespace CrestApps.OrchardCore.Tests.AI;

/// <summary>
/// <see cref="AIMoneyArguments"/> refuses free text that holds a payment card number (13 to 19 digits that pass the Luhn
/// check, together or in groups), an IBAN (in capitals, passing its mod-97 check) or domestic bank details (bank wording
/// followed right away by six or more digits), and accepts the ordinary references the guard it replaced refused: a fiscal year, a dated
/// wire reference, a check number, the last four digits of an account.
/// </summary>
public sealed class AIMoneyArgumentsTests
{
    [Theory]
    [InlineData("4111111111111111")]
    [InlineData("Paid with 4111-1111-1111-1111")]
    [InlineData("card 4111 1111 1111 1111 exp 12/27")]
    [InlineData("Amex 378282246310005")]
    [InlineData("DE89370400440532013000")]
    [InlineData("IBAN DE89 3704 0044 0532 0130 00 for March")]
    [InlineData("GB82 WEST 1234 5698 7654 32 REF")]
    [InlineData("Routing 021000021 account 123456789")]
    [InlineData("ABA 021000021")]
    [InlineData("acct # 00012345678")]
    [InlineData("Sort code 20-00-00, account number 55779911")]
    [InlineData("From bank account 9876543210")]
    [InlineData("Account #: 4455667788")]
    [InlineData("a/c 12345678")]
    [InlineData("Bank account no. 12345678")]
    [InlineData("Our account number is 1234 5678")]
    public void RefuseAccountNumbers_RefusesACardNumberOrAnIban(string text)
    {
        // Act
        var refusal = AIMoneyArguments.RefuseAccountNumbers("Nothing was recorded", "chk-4471", text);

        // Assert
        Assert.Equal("Nothing was recorded: the text looks like a card or bank account number. Never give one; use a check number or a short transfer reference instead.", refusal);
    }

    [Theory]
    [InlineData("FY25 payment for March")]
    [InlineData("FY25 PAYMENT FOR MARCH")]
    [InlineData("Wire ref 2025-11-20-0001")]
    [InlineData("Check 1042 for invoice INV-2025-0042")]
    [InlineData("4111 1111 1111 1112")]
    [InlineData("Order 123456789012")]
    [InlineData("GB82 WEST 1234 5698 7654 33")]
    [InlineData("de89370400440532013000")]
    [InlineData("Paid from the account ending 4321")]
    [InlineData("Acct 2025 renewal")]
    [InlineData("Account manager approved order 123456")]
    [InlineData("Abandoned cart refund 1234567")]
    [InlineData("Accounting adjustment 2025112001")]
    [InlineData("Paid by bank account transfer for invoice INV-100234")]
    [InlineData("Routing confirmed by phone, ref 20251120")]
    [InlineData("")]
    [InlineData(null)]
    public void RefuseAccountNumbers_AcceptsOrdinaryReferences(string text)
    {
        // Act
        var refusal = AIMoneyArguments.RefuseAccountNumbers("Nothing was recorded", text);

        // Assert
        Assert.Null(refusal);
    }

    [Theory]
    [InlineData("4111111111111111", true)]
    [InlineData("79927398713", true)]
    [InlineData("4111111111111112", false)]
    [InlineData("41111a1111111111", false)]
    [InlineData("", false)]
    public void IsValidLuhn_ChecksTheCheckDigit(string digits, bool valid)
    {
        // Assert
        Assert.Equal(valid, AIMoneyArguments.IsValidLuhn(digits));
    }

    [Theory]
    [InlineData("GB82WEST12345698765432", true)]
    [InlineData("DE89370400440532013000", true)]
    [InlineData("GB82WEST12345698765433", false)]
    [InlineData("gb82WEST12345698765432", false)]
    [InlineData("FY25PAYMENTFORMARCH", false)]
    public void IsValidIban_ChecksMod97(string iban, bool valid)
    {
        // Assert
        Assert.Equal(valid, AIMoneyArguments.IsValidIban(iban));
    }
}
