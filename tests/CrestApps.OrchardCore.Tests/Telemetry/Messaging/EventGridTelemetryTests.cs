using System.Diagnostics;
using System.Reflection;
using System.Text;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.EventGrid;
using CrestApps.OrchardCore.Omnichannel.EventGrid.Models;
using CrestApps.OrchardCore.Omnichannel.EventGrid.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Environment.Shell;
using Startup = CrestApps.OrchardCore.Omnichannel.EventGrid.Startup;
using YesSqlSession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Telemetry.Messaging;

/// <summary>
/// The Azure Event Grid webhook counts the events its deliveries carry under <c>CrestApps.OrchardCore.EventGrid</c>,
/// with the event type mapped to a bounded set (the type is chosen by whoever publishes to the topic), and tags what
/// became of each delivery on the ASP.NET Core server span.
/// </summary>
public sealed class EventGridTelemetryTests
{
    private static readonly Type _endpointType = typeof(EventGridEventMapper).Assembly.GetType("AzureEventGridEndpoint", throwOnError: true);

    [Theory]
    [InlineData(EventGridEventTypes.SubscriptionValidation, "subscription_validation")]
    [InlineData(EventGridEventTypes.AcsSmsReceived, "sms_received")]
    [InlineData(EventGridEventTypes.AcsSmsDeliveryReportReceived, "sms_delivery_report")]
    [InlineData("Contoso.Orders.Created", "other")]
    [InlineData(null, "other")]
    public void GetEventTypeTag_MapsEveryTypeToABoundedValue(string eventType, string expected)
    {
        Assert.Equal(expected, EventGridTelemetry.GetEventTypeTag(eventType));
    }

    [Fact]
    public void RecordEvent_TagsTheBoundedTypeAndTheResult()
    {
        // Arrange
        using var capture = new TelemetryCapture();
        var unknownType = "Contoso." + Guid.NewGuid().ToString("n");

        // Act
        EventGridTelemetry.RecordEvent(unknownType, EventGridTelemetry.EventResults.Stored);

        // Assert
        // The webhook tests may record the same bounded tags at the same time, so the free type is what tells this
        // measurement apart: it must never reach a metric.
        Assert.Contains(
            capture.Measurements,
            measurement => measurement.Meter == EventGridTelemetry.SourceName &&
                measurement.Instrument == "crestapps.eventgrid.event" &&
                Equals(measurement.Tags[CrestAppsOrchardCoreTelemetry.ResultTag], EventGridTelemetry.EventResults.Stored) &&
                Equals(measurement.Tags[EventGridTelemetry.TypeTag], "other"));

        Assert.DoesNotContain(capture.Measurements, entry => entry.Tags.ContainsValue(unknownType));
        Assert.Empty(capture.FindBannedTagKeys());
    }

    [Fact]
    public async Task HandleAsync_WhenTheRequestIsNotAuthorized_TagsTheServerSpanUnauthorized()
    {
        // Arrange
        // Stands in for the server span ASP.NET Core starts around the request.
        using var serverSpan = new Activity("server").Start();
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider(),
        };

        context.Request.Method = HttpMethods.Post;
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("[]"));

        // Act
        var result = await InvokeHandleAsync(context);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(EventGridTelemetry.RequestResults.Unauthorized, serverSpan.GetTagItem("crestapps.eventgrid.result"));
    }

    private static Task<IResult> InvokeHandleAsync(HttpContext context)
    {
        var method = _endpointType.GetMethod("HandleAsync", BindingFlags.NonPublic | BindingFlags.Static);

        return (Task<IResult>)method.Invoke(null,
        [
            context,
            Array.Empty<IOmnichannelEventHandler>(),
            Mock.Of<YesSqlSession>(),
            new StubClock(new DateTime(2026, 11, 10, 9, 0, 0, DateTimeKind.Utc)),
            Options.Create(new EventGridOptions { EventGridSasKey = "test-sas-key" }),
            Mock.Of<IShellHost>(),
            new ShellSettings { Name = "TenantA" },
            NullLogger<Startup>.Instance,
        ]);
    }
}
