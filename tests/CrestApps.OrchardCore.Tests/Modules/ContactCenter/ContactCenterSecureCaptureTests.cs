using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Covers the hosted secure-data-capture boundary: the tokenization sink that masks and never retains raw values,
/// the one-time access token hashing, and the capture service that starts, resolves, completes, cancels, and
/// expires a capture while ensuring the raw sensitive value only ever reaches the tokenization sink.
/// </summary>
public sealed class ContactCenterSecureCaptureTests
{
    [Theory]
    [InlineData("4111111111111111")]
    [InlineData("4111 1111 1111 1111")]
    [InlineData("5555555555554444")]
    public async Task Tokenize_WithValidCard_MasksToLastFourAndReturnsToken(string cardNumber)
    {
        // Arrange
        var sink = new MaskingSecureCaptureTokenSink();

        // Act
        var result = await sink.TokenizeAsync(SecureCaptureField.CreditCardNumber, cardNumber, "idem-test", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.StartsWith("tok_", result.Token);
        var digits = cardNumber.Replace(" ", string.Empty);
        Assert.EndsWith(digits.Substring(digits.Length - 4), result.MaskedValue);
        Assert.DoesNotContain(digits.Substring(0, 8), result.MaskedValue);
    }

    [Theory]
    [InlineData("1234567890123456")]
    [InlineData("411")]
    [InlineData("notacard")]
    public async Task Tokenize_WithInvalidCard_Fails(string cardNumber)
    {
        // Arrange
        var sink = new MaskingSecureCaptureTokenSink();

        // Act
        var result = await sink.TokenizeAsync(SecureCaptureField.CreditCardNumber, cardNumber, "idem-test", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Null(result.Token);
    }

    [Fact]
    public async Task Tokenize_WithSecurityCode_ReturnsNonRetainableWithNothingStored()
    {
        // Arrange
        var sink = new MaskingSecureCaptureTokenSink();

        // Act
        var result = await sink.TokenizeAsync(SecureCaptureField.CardSecurityCode, "123", "idem-test", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.False(result.IsRetainable);
        Assert.Null(result.Token);
        Assert.Null(result.MaskedValue);
    }

    [Theory]
    [InlineData("12")]
    [InlineData("12345")]
    public async Task Tokenize_WithInvalidSecurityCode_Fails(string code)
    {
        // Arrange
        var sink = new MaskingSecureCaptureTokenSink();

        // Act
        var result = await sink.TokenizeAsync(SecureCaptureField.CardSecurityCode, code, "idem-test", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Tokenize_WithBlankValue_Fails()
    {
        // Arrange
        var sink = new MaskingSecureCaptureTokenSink();

        // Act
        var result = await sink.TokenizeAsync(SecureCaptureField.NationalId, "   ", "idem-test", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
    }

    [Fact]
    public void AccessToken_Create_ReturnsDistinctRawAndDeterministicHash()
    {
        // Act
        var (rawToken, hash) = SecureCaptureAccessToken.Create();

        // Assert
        Assert.NotEqual(rawToken, hash);
        Assert.Equal(hash, SecureCaptureAccessToken.Hash(rawToken));
        Assert.Equal(hash, hash.ToLowerInvariant());
    }

    [Fact]
    public void AccessToken_Hash_WithEmpty_ReturnsNull()
    {
        // Assert
        Assert.Null(SecureCaptureAccessToken.Hash(null));
        Assert.Null(SecureCaptureAccessToken.Hash(string.Empty));
    }

    [Fact]
    public async Task Begin_WhenDisabled_FailsWithoutCreatingSession()
    {
        // Arrange
        var harness = new Harness(new SecureCaptureSettings { Enabled = false });

        // Act
        var result = await harness.Service.BeginAsync(
            "int1",
            "user1",
            CreatePrincipal(),
            [SecureCaptureField.CreditCardNumber],
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        harness.SessionManager.Verify(m => m.CreateAsync(It.IsAny<SecureCaptureSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Begin_WithNoFields_Fails()
    {
        // Arrange
        var harness = new Harness();

        // Act
        var result = await harness.Service.BeginAsync(
            "int1",
            "user1",
            CreatePrincipal(),
            [],
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Begin_WhenInteractionMissing_Fails()
    {
        // Arrange
        var harness = new Harness();
        harness.InteractionManager
            .Setup(m => m.FindByIdAsync("int1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Interaction)null);

        // Act
        var result = await harness.Service.BeginAsync(
            "int1",
            "user1",
            CreatePrincipal(),
            [SecureCaptureField.CreditCardNumber],
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Begin_WhenOwnershipDenied_FailsWithoutCreatingSession()
    {
        // Arrange
        var harness = new Harness(authorization: FakeCallControlAuthorizationService.Denying());
        harness.WithInteraction();

        // Act
        var result = await harness.Service.BeginAsync(
            "int1",
            "user1",
            CreatePrincipal(),
            [SecureCaptureField.CreditCardNumber],
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        harness.SessionManager.Verify(m => m.CreateAsync(It.IsAny<SecureCaptureSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Begin_WhenAuthorized_MintsTokenEngagesPauseAndPublishes()
    {
        // Arrange
        var harness = new Harness();
        harness.WithInteraction();
        harness.RecordingService
            .Setup(r => r.PauseAsync("int1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(RecordingCommandResult.Success());

        // Act
        var result = await harness.Service.BeginAsync(
            "int1",
            "user1",
            CreatePrincipal(),
            [SecureCaptureField.CreditCardNumber, SecureCaptureField.CardSecurityCode],
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.False(string.IsNullOrEmpty(result.AccessToken));
        Assert.Equal(SecureCaptureState.Collecting, harness.LastSession.State);
        Assert.True(harness.LastSession.EngagedRecordingPause);
        Assert.Equal(SecureCaptureAccessToken.Hash(result.AccessToken), harness.LastSession.AccessTokenHash);
        harness.RecordingService.Verify(r => r.PauseAsync("int1", It.IsAny<CancellationToken>()), Times.Once);
        harness.Publisher.Verify(p => p.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetForCustomer_WithExpiredSession_ReturnsNull()
    {
        // Arrange
        var harness = new Harness();
        var session = new SecureCaptureSession
        {
            State = SecureCaptureState.Collecting,
            ExpiresUtc = harness.Now.AddSeconds(-1),
        };
        harness.SessionManager
            .Setup(m => m.FindByAccessTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        // Act
        var resolved = await harness.Service.GetForCustomerAsync("raw-token", TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(resolved);
    }

    [Fact]
    public async Task GetForCustomer_WithSettledSession_ReturnsNull()
    {
        // Arrange
        var harness = new Harness();
        var session = new SecureCaptureSession
        {
            State = SecureCaptureState.Completed,
            ExpiresUtc = harness.Now.AddMinutes(5),
        };
        harness.SessionManager
            .Setup(m => m.FindByAccessTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        // Act
        var resolved = await harness.Service.GetForCustomerAsync("raw-token", TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(resolved);
    }

    [Fact]
    public async Task GetForCustomer_WithActiveSession_ReturnsSession()
    {
        // Arrange
        var harness = new Harness();
        var session = new SecureCaptureSession
        {
            State = SecureCaptureState.Collecting,
            ExpiresUtc = harness.Now.AddMinutes(5),
        };
        harness.SessionManager
            .Setup(m => m.FindByAccessTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        // Act
        var resolved = await harness.Service.GetForCustomerAsync("raw-token", TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(session, resolved);
    }

    [Fact]
    public async Task Submit_WhenValueMissing_FailsWithoutCompleting()
    {
        // Arrange
        var harness = new Harness();
        var session = harness.CollectingSession([SecureCaptureField.CreditCardNumber, SecureCaptureField.CardSecurityCode], engagedPause: true);
        harness.SessionManager
            .Setup(m => m.FindByAccessTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var values = new Dictionary<SecureCaptureField, string>
        {
            [SecureCaptureField.CreditCardNumber] = "4111111111111111",
        };

        // Act
        var result = await harness.Service.SubmitAsync("raw-token", values, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(SecureCaptureState.Collecting, session.State);
        harness.RecordingService.Verify(r => r.ResumeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Submit_WhenValueInvalid_FailsWholeSubmit()
    {
        // Arrange
        var harness = new Harness();
        var session = harness.CollectingSession([SecureCaptureField.CreditCardNumber], engagedPause: false);
        harness.SessionManager
            .Setup(m => m.FindByAccessTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var values = new Dictionary<SecureCaptureField, string>
        {
            [SecureCaptureField.CreditCardNumber] = "1234567890123456",
        };

        // Act
        var result = await harness.Service.SubmitAsync("raw-token", values, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(SecureCaptureState.Collecting, session.State);
        Assert.Empty(session.TokenReferences);
    }

    [Fact]
    public async Task Submit_WithValidValues_CompletesResumesAndPublishes()
    {
        // Arrange
        var harness = new Harness();
        var session = harness.CollectingSession([SecureCaptureField.CreditCardNumber, SecureCaptureField.CardSecurityCode], engagedPause: true);
        harness.SessionManager
            .Setup(m => m.FindByAccessTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        harness.RecordingService
            .Setup(r => r.ResumeAsync("int1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(RecordingCommandResult.Success());

        var values = new Dictionary<SecureCaptureField, string>
        {
            [SecureCaptureField.CreditCardNumber] = "4111111111111111",
            [SecureCaptureField.CardSecurityCode] = "123",
        };

        // Act
        var result = await harness.Service.SubmitAsync("raw-token", values, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(SecureCaptureState.Completed, session.State);
        Assert.Single(session.TokenReferences);
        Assert.True(session.TokenReferences.ContainsKey(SecureCaptureField.CreditCardNumber));
        Assert.False(session.TokenReferences.ContainsKey(SecureCaptureField.CardSecurityCode));
        Assert.False(session.MaskedValues.ContainsKey(SecureCaptureField.CardSecurityCode));
        Assert.EndsWith("1111", session.MaskedValues[SecureCaptureField.CreditCardNumber]);
        harness.RecordingService.Verify(r => r.ResumeAsync("int1", It.IsAny<CancellationToken>()), Times.Once);
        harness.Publisher.Verify(p => p.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Cancel_WhenNotCollecting_Fails()
    {
        // Arrange
        var harness = new Harness();
        var session = harness.CollectingSession([SecureCaptureField.CreditCardNumber], engagedPause: false);
        session.State = SecureCaptureState.Completed;
        harness.SessionManager
            .Setup(m => m.FindByIdAsync("sess1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        // Act
        var result = await harness.Service.CancelAsync("sess1", "user1", CreatePrincipal(), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Cancel_WhenOwnershipDenied_Fails()
    {
        // Arrange
        var harness = new Harness(authorization: FakeCallControlAuthorizationService.Denying());
        harness.WithInteraction();
        var session = harness.CollectingSession([SecureCaptureField.CreditCardNumber], engagedPause: false);
        harness.SessionManager
            .Setup(m => m.FindByIdAsync("sess1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        // Act
        var result = await harness.Service.CancelAsync("sess1", "user1", CreatePrincipal(), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(SecureCaptureState.Collecting, session.State);
    }

    [Fact]
    public async Task Cancel_WhenAuthorized_MarksCancelledAndResumes()
    {
        // Arrange
        var harness = new Harness();
        harness.WithInteraction();
        var session = harness.CollectingSession([SecureCaptureField.CreditCardNumber], engagedPause: true);
        harness.SessionManager
            .Setup(m => m.FindByIdAsync("sess1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        harness.RecordingService
            .Setup(r => r.ResumeAsync("int1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(RecordingCommandResult.Success());

        // Act
        var result = await harness.Service.CancelAsync("sess1", "user1", CreatePrincipal(), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(SecureCaptureState.Cancelled, session.State);
        harness.RecordingService.Verify(r => r.ResumeAsync("int1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExpireDue_ExpiresCollectingAndSkipsSettled()
    {
        // Arrange
        var harness = new Harness();
        var collecting = harness.CollectingSession([SecureCaptureField.CreditCardNumber], engagedPause: true);
        var settled = harness.CollectingSession([SecureCaptureField.CreditCardNumber], engagedPause: true);
        settled.State = SecureCaptureState.Completed;
        harness.SessionManager
            .Setup(m => m.ListExpiredAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([collecting, settled]);
        harness.RecordingService
            .Setup(r => r.ResumeAsync("int1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(RecordingCommandResult.Success());

        // Act
        var expired = await harness.Service.ExpireDueAsync(50, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, expired);
        Assert.Equal(SecureCaptureState.Expired, collecting.State);
        Assert.Equal(SecureCaptureState.Completed, settled.State);
        harness.RecordingService.Verify(r => r.ResumeAsync("int1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Begin_WhenCaptureAlreadyActive_FailsWithoutCreatingSession()
    {
        // Arrange
        var harness = new Harness();
        harness.WithInteraction();
        harness.SessionManager
            .Setup(m => m.FindActiveByInteractionAsync("int1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SecureCaptureSession { ItemId = "existing", State = SecureCaptureState.Collecting });

        // Act
        var result = await harness.Service.BeginAsync(
            "int1",
            "user1",
            CreatePrincipal(),
            [SecureCaptureField.CreditCardNumber],
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        harness.SessionManager.Verify(m => m.CreateAsync(It.IsAny<SecureCaptureSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Begin_WithUndefinedField_Fails()
    {
        // Arrange
        var harness = new Harness();

        // Act
        var result = await harness.Service.BeginAsync(
            "int1",
            "user1",
            CreatePrincipal(),
            [(SecureCaptureField)999],
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        harness.SessionManager.Verify(m => m.CreateAsync(It.IsAny<SecureCaptureSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Begin_WhenPersistenceFails_ResumesRecordingAndRethrows()
    {
        // Arrange
        var harness = new Harness();
        harness.WithInteraction();
        harness.RecordingService
            .Setup(r => r.PauseAsync("int1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(RecordingCommandResult.Success());
        harness.RecordingService
            .Setup(r => r.ResumeAsync("int1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(RecordingCommandResult.Success());
        harness.SessionManager
            .Setup(m => m.CreateAsync(It.IsAny<SecureCaptureSession>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("write failed"));

        // Act
        var act = async () => await harness.Service.BeginAsync(
            "int1",
            "user1",
            CreatePrincipal(),
            [SecureCaptureField.CreditCardNumber],
            TestContext.Current.CancellationToken);

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(act);
        harness.RecordingService.Verify(r => r.ResumeAsync("int1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RecoverRecordingResumes_ResumesPendingAndPersists()
    {
        // Arrange
        var harness = new Harness();
        var pending = harness.CollectingSession([SecureCaptureField.CreditCardNumber], engagedPause: true);
        pending.State = SecureCaptureState.Completed;
        pending.RecordingResumed = false;
        harness.SessionManager
            .Setup(m => m.ListPendingRecordingResumeAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([pending]);
        harness.RecordingService
            .Setup(r => r.ResumeAsync("int1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(RecordingCommandResult.Success());

        // Act
        var recovered = await harness.Service.RecoverRecordingResumesAsync(50, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, recovered);
        Assert.True(pending.RecordingResumed);
        harness.SessionManager.Verify(m => m.UpdateAsync(pending, It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Submit_WhenResumeFails_CompletesButLeavesRecoverable()
    {
        // Arrange
        var harness = new Harness();
        var session = harness.CollectingSession([SecureCaptureField.CreditCardNumber], engagedPause: true);
        harness.SessionManager
            .Setup(m => m.FindByAccessTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        harness.RecordingService
            .Setup(r => r.ResumeAsync("int1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(RecordingCommandResult.Failure("provider offline"));

        var values = new Dictionary<SecureCaptureField, string>
        {
            [SecureCaptureField.CreditCardNumber] = "4111111111111111",
        };

        // Act
        var result = await harness.Service.SubmitAsync("raw-token", values, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(SecureCaptureState.Completed, session.State);
        Assert.False(session.RecordingResumed);
    }

    [Fact]
    public async Task UnconfiguredTokenSink_AlwaysFailsClosed()
    {
        // Arrange
        var sink = new UnconfiguredSecureCaptureTokenSink();

        // Act
        var result = await sink.TokenizeAsync(SecureCaptureField.CreditCardNumber, "4111111111111111", "idem-test", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Null(result.Token);
    }

    [Fact]
    public async Task Submit_PassesStablePerFieldIdempotencyKeyToSink()
    {
        // Arrange
        var recordingSink = new RecordingTokenSink();
        var harness = new Harness(tokenSink: recordingSink);
        var session = harness.CollectingSession([SecureCaptureField.CreditCardNumber], engagedPause: false);
        harness.SessionManager
            .Setup(m => m.FindByAccessTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var values = new Dictionary<SecureCaptureField, string>
        {
            [SecureCaptureField.CreditCardNumber] = "4111111111111111",
        };

        // Act
        var result = await harness.Service.SubmitAsync("raw-token", values, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal($"{session.ItemId}:{SecureCaptureField.CreditCardNumber}", Assert.Single(recordingSink.Keys));
    }

    [Fact]
    public async Task IdempotentSinkContract_SameKeyAndValue_ReturnsOriginalTokenWithoutMintingASecond()
    {
        // Arrange
        var sink = new IdempotentContractSink();

        // Act
        var first = await sink.TokenizeAsync(SecureCaptureField.CreditCardNumber, "4111111111111111", "sess1:CreditCardNumber", TestContext.Current.CancellationToken);
        var second = await sink.TokenizeAsync(SecureCaptureField.CreditCardNumber, "4111111111111111", "sess1:CreditCardNumber", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.Equal(first.Token, second.Token);
        Assert.Equal(1, sink.MintCount);
    }

    [Fact]
    public async Task IdempotentSinkContract_SameKeyDifferentValue_FailsSafelyWithoutTokenizing()
    {
        // Arrange
        var sink = new IdempotentContractSink();

        // Act
        var first = await sink.TokenizeAsync(SecureCaptureField.CreditCardNumber, "4111111111111111", "sess1:CreditCardNumber", TestContext.Current.CancellationToken);
        var conflicting = await sink.TokenizeAsync(SecureCaptureField.CreditCardNumber, "5555555555554444", "sess1:CreditCardNumber", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(first.Succeeded);
        Assert.False(conflicting.Succeeded);
        Assert.Null(conflicting.Token);
        Assert.Equal(1, sink.MintCount);
    }

    [Fact]
    public async Task IdempotentSinkContract_NonRetainableField_IsExemptFromValueComparison()
    {
        // Arrange
        var sink = new IdempotentContractSink();

        // Act
        var first = await sink.TokenizeAsync(SecureCaptureField.CardSecurityCode, "123", "sess1:CardSecurityCode", TestContext.Current.CancellationToken);
        var differentValue = await sink.TokenizeAsync(SecureCaptureField.CardSecurityCode, "456", "sess1:CardSecurityCode", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(first.Succeeded);
        Assert.False(first.IsRetainable);
        Assert.True(differentValue.Succeeded);
        Assert.False(differentValue.IsRetainable);
        Assert.Equal(0, sink.MintCount);
    }

    private static ClaimsPrincipal CreatePrincipal()
        => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user1")], "test"));

    private sealed class Harness
    {
        public Harness(
            SecureCaptureSettings settings = null,
            FakeCallControlAuthorizationService authorization = null,
            ISecureCaptureTokenSink tokenSink = null)
        {
            Now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            Clock = new StubClock(Now);
            InteractionManager = new Mock<IInteractionManager>();
            SessionManager = new Mock<ISecureCaptureSessionManager>();
            RecordingService = new Mock<IContactCenterRecordingService>();
            Publisher = new Mock<IContactCenterEventPublisher>();

            SessionManager
                .Setup(m => m.NewAsync(It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    LastSession = new SecureCaptureSession { ItemId = "sess1" };

                    return LastSession;
                });

            Service = new SecureCaptureService(
                InteractionManager.Object,
                SessionManager.Object,
                authorization ?? new FakeCallControlAuthorizationService(),
                tokenSink ?? new MaskingSecureCaptureTokenSink(),
                RecordingService.Object,
                Publisher.Object,
                SiteServiceFactory.Create(settings ?? new SecureCaptureSettings { Enabled = true }),
                Clock,
                NullLogger<SecureCaptureService>.Instance);
        }

        public DateTime Now { get; }

        public StubClock Clock { get; }

        public Mock<IInteractionManager> InteractionManager { get; }

        public Mock<ISecureCaptureSessionManager> SessionManager { get; }

        public Mock<IContactCenterRecordingService> RecordingService { get; }

        public Mock<IContactCenterEventPublisher> Publisher { get; }

        public SecureCaptureService Service { get; }

        public SecureCaptureSession LastSession { get; private set; }

        public void WithInteraction()
        {
            InteractionManager
                .Setup(m => m.FindByIdAsync("int1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Interaction
                {
                    ItemId = "int1",
                    ProviderName = "p1",
                    ProviderInteractionId = "call-1",
                    AgentId = "agent1",
                });
        }

        public SecureCaptureSession CollectingSession(SecureCaptureField[] fields, bool engagedPause)
        {
            return new SecureCaptureSession
            {
                ItemId = "sess1",
                InteractionId = "int1",
                AgentId = "agent1",
                RequestedFields = fields,
                State = SecureCaptureState.Collecting,
                EngagedRecordingPause = engagedPause,
                ExpiresUtc = Now.AddMinutes(5),
            };
        }
    }

    private sealed class RecordingTokenSink : ISecureCaptureTokenSink
    {
        public List<string> Keys { get; } = [];

        public Task<SecureCaptureTokenResult> TokenizeAsync(
            SecureCaptureField field,
            string rawValue,
            string idempotencyKey,
            CancellationToken cancellationToken = default)
        {
            Keys.Add(idempotencyKey);

            return Task.FromResult(SecureCaptureTokenResult.Success("tok_test", "•••• 1111"));
        }
    }

    private sealed class IdempotentContractSink : ISecureCaptureTokenSink
    {
        private readonly Dictionary<string, (string Value, SecureCaptureTokenResult Result)> _byKey = [];

        public int MintCount { get; private set; }

        public Task<SecureCaptureTokenResult> TokenizeAsync(
            SecureCaptureField field,
            string rawValue,
            string idempotencyKey,
            CancellationToken cancellationToken = default)
        {
            // Sensitive authentication data (a card security code) is never retained in any form, so it is exempt
            // from value-comparing idempotency: validate it independently on every call and store nothing.
            if (field == SecureCaptureField.CardSecurityCode)
            {
                return Task.FromResult(SecureCaptureTokenResult.SuccessNonRetainable());
            }

            if (_byKey.TryGetValue(idempotencyKey, out var existing))
            {
                if (!string.Equals(existing.Value, rawValue, StringComparison.Ordinal))
                {
                    return Task.FromResult(SecureCaptureTokenResult.Failure(
                        "The idempotency key was already used with a different value."));
                }

                return Task.FromResult(existing.Result);
            }

            MintCount++;
            var result = SecureCaptureTokenResult.Success($"tok_{MintCount}", "•••• 1111");
            _byKey[idempotencyKey] = (rawValue, result);

            return Task.FromResult(result);
        }
    }
}
