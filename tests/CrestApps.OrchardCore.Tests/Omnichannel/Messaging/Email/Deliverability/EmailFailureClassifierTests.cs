using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Deliverability;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging.Email.Deliverability;

public sealed class EmailFailureClassifierTests
{
    private const string Rejected = OmnichannelConstants.MessagingErrorCodes.RecipientRejected;

    [Theory]
    [InlineData(Rejected, "The mail server refused the recipient (550): 5.1.1 <ann@example.com>: Recipient address rejected: User unknown", EmailFailureKind.HardBounce)]
    [InlineData(null, "550 5.1.10 RESOLVER.ADR.RecipientNotFound; Recipient not found by SMTP address lookup", EmailFailureKind.HardBounce)]
    [InlineData(null, "550 5.2.1 The email account that you tried to reach is disabled.", EmailFailureKind.HardBounce)]
    [InlineData(Rejected, "550 Requested action not taken: mailbox unavailable", EmailFailureKind.HardBounce)]
    [InlineData(Rejected, "The mail server refused the recipient (550): 5.7.1 Service unavailable; client host blocked using Spamhaus", EmailFailureKind.Blocked)]
    [InlineData(null, "550 5.7.26 Unauthenticated email from contoso.com is not accepted due to domain's DMARC policy", EmailFailureKind.Blocked)]
    [InlineData(null, "550 5.1.8 Sender address rejected: Domain not found", EmailFailureKind.Blocked)]
    [InlineData(null, "554 Message rejected due to spam content", EmailFailureKind.Blocked)]
    [InlineData(null, "The mail server did not accept the email (421): 4.7.0 Try again later, closing connection.", EmailFailureKind.Throttled)]
    [InlineData(null, "450 4.2.1 The user you are trying to contact is receiving mail at a rate that prevents additional messages", EmailFailureKind.Throttled)]
    [InlineData(null, "Status: 429 TooManyRequests", EmailFailureKind.Throttled)]
    [InlineData(null, "The mail server did not accept the email (421): Service not available, closing transmission channel", EmailFailureKind.Throttled)]
    [InlineData(null, "552 Requested mail action aborted: exceeded storage allocation", EmailFailureKind.Temporary)]
    [InlineData(null, "552 Message size exceeds maximum permitted", EmailFailureKind.Rejected)]
    [InlineData(null, "452 4.2.2 The recipient's mailbox is full", EmailFailureKind.Temporary)]
    [InlineData(null, "552 5.2.2 The recipient's mailbox is full", EmailFailureKind.Temporary)]
    [InlineData(null, "550 5.4.4 Host or domain name not found", EmailFailureKind.HardBounce)]
    [InlineData(null, "550 5.0.0 Unrouteable address", EmailFailureKind.HardBounce)]
    [InlineData(null, "550 5.0.0 Rejected: listed on a block list", EmailFailureKind.Blocked)]
    [InlineData(null, "550 5.5.0 Requested action not taken: mailbox unavailable", EmailFailureKind.HardBounce)]
    [InlineData(null, "552 5.3.4 Message size exceeds fixed maximum message size", EmailFailureKind.Rejected)]
    [InlineData(null, "554 5.6.0 Message content rejected", EmailFailureKind.Rejected)]
    [InlineData(null, "550 5.4.5 Daily user sending quota exceeded.", EmailFailureKind.Throttled)]
    [InlineData(null, "452 4.2.2 The email account that you tried to reach is over quota.", EmailFailureKind.Temporary)]
    [InlineData(null, "452 Mailbox full, quota exceeded", EmailFailureKind.Temporary)]
    [InlineData(null, "Daily message quota exceeded", EmailFailureKind.Throttled)]
    [InlineData(null, "No connection could be made because the target machine actively refused it.", EmailFailureKind.Temporary)]
    [InlineData(null, null, EmailFailureKind.Temporary)]
    public void Classify_ReadsTheServersCodesAndWords(string errorCode, string text, EmailFailureKind expected)
    {
        Assert.Equal(expected, EmailFailureClassifier.Classify(errorCode, text));
    }

    [Theory]
    [InlineData("5.1.1", EmailFailureKind.HardBounce)]
    [InlineData("5.7.1", EmailFailureKind.Blocked)]
    [InlineData("4.7.0", EmailFailureKind.Throttled)]
    [InlineData("4.4.7", EmailFailureKind.Temporary)]
    [InlineData("5.4.4", EmailFailureKind.HardBounce)]
    [InlineData("5.0.0", EmailFailureKind.HardBounce)]
    [InlineData("5.5.0", EmailFailureKind.Rejected)]
    [InlineData("5.3.4", EmailFailureKind.Rejected)]
    [InlineData("5.6.0", EmailFailureKind.Rejected)]
    [InlineData("4.2.2", EmailFailureKind.Temporary)]
    public void ClassifyStatus_ReadsAnEnhancedStatusCode(string status, EmailFailureKind expected)
    {
        Assert.Equal(expected, EmailFailureClassifier.ClassifyStatus(status));
    }

    // A full mailbox is about the one recipient; the words "quota" and "exceeded" around it never make it a throttle,
    // which would pause all the address's bulk mail.
    [Theory]
    [InlineData("4.2.2", "452 4.2.2 Mailbox quota exceeded", EmailFailureKind.Temporary)]
    [InlineData("5.5.0", "550 5.5.0 No such user here", EmailFailureKind.HardBounce)]
    [InlineData("5.4.1", "550 5.4.1 Relay access denied", EmailFailureKind.Blocked)]
    public void ClassifyStatus_ReadsTheWordsThatCameWithTheCode(string status, string text, EmailFailureKind expected)
    {
        Assert.Equal(expected, EmailFailureClassifier.ClassifyStatus(status, text));
    }

    [Theory]
    [InlineData("2.0.0")]
    [InlineData("")]
    [InlineData(null)]
    public void ClassifyStatus_IsNothingForASuccessOrNoCode(string status)
    {
        Assert.Null(EmailFailureClassifier.ClassifyStatus(status));
    }
}
