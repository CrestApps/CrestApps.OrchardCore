using System.Net;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Learning.IntegrationTests;

/// <summary>
/// Issued certificates on a running site: the public verification page proves a certificate by its code and shows a
/// revocation at once, the printed certificate is a PDF and is no longer handed out once revoked, and a learner's
/// certifications page needs a sign-in.
/// </summary>
[Collection(LearningSiteCollection.Name)]
public sealed class CertificationPagesTests
{
    private readonly LearningSiteFixture _fixture;

    public CertificationPagesTests(LearningSiteFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task VerificationPage_ProvesACertificateByItsCode_AndPrintsItAsAPdf()
    {
        // Arrange
        var record = await IssueAsync("Fire Warden " + Guid.NewGuid().ToString("n")[..6], "Dana Example");
        var code = CertificateVerificationCodes.Format(record.VerificationCode);
        var anonymous = await _fixture.CreateClientAsync();

        // Act
        var page = await anonymous.GetAsync($"certificates/verify/{code}", TestContext.Current.CancellationToken);
        var html = await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var typed = await anonymous.GetAsync($"certificates/verify?code={code.ToLowerInvariant().Replace("-", " ", StringComparison.Ordinal)}", TestContext.Current.CancellationToken);
        var unknown = await anonymous.GetStringAsync("certificates/verify/AAAA-AAAA-AAAA-AAAA", TestContext.Current.CancellationToken);
        var pdf = await anonymous.GetAsync($"certificates/verify/{code}/certificate.pdf", TestContext.Current.CancellationToken);
        var bytes = await pdf.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("Dana Example", html);
        Assert.Contains("Valid", html);
        Assert.Contains("noindex", page.Headers.GetValues("X-Robots-Tag").Single());
        Assert.Equal(HttpStatusCode.Redirect, typed.StatusCode);
        Assert.EndsWith($"/certificates/verify/{code}", typed.Headers.Location.ToString(), StringComparison.Ordinal);
        Assert.Contains("No certificate has this code", unknown);
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
    }

    [Fact]
    public async Task Revocation_ShowsOnTheVerificationPageAtOnce_AndThePdfIsNoLongerHandedOut()
    {
        // Arrange
        var record = await IssueAsync("First Aid " + Guid.NewGuid().ToString("n")[..6], "Eli Example");
        var code = CertificateVerificationCodes.Format(record.VerificationCode);
        var anonymous = await _fixture.CreateClientAsync();

        // Act
        await _fixture.InTenantAsync(services => services.GetRequiredService<ICertificateIssuer>().RevokeAsync(record.ItemId, "auditor", "Exam voided"));

        var html = await anonymous.GetStringAsync($"certificates/verify/{code}", TestContext.Current.CancellationToken);
        var pdf = await anonymous.GetAsync($"certificates/verify/{code}/certificate.pdf", TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("Revoked", html);
        Assert.Contains("Revoked on", html);
        Assert.Equal(HttpStatusCode.NotFound, pdf.StatusCode);
    }

    [Fact]
    public async Task MyCertifications_NeedsASignIn()
    {
        // Arrange
        var anonymous = await _fixture.CreateClientAsync();

        // Act
        var page = await anonymous.GetAsync("learning/certifications", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, page.StatusCode);
        Assert.Contains("Login", page.Headers.Location.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private Task<CertificationRecord> IssueAsync(string certificationName, string holderName)
        => _fixture.InTenantAsync(async services =>
        {
            var manager = services.GetRequiredService<ICertificationManager>();
            var certification = await manager.NewAsync();

            certification.Name = certificationName;
            certification.ValidityMonths = 24;
            certification.GenerateNumbers = true;
            await manager.CreateAsync(certification);

            var result = await services.GetRequiredService<ICertificateIssuer>().IssueAsync(new CertificateIssueRequest
            {
                CertificationId = certification.ItemId,
                Subject = SkillSubject.User(_fixture.EnrolledUserId),
                SubjectName = holderName,
                Source = "Tests",
                SourceId = Guid.NewGuid().ToString("n"),
                Evidence = "Passed the practical test",
                IssuedOn = DateOnly.FromDateTime(DateTime.UtcNow),
                Actor = "tests",
            });

            Assert.True(result.Succeeded, string.Join(" ", result.Errors));

            return result.Record;
        });
}
