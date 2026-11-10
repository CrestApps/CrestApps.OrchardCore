using System.Diagnostics;
using System.Net;
using System.Text;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.DncRegistry;
using CrestApps.OrchardCore.DncRegistry.Models;
using CrestApps.OrchardCore.DncRegistry.Services;
using CrestApps.OrchardCore.DncRegistry.Telemetry;
using CrestApps.OrchardCore.PhoneNumbers;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Telemetry.DncRegistry;

/// <summary>
/// Screening numbers against a do-not-call registry and importing a local list emit the <c>crestapps.dnc.*</c>
/// instruments and spans the observability docs list, tagged with bounded values only. A phone number is never a tag.
/// The names are a public contract that dashboards and alerts depend on.
/// </summary>
public sealed class DncRegistryTelemetryTests
{
    private const string NationalNumber = "4255551212";

    private static readonly ActivitySource _testSource = new("CrestApps.OrchardCore.Tests.DncRegistry");

    private static readonly PhoneNumber _number = PhoneNumber.FromE164("+1" + NationalNumber);

    [Fact]
    public async Task Screening_WithARemoteRegistry_EmitsTheScreeningSpanAndMetrics()
    {
        // Arrange
        using var capture = new TelemetryCapture();
        var registry = CreateCanadaDnclRegistry(new StubHttpMessageHandler(HttpStatusCode.OK, """{ "IsRegistered": true }"""));

        // Act
        Activity parent;

        using (parent = _testSource.StartActivity("test"))
        {
            await registry.GetRegisteredNumbersAsync([_number], TestContext.Current.CancellationToken);
        }

        // Assert
        var screening = Assert.Single(capture.Spans, span => span.ParentSpanId == parent.SpanId);
        Assert.Equal(DncRegistryTelemetry.ScreeningSpanName, screening.OperationName);
        Assert.Equal(DncRegistryTelemetry.RegistryCanadaDncl, screening.GetTagItem(DncRegistryTelemetry.RegistryTag));
        Assert.Equal(CrestAppsOrchardCoreTelemetry.OutcomeOk, screening.GetTagItem(CrestAppsOrchardCoreTelemetry.OutcomeTag));
        Assert.Equal(1, screening.GetTagItem("crestapps.dnc.numbers.checked"));
        Assert.Equal(1, screening.GetTagItem("crestapps.dnc.numbers.listed"));

        Assert.Contains(
            capture.Find("crestapps.dnc.screening.duration", DncRegistryTelemetry.RegistryTag, DncRegistryTelemetry.RegistryCanadaDncl),
            measurement => Equals(measurement.Tags[CrestAppsOrchardCoreTelemetry.OutcomeTag], CrestAppsOrchardCoreTelemetry.OutcomeOk));
        Assert.NotEmpty(capture.Find("crestapps.dnc.screening.size", DncRegistryTelemetry.RegistryTag, DncRegistryTelemetry.RegistryCanadaDncl));
        Assert.NotEmpty(capture.Find("crestapps.dnc.screening.listed", DncRegistryTelemetry.RegistryTag, DncRegistryTelemetry.RegistryCanadaDncl));

        AssertNoUnsafeTags(capture);
    }

    [Fact]
    public async Task Screening_WhenTheRegistryFails_RecordsAnError()
    {
        // Arrange
        using var capture = new TelemetryCapture();
        var registry = CreateCanadaDnclRegistry(new StubHttpMessageHandler(HttpStatusCode.ServiceUnavailable, "{}"));

        // Act
        Activity parent;

        using (parent = _testSource.StartActivity("test"))
        {
            await Assert.ThrowsAsync<DoNotCallScreeningException>(
                () => registry.GetRegisteredNumbersAsync([_number], TestContext.Current.CancellationToken));
        }

        // Assert
        var screening = Assert.Single(capture.Spans, span => span.ParentSpanId == parent.SpanId);
        Assert.Equal(CrestAppsOrchardCoreTelemetry.OutcomeError, screening.GetTagItem(CrestAppsOrchardCoreTelemetry.OutcomeTag));
        Assert.Equal(ActivityStatusCode.Error, screening.Status);

        Assert.Contains(
            capture.Find("crestapps.dnc.screening.duration", DncRegistryTelemetry.RegistryTag, DncRegistryTelemetry.RegistryCanadaDncl),
            measurement => Equals(measurement.Tags[CrestAppsOrchardCoreTelemetry.OutcomeTag], CrestAppsOrchardCoreTelemetry.OutcomeError));

        AssertNoUnsafeTags(capture);
    }

    [Fact]
    public void ImportRows_AreCountedOnlyOnceTheirBatchIsCommitted()
    {
        // Arrange
        using var capture = new TelemetryCapture();

        // A result value only this test uses keeps the measurements of imports running in parallel tests apart.
        var result = "test_" + Guid.NewGuid().ToString("n");
        var rows = new DncImportRowTally();

        // Act
        rows.Add(result);
        rows.Add(result);
        rows.Add(DncRegistryTelemetry.RowImported);
        rows.Add(DncRegistryTelemetry.RowHeader);

        var beforeCommit = capture.Find("crestapps.dnc.import.rows", CrestAppsOrchardCoreTelemetry.ResultTag, result);

        rows.Commit();

        // Assert
        Assert.Empty(beforeCommit);
        Assert.Equal(2, Assert.Single(capture.Find("crestapps.dnc.import.rows", CrestAppsOrchardCoreTelemetry.ResultTag, result)).Value);
        Assert.Equal(1, rows.ImportedRows);

        // The header row is neither imported nor rejected.
        Assert.Equal(2, rows.RejectedRows);

        AssertNoUnsafeTags(capture);
    }

    [Fact]
    public void ImportRun_KeepsTheListIdOnTheSpanOnly()
    {
        // Arrange
        using var capture = new TelemetryCapture();
        var listId = "list" + Guid.NewGuid().ToString("n");

        // Act
        using (var run = DncRegistryTelemetry.StartImportRun(listId))
        {
            run.SetOutcome(DncRegistryTelemetry.ImportOutcomeCompleted);
        }

        // Assert
        var span = Assert.Single(capture.Spans, span => Equals(span.GetTagItem(DncRegistryTelemetry.ListIdSpanTag), listId));
        Assert.Equal(DncRegistryTelemetry.ImportRunSpanName, span.OperationName);
        Assert.Equal(DncRegistryTelemetry.ImportOutcomeCompleted, span.GetTagItem(CrestAppsOrchardCoreTelemetry.OutcomeTag));

        Assert.NotEmpty(capture.Find("crestapps.dnc.import.duration", CrestAppsOrchardCoreTelemetry.OutcomeTag, DncRegistryTelemetry.ImportOutcomeCompleted));

        Assert.DoesNotContain(
            capture.Measurements,
            measurement => measurement.Tags.Values.Any(value => Equals(value, listId)));

        AssertNoUnsafeTags(capture);
    }

    private static void AssertNoUnsafeTags(TelemetryCapture capture)
    {
        Assert.Empty(capture.FindBannedTagKeys());

        // No measurement of the module carries the screened number, in any form.
        Assert.DoesNotContain(
            capture.Measurements.Where(measurement => measurement.Instrument.StartsWith("crestapps.dnc.", StringComparison.Ordinal)),
            measurement => measurement.Tags.Values.Any(value => value?.ToString()?.Contains(NationalNumber, StringComparison.Ordinal) == true));
    }

    private static CanadaDnclRegistry CreateCanadaDnclRegistry(HttpMessageHandler handler)
    {
        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var settings = new CanadaDnclRegistrySettings
        {
            ProtectedApiKey = dataProtectionProvider
                .CreateProtector(DncRegistryConstants.DataProtectionPurposes.CanadaDnclApiKey)
                .Protect("fake-dncl-api-key"),
            AccountNumber = "account-1",
            BaseUrl = "https://dncl.example.test/api",
        };

        var site = new Mock<ISite>();
        site.Setup(site => site.GetOrCreate<CanadaDnclRegistrySettings>())
            .Returns(settings);

        var siteService = new Mock<ISiteService>();
        siteService.Setup(service => service.GetSiteSettingsAsync())
            .ReturnsAsync(site.Object);

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory
            .Setup(factory => factory.CreateClient(nameof(CanadaDnclRegistry)))
            .Returns(new HttpClient(handler));

        var localizer = new Mock<IStringLocalizer<CanadaDnclRegistry>>();
        localizer
            .Setup(localizer => localizer[It.IsAny<string>()])
            .Returns((string name) => new LocalizedString(name, name));

        return new CanadaDnclRegistry(
            httpClientFactory.Object,
            siteService.Object,
            dataProtectionProvider,
            localizer.Object,
            NullLogger<CanadaDnclRegistry>.Instance);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _content;

        public StubHttpMessageHandler(HttpStatusCode statusCode, string content)
        {
            _statusCode = statusCode;
            _content = content;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_content, Encoding.UTF8, "application/json"),
            });
        }
    }
}
