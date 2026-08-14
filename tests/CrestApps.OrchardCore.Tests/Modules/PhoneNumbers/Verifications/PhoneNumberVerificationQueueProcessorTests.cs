using CrestApps.OrchardCore.ContentFields.Fields;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.PhoneNumbers;
using CrestApps.OrchardCore.PhoneNumbers.Core.Models;
using CrestApps.OrchardCore.PhoneNumbers.Core.Services;
using CrestApps.OrchardCore.PhoneNumbers.Verifications.Handlers;
using CrestApps.OrchardCore.PhoneNumbers.Verifications.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore;
using OrchardCore.ContentFields.Fields;
using OrchardCore.ContentManagement;
using OrchardCore.Flows.Models;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.PhoneNumbers.Verifications;

public sealed class PhoneNumberVerificationQueueProcessorTests
{
    private static readonly DateTime _now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ProcessAsync_VerifiesRecordsSequentiallyAndDelaysBetweenProviderRequests()
    {
        // Arrange
        var manager = new FakePhoneNumberVerificationManager(phoneNumber => CreateResult(phoneNumber, PhoneNumberVerificationStatus.Verified));
        var delayer = new FakePhoneNumberVerificationRequestDelayer();
        var processor = CreateProcessor(manager, delayer);
        var settings = new PhoneNumberVerificationsSettings
        {
            RequestDelayMilliseconds = 250,
        };
        var contentItems = new[]
        {
            CreateQueuedContentItem("+17024993350"),
            CreateQueuedContentItem("+17024993351"),
            CreateQueuedContentItem("+17024993352"),
        };

        // Act
        var processed = await processor.ProcessAsync(contentItems, settings, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(3, processed);
        Assert.Equal(["+17024993350", "+17024993351", "+17024993352"], manager.PhoneNumbers);
        Assert.Equal([250, 250], delayer.Delays);

        foreach (var contentItem in contentItems)
        {
            Assert.True(contentItem.TryGet<PhoneNumberVerificationPart>(out var part));
            Assert.Equal(PhoneNumberVerificationStatus.Verified, part.VerificationStatus);
            Assert.Equal(1, part.VerificationAttemptCount);
            Assert.Equal(0, part.FailedAttemptCount);
            Assert.Null(part.LastError);
        }
    }

    [Fact]
    public async Task ProcessAsync_WhenDelayBeforeFirstRequestIsTrue_DelaysBeforeTheFirstProviderRequest()
    {
        // Arrange
        var manager = new FakePhoneNumberVerificationManager(phoneNumber => CreateResult(phoneNumber, PhoneNumberVerificationStatus.Verified));
        var delayer = new FakePhoneNumberVerificationRequestDelayer();
        var processor = CreateProcessor(manager, delayer);
        var settings = new PhoneNumberVerificationsSettings
        {
            RequestDelayMilliseconds = 500,
        };
        var contentItems = new[]
        {
            CreateQueuedContentItem("+17024993350"),
            CreateQueuedContentItem("+17024993351"),
        };

        // Act
        var processed = await processor.ProcessAsync(
            contentItems,
            settings,
            delayBeforeFirstRequest: true,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, processed);
        Assert.Equal([500, 500], delayer.Delays);
    }

    [Fact]
    public async Task ProcessAsync_WhenProviderReturnsFailedResult_StoresFailedStatusErrorAndAttempts()
    {
        // Arrange
        var manager = new FakePhoneNumberVerificationManager(phoneNumber => CreateResult(
            phoneNumber,
            PhoneNumberVerificationStatus.Failed,
            "Provider returned 429 Too Many Requests."));
        var processor = CreateProcessor(manager, new FakePhoneNumberVerificationRequestDelayer());
        var contentItem = CreateQueuedContentItem("+17024993350");

        // Act
        var processed = await processor.ProcessAsync(
            [contentItem],
            new PhoneNumberVerificationsSettings(),
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, processed);
        Assert.True(contentItem.TryGet<PhoneNumberVerificationPart>(out var part));
        Assert.Equal(PhoneNumberVerificationStatus.Failed, part.VerificationStatus);
        Assert.Equal(1, part.VerificationAttemptCount);
        Assert.Equal(1, part.FailedAttemptCount);
        Assert.Equal("Provider returned 429 Too Many Requests.", part.LastError);
        Assert.Equal(_now, part.LastAttemptUtc);
    }

    [Fact]
    public async Task ProcessAsync_WhenManagerThrows_StoresFailedStatusAndError()
    {
        // Arrange
        var manager = new FakePhoneNumberVerificationManager(new InvalidOperationException("No provider is available."));
        var processor = CreateProcessor(manager, new FakePhoneNumberVerificationRequestDelayer());
        var contentItem = CreateQueuedContentItem("+17024993350");

        // Act
        var processed = await processor.ProcessAsync(
            [contentItem],
            new PhoneNumberVerificationsSettings(),
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, processed);
        Assert.True(contentItem.TryGet<PhoneNumberVerificationPart>(out var part));
        Assert.Equal(PhoneNumberVerificationStatus.Failed, part.VerificationStatus);
        Assert.Equal("No provider is available.", part.LastError);
        Assert.Equal(1, part.FailedAttemptCount);
    }

    [Fact]
    public void PreparePendingVerification_WhenPreferredPhoneChanged_MarksContactAndPhoneRecordPending()
    {
        // Arrange
        var phoneNumberContentItem = CreatePhoneNumberContentItem("+17024993350", "Cell");
        var contact = CreateContact(phoneNumberContentItem);

        contact.Apply(new PhoneNumberVerificationPart
        {
            PhoneNumber = "+17024993349",
            NormalizedPhoneNumber = "+17024993349",
            VerificationStatus = PhoneNumberVerificationStatus.Verified,
            LastVerifiedUtc = _now.AddDays(-1),
            VerificationProvider = "Fake",
        });

        // Act
        var prepared = OmnichannelContactPhoneNumberVerificationHandler.PreparePendingVerification(
            contact,
            new DefaultPhoneNumberService());

        // Assert
        Assert.True(prepared);
        Assert.True(contact.TryGet<PhoneNumberVerificationPart>(out var contactPart));
        Assert.True(phoneNumberContentItem.TryGet<PhoneNumberVerificationPart>(out var phonePart));

        Assert.Equal(PhoneNumberVerificationStatus.Unverified, contactPart.VerificationStatus);
        Assert.Equal(PhoneNumberVerificationStatus.Unverified, phonePart.VerificationStatus);
        Assert.Equal("+17024993350", contactPart.PhoneNumber);
        Assert.Equal("+17024993350", contactPart.NormalizedPhoneNumber);
        Assert.Null(contactPart.LastVerifiedUtc);
        Assert.Null(contactPart.VerificationProvider);
        Assert.Null(contactPart.VerificationResultJson);
    }

    private static PhoneNumberVerificationQueueProcessor CreateProcessor(
        IPhoneNumberVerificationManager manager,
        IPhoneNumberVerificationRequestDelayer delayer)
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(_now);

        return new PhoneNumberVerificationQueueProcessor(
            manager,
            delayer,
            clock.Object,
            NullLogger<PhoneNumberVerificationQueueProcessor>.Instance);
    }

    private static PhoneNumberVerificationResult CreateResult(
        string phoneNumber,
        PhoneNumberVerificationStatus status,
        string errorMessage = null)
    {
        return new PhoneNumberVerificationResult
        {
            PhoneNumber = phoneNumber,
            NormalizedPhoneNumber = phoneNumber,
            VerificationProvider = "Fake",
            VerificationDateUtc = _now,
            Status = status,
            LineType = PhoneNumberLineType.Mobile,
            ErrorMessage = errorMessage,
        };
    }

    private static ContentItem CreateQueuedContentItem(string phoneNumber)
    {
        var contentItem = new ContentItem
        {
            ContentItemId = IdGenerator.GenerateId(),
            ContentType = "Contact",
        };

        contentItem.AlterPhoneNumberVerificationPending(phoneNumber, phoneNumber);

        return contentItem;
    }

    private static ContentItem CreateContact(params ContentItem[] phoneNumbers)
    {
        var contact = new ContentItem
        {
            ContentItemId = IdGenerator.GenerateId(),
            ContentType = "Contact",
        };

        contact.Apply(new OmnichannelContactPart());

        var bagPart = new BagPart();

        foreach (var phoneNumber in phoneNumbers)
        {
            bagPart.ContentItems.Add(phoneNumber);
        }

        contact.Apply(OmnichannelConstants.NamedParts.ContactMethods, bagPart);

        return contact;
    }

    private static ContentItem CreatePhoneNumberContentItem(string number, string type)
    {
        var contentItem = new ContentItem
        {
            ContentItemId = IdGenerator.GenerateId(),
            ContentType = OmnichannelConstants.ContentTypes.PhoneNumber,
            DisplayText = $"{type}: {number}",
        };

        contentItem.Alter<PhoneNumberInfoPart>(part =>
        {
            part.Number = new PhoneField { PhoneNumber = number };
            part.Type = new TextField { Text = type };
        });

        return contentItem;
    }

    private sealed class FakePhoneNumberVerificationManager : IPhoneNumberVerificationManager
    {
        private readonly Func<string, PhoneNumberVerificationResult> _resultFactory;
        private readonly Exception _exception;

        public FakePhoneNumberVerificationManager(Func<string, PhoneNumberVerificationResult> resultFactory)
        {
            _resultFactory = resultFactory;
        }

        public FakePhoneNumberVerificationManager(Exception exception)
        {
            _exception = exception;
        }

        public List<string> PhoneNumbers { get; } = [];

        public IReadOnlyCollection<PhoneNumberVerificationProviderDescriptor> GetProviders()
        {
            return [new PhoneNumberVerificationProviderDescriptor("Fake")];
        }

        public Task<IReadOnlyCollection<PhoneNumberVerificationProviderDescriptor>> GetEnabledProvidersAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyCollection<PhoneNumberVerificationProviderDescriptor>>([new PhoneNumberVerificationProviderDescriptor("Fake")]);
        }

        public bool TryGetProvider(string key, out IPhoneNumberVerificationProvider provider)
        {
            provider = null;

            return false;
        }

        public Task<string> GetDefaultProviderKeyAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult("Fake");
        }

        public Task<PhoneNumberVerificationResult> VerifyAsync(
            string phoneNumber,
            string providerKey = null,
            CancellationToken cancellationToken = default)
        {
            PhoneNumbers.Add(phoneNumber);

            if (_exception is not null)
            {
                throw _exception;
            }

            return Task.FromResult(_resultFactory(phoneNumber));
        }
    }

    private sealed class FakePhoneNumberVerificationRequestDelayer : IPhoneNumberVerificationRequestDelayer
    {
        public List<int> Delays { get; } = [];

        public Task DelayAsync(int delayMilliseconds, CancellationToken cancellationToken = default)
        {
            Delays.Add(delayMilliseconds);

            return Task.CompletedTask;
        }
    }
}
