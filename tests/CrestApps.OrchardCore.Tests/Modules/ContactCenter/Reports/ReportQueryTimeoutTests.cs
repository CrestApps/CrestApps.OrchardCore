using System.Data.Common;
using CrestApps.OrchardCore.ContactCenter.Reports.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.Reports;

/// <summary>
/// A report over a period too wide for the database to read in time used to surface as an unhandled SQL timeout
/// (an HTTP 500 after the full command timeout). It is recognized and answered with what to change instead, without
/// swallowing the errors that are not a timeout.
/// </summary>
public sealed class ReportQueryTimeoutTests
{
    [Fact]
    public void IsTimeout_ForADatabaseTimeoutMessage_IsTrue()
    {
        // Arrange
        var exception = new TestDbException("Execution Timeout Expired. The timeout period elapsed prior to completion of the operation.");

        // Act & Assert
        Assert.True(ReportQueryTimeout.IsTimeout(exception));
    }

    [Fact]
    public void IsTimeout_ForATimeoutWrappedByAnotherException_IsTrue()
    {
        // Arrange
        var exception = new InvalidOperationException("The query failed.", new TimeoutException());

        // Act & Assert
        Assert.True(ReportQueryTimeout.IsTimeout(exception));
    }

    [Fact]
    public void IsTimeout_ForADatabaseErrorThatIsNotATimeout_IsFalse()
    {
        // Arrange
        var exception = new TestDbException("Login failed for user 'reports'.");

        // Act & Assert
        Assert.False(ReportQueryTimeout.IsTimeout(exception));
    }

    [Fact]
    public void IsTimeout_ForAnOrdinaryException_IsFalse()
    {
        // Act & Assert
        Assert.False(ReportQueryTimeout.IsTimeout(new InvalidOperationException("Something else went wrong.")));
    }

    /// <summary>
    /// A visitor who navigates away cancels the request. That is not a timeout, and answering it with an error
    /// message would hide the cancellation from the caller.
    /// </summary>
    [Fact]
    public void IsTimeout_ForACancelledRequest_IsFalse()
    {
        // Arrange
        var exception = new OperationCanceledException("The request was aborted.", new TimeoutException());

        // Act & Assert
        Assert.False(ReportQueryTimeout.IsTimeout(exception));
    }

    [Fact]
    public void Describe_CarriesAMessageAndNoMeasurements()
    {
        // Act
        var document = ReportQueryTimeout.Describe(new EchoLocalizer());

        // Assert
        var section = Assert.Single(document.Sections);
        Assert.False(string.IsNullOrWhiteSpace(section.Title));
        Assert.Contains("shorter period", section.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(section.Rows);
    }

    private sealed class TestDbException : DbException
    {
        public TestDbException(string message)
            : base(message)
        {
        }
    }

    private sealed class EchoLocalizer : IStringLocalizer
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, name);

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
