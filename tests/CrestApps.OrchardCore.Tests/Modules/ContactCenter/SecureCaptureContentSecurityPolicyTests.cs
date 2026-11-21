using CrestApps.OrchardCore.ContactCenter.Controllers;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// SEC-FE3: the anonymous Secure Capture page collects card data, so it sends a strict Content Security Policy (no
/// script, styles only with the response's nonce, posts only to itself, never framed) and its inputs carry the card
/// autocomplete tokens.
/// </summary>
public sealed class SecureCaptureContentSecurityPolicyTests
{
    [Fact]
    public async Task Index_SendsAStrictContentSecurityPolicyWithANonce()
    {
        var service = new Mock<ISecureCaptureService>();
        service
            .Setup(s => s.GetForCustomerAsync("token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SecureCaptureSession { RequestedFields = [SecureCaptureField.CreditCardNumber] });

        var httpContext = new DefaultHttpContext();
        var controller = new SecureCaptureController(service.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
        };

        var view = Assert.IsType<ViewResult>(await controller.Index("token"));
        var model = Assert.IsType<SecureCaptureFormViewModel>(view.Model);
        var policy = httpContext.Response.Headers.ContentSecurityPolicy.ToString();

        Assert.False(string.IsNullOrEmpty(model.CspNonce));
        Assert.Contains("default-src 'none'", policy);
        Assert.Contains($"style-src 'nonce-{model.CspNonce}'", policy);
        Assert.Contains("form-action 'self'", policy);
        Assert.Contains("frame-ancestors 'none'", policy);
        Assert.Contains("base-uri 'none'", policy);
        Assert.DoesNotContain("script-src", policy);
    }

    [Theory]
    [InlineData("SecureCaptureField.CreditCardNumber => \"cc-number\"")]
    [InlineData("SecureCaptureField.CardExpiry => \"cc-exp\"")]
    [InlineData("SecureCaptureField.CardSecurityCode => \"cc-csc\"")]
    [InlineData("nonce=\"@Model.CspNonce\"")]
    public void TheView_UsesTheCardAutocompleteTokensAndTheNonce(string expected)
    {
        var view = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "Modules", "CrestApps.OrchardCore.ContactCenter", "Views", "SecureCapture", "Index.cshtml"));

        Assert.Contains(expected, view);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src"))
                && Directory.Exists(Path.Combine(directory.FullName, "tests", "CrestApps.OrchardCore.Tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("The repository root was not found.");
    }
}
