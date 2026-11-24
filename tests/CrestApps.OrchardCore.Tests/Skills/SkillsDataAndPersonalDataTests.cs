using CrestApps.OrchardCore.PersonalData;
using CrestApps.OrchardCore.Skills;
using CrestApps.OrchardCore.Skills.Core.Services;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Reports;
using CrestApps.OrchardCore.Skills.Services;
using CrestApps.OrchardCore.Tests.DataConnectors;
using CrestApps.OrchardCore.Tests.Modules.Reports.Designer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.Tests.Skills;

/// <summary>
/// The Skills data source (certifications and certificates, never a verification code) and the personal-data
/// contributor (export everything; anonymizing keeps issued certificates without the name).
/// </summary>
public sealed class SkillsDataAndPersonalDataTests
{
    private static readonly SkillSubject _ana = SkillSubject.User("ana");

    [Fact]
    public async Task DataSource_ListsCertificatesWithTheirSource_ButNeverTheVerificationCode()
    {
        // Arrange
        await using var harness = await CreateHarnessAsync();
        var cpr = await harness.AddCertificationAsync("CPR", certification => certification.GenerateNumbers = true);
        var issued = await IssueAsync(harness, cpr.ItemId, "path:ana:1");

        // Act
        var (sets, rows, denied) = await harness.RunAsync(async services =>
        {
            var source = services.GetRequiredService<SkillsReportDataSource>();
            var context = new DataSourceContext { User = PermissionClaimsAuthorizationService.UserWith(SkillsPermissions.ViewSkills.Name) };
            var all = await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken);
            var result = await ReportDesignerTestServices.ReadAsync(source, new DataSourceQuery { DataSet = CertificatesReportDataSet.DataSetName, MaxRows = 10, Context = context }, TestContext.Current.CancellationToken);
            var none = await source.GetDataSetsAsync(new DataSourceContext { User = PermissionClaimsAuthorizationService.UserWith() }, TestContext.Current.CancellationToken);

            return (all, result, none);
        }, commit: false);

        // Assert
        Assert.Equal([CertificationsReportDataSet.DataSetName, CertificatesReportDataSet.DataSetName], sets.Select(set => set.Name));
        Assert.Empty(denied);

        var fields = rows.Fields.Select(field => field.Name).ToList();
        var row = Assert.Single(rows.Rows);

        Assert.DoesNotContain(fields, field => field.Contains("Code", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(row, value => Equals(value, issued.Record.VerificationCode));
        Assert.Equal("CERT-000001", row[fields.IndexOf("Number")]);
        Assert.Equal("Learning.CertificationPath", row[fields.IndexOf("Source")]);
    }

    [Fact]
    public async Task PersonalData_ExportsSkillsAndCertificates_AndAnonymizingKeepsIssuedCertificatesWithoutTheName()
    {
        // Arrange
        await using var harness = await CreateHarnessAsync();
        var cpr = await harness.AddCertificationAsync("CPR");
        var gas = await harness.AddCertificationAsync("Gas safety");
        var welding = await harness.AddSkillAsync("Welding");
        var issued = await IssueAsync(harness, cpr.ItemId, "path:ana:1");
        var byHand = await SkillsTests.SaveRecordAsync(harness, _ana, gas.ItemId, SkillsTestHarness.Today, CertificationStatus.Valid);

        await harness.RunAsync(services => services.GetRequiredService<ISkillAssignmentService>().AssignAsync(_ana, new SkillAssignmentInput { SkillId = welding.ItemId, Proficiency = 4 }, "manager"));

        // Act
        var export = await harness.RunAsync(async services =>
        {
            var context = new PersonalDataExportContext(new PersonalDataSubject("ana"), new PersonalDataExport());

            await services.GetRequiredService<SkillsPersonalDataContributor>().ExportAsync(context, TestContext.Current.CancellationToken);

            return context.Export;
        }, commit: false);

        var result = await harness.RunAsync(async services =>
        {
            var context = new PersonalDataAnonymizeContext(new PersonalDataSubject("ana"), null, new PersonalDataAnonymizeResult());

            await services.GetRequiredService<SkillsPersonalDataContributor>().AnonymizeAsync(context, TestContext.Current.CancellationToken);

            return context.Result;
        });

        var remaining = await harness.RunAsync(services => services.GetRequiredService<ICertificationRecordService>().GetForSubjectAsync(_ana), commit: false);

        // Assert
        Assert.Equal(3, export.Records.Count);
        Assert.Contains(export.Records, record => record.Kind == "Certificate" && record.Fields["VerificationCode"] == issued.Record.VerificationCode);
        Assert.Contains(export.Records, record => record.Kind == "SkillAssignment" && record.Fields["Proficiency"] == "4");

        var kept = Assert.Single(remaining);

        Assert.Equal(issued.Record.ItemId, kept.ItemId);
        Assert.Null(kept.SubjectName);
        Assert.Null(kept.Evidence);
        Assert.NotEqual(byHand.ItemId, kept.ItemId);
        Assert.Single(result.Warnings);
    }

    private static Task<SkillsTestHarness> CreateHarnessAsync()
    {
        return SkillsTestHarness.CreateAsync(services =>
        {
            services.AddSingleton<IAuthorizationService, PermissionClaimsAuthorizationService>();
            services.AddScoped<ISkillsReportDataSet, CertificationsReportDataSet>();
            services.AddScoped<ISkillsReportDataSet, CertificatesReportDataSet>();
            services.AddScoped<SkillsReportDataSource>();
            services.AddScoped<SkillsPersonalDataContributor>();
        });
    }

    private static Task<CertificateIssueResult> IssueAsync(SkillsTestHarness harness, string certificationId, string sourceId)
    {
        return harness.RunAsync(services => services.GetRequiredService<ICertificateIssuer>().IssueAsync(new CertificateIssueRequest
        {
            CertificationId = certificationId,
            Subject = _ana,
            SubjectName = "Ana Lima",
            Source = "Learning.CertificationPath",
            SourceId = sourceId,
            Evidence = "Course passed",
            IssuedOn = SkillsTestHarness.Today,
            Actor = "Learning",
        }));
    }
}
