using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;

namespace CrestApps.OrchardCore.Tests.Surveys.Controls;

public sealed class SurveyControlRegistrationCheckerTests
{
    [Fact]
    public void Check_BuiltInTypes_HaveNoProblems()
    {
        // Arrange
        using var services = ControlsTestServices.Build();
        using var scope = services.CreateScope();

        // Act
        var problems = SurveyControlRegistrationChecker.Check(
            scope.ServiceProvider.GetRequiredService<IOptions<SurveyControlOptions>>().Value,
            scope.ServiceProvider.GetServices<ISurveyControlHandler>(),
            scope.ServiceProvider.GetServices<ISurveyControlAnalyzer>());

        // Assert
        Assert.Empty(problems);
    }

    [Fact]
    public void Check_HandlerReportingAnotherName_IsReported()
    {
        // Arrange
        var handler = Misnamed();
        var options = new SurveyControlOptions();
        options.Controls["Acme.Thumbs"] = new SurveyControlDescriptor { Name = "Acme.Thumbs", HandlerType = handler.GetType() };

        // Act
        var problems = SurveyControlRegistrationChecker.Check(options, [handler], []);

        // Assert
        Assert.Contains(problems, problem => problem.Contains("'Acme.Thumbs'", StringComparison.Ordinal) && problem.Contains("'Thumbs'", StringComparison.Ordinal));
    }

    [Fact]
    public void Check_ClientScriptWithoutResource_IsReported()
    {
        // Arrange
        var handler = Misnamed();
        var options = new SurveyControlOptions();
        options.Controls["Thumbs"] = new SurveyControlDescriptor
        {
            Name = "Thumbs",
            HandlerType = handler.GetType(),
            Capabilities = SurveyControlCapabilities.Answerable | SurveyControlCapabilities.ClientScript,
        };

        // Act
        var problems = SurveyControlRegistrationChecker.Check(options, [handler], []);

        // Assert
        Assert.Contains(problems, problem => problem.Contains("ScriptResourceName", StringComparison.Ordinal));
    }

    private static ISurveyControlHandler Misnamed()
    {
        var handler = new Mock<ISurveyControlHandler>();
        handler.SetupGet(item => item.ControlType).Returns("Thumbs");

        return handler.Object;
    }
}
