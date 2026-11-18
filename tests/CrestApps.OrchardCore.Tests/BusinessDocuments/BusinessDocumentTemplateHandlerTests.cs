using CrestApps.Core.Models;
using CrestApps.OrchardCore.BusinessDocuments.Handlers;
using CrestApps.OrchardCore.BusinessDocuments.Models;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.Tests.BusinessDocuments;

public sealed class BusinessDocumentTemplateHandlerTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ValidatingAsync_RequiresAName(string name)
    {
        // Arrange
        var context = new ValidatingContext<BusinessDocumentTemplate>(new BusinessDocumentTemplate { Name = name });

        // Act
        await CreateHandler().ValidatingAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(context.Result.Errors, error => error.MemberNames.Contains(nameof(BusinessDocumentTemplate.Name)));
    }

    [Theory]
    [InlineData("red")]
    [InlineData("#12345")]
    [InlineData("#GGGGGG")]
    [InlineData("2563EB")]
    [InlineData("#2563EB;background:url(x)")]
    public async Task ValidatingAsync_RefusesAnAccentColorNotWrittenAsSixHexadecimalDigits(string accentColor)
    {
        // Arrange
        var context = new ValidatingContext<BusinessDocumentTemplate>(new BusinessDocumentTemplate { Name = "Standard", AccentColor = accentColor });

        // Act
        await CreateHandler().ValidatingAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var error = Assert.Single(context.Result.Errors);
        Assert.Contains(nameof(BusinessDocumentTemplate.AccentColor), error.MemberNames);
    }

    [Fact]
    public async Task ValidatingAsync_AcceptsAndNormalizesAValidTemplate()
    {
        // Arrange
        var template = new BusinessDocumentTemplate
        {
            Name = "  Standard ",
            AccentColor = " #2563eb ",
            LogoUrl = " /media/logo.png ",
            Kinds = ["Invoice", " invoice ", "", "Receipt"],
        };
        var context = new ValidatingContext<BusinessDocumentTemplate>(template);

        // Act
        await CreateHandler().ValidatingAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(context.Result.Errors);
        Assert.Equal("Standard", template.Name);
        Assert.Equal("#2563EB", template.AccentColor);
        Assert.Equal("/media/logo.png", template.LogoUrl);
        Assert.Equal(new[] { "Invoice", "Receipt" }, template.Kinds);
    }

    [Fact]
    public async Task ValidatingAsync_RefusesALogoAddressWithAnUnsafeScheme()
    {
        // Arrange
        var context = new ValidatingContext<BusinessDocumentTemplate>(new BusinessDocumentTemplate { Name = "Standard", LogoUrl = "javascript:alert(1)" });

        // Act
        await CreateHandler().ValidatingAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var error = Assert.Single(context.Result.Errors);
        Assert.Contains(nameof(BusinessDocumentTemplate.LogoUrl), error.MemberNames);
    }

    private static BusinessDocumentTemplateHandler CreateHandler()
        => new(new HttpContextAccessor(), new TestClock(DateTime.UtcNow), new PassThroughStringLocalizer<BusinessDocumentTemplateHandler>());
}
