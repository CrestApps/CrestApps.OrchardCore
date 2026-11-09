using CrestApps.OrchardCore.PersonalData;
using CrestApps.OrchardCore.Scheduling.Core.Services;
using CrestApps.OrchardCore.Scheduling.Models;
using CrestApps.OrchardCore.Scheduling.Services;
using CrestApps.OrchardCore.Tests.Scheduling;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.PersonalData;

/// <summary>
/// A person's appointments, past or canceled included, are found by their account, email or phone number; erasing keeps
/// the time and the state but removes the customer's details and manage link.
/// </summary>
public sealed class AppointmentPersonalDataContributorTests
{
    [Fact]
    public async Task AppointmentsFoundByEmailOrPhone_AreExported_AndAnonymized()
    {
        // Arrange
        await using var harness = await SchedulingTestSupport.CreateAsync();

        await harness.RunAsync(async services =>
        {
            var store = services.GetRequiredService<AppointmentStore>();

            await store.CreateAsync(Appointment("a-email", email: "Ann@Example.com", phone: null, AppointmentStatus.Cancelled));
            await store.CreateAsync(Appointment("a-phone", email: null, phone: "+17025550100", AppointmentStatus.Completed));
            await store.CreateAsync(Appointment("a-other", email: "bob@example.com", phone: "+17025550199", AppointmentStatus.Confirmed));
        });

        var subject = new PersonalDataSubject(email: "ann@example.com", phoneNumber: "+17025550100");
        var export = new PersonalDataExport();
        var result = new PersonalDataAnonymizeResult();

        // Act
        await harness.RunAsync(async services =>
        {
            var contributor = new AppointmentPersonalDataContributor(services.GetRequiredService<AppointmentStore>());

            await contributor.ExportAsync(new PersonalDataExportContext(subject, export), TestContext.Current.CancellationToken);
            await contributor.AnonymizeAsync(new PersonalDataAnonymizeContext(subject, null, result), TestContext.Current.CancellationToken);
        });

        // Assert
        Assert.Equal(["a-email", "a-phone"], export.Records.Select(record => record.Id).Order());
        Assert.Equal(2, result.Records[AppointmentPersonalDataContributor.AppointmentKind]);

        var appointments = await harness.RunAsync(async services =>
        {
            var store = services.GetRequiredService<AppointmentStore>();

            return new[]
            {
                await store.FindByIdAsync("a-email"),
                await store.FindByIdAsync("a-other"),
            };
        });

        Assert.Null(appointments[0].CustomerEmail);
        Assert.Null(appointments[0].CustomerName);
        Assert.Null(appointments[0].ManageToken);
        Assert.DoesNotContain("ann@example.com", appointments[0].Notes, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("Please call Ann back", appointments[0].Notes, StringComparison.Ordinal);
        Assert.Equal(AppointmentStatus.Cancelled, appointments[0].Status);
        Assert.Equal("bob@example.com", appointments[1].CustomerEmail);
    }

    [Fact]
    public async Task ASharedPhone_DoesNotReachAnotherPersonsAppointment()
    {
        // Arrange
        await using var harness = await SchedulingTestSupport.CreateAsync();

        await harness.RunAsync(async services =>
        {
            var store = services.GetRequiredService<AppointmentStore>();

            await store.CreateAsync(Appointment("a-mine", email: "ann@example.com", phone: "+17025550100", AppointmentStatus.Completed));
            await store.CreateAsync(Appointment("a-phone-only", email: null, phone: "+17025550100", AppointmentStatus.Completed));
            await store.CreateAsync(Appointment("a-bob", email: "bob@example.com", phone: "+17025550100", AppointmentStatus.Confirmed));
        });

        // A customer erased by their email and the household number they share with Bob.
        var subject = new PersonalDataSubject(email: "ann@example.com", phoneNumber: "+17025550100");
        var export = new PersonalDataExport();
        var result = new PersonalDataAnonymizeResult();

        // Act
        await harness.RunAsync(async services =>
        {
            var contributor = new AppointmentPersonalDataContributor(services.GetRequiredService<AppointmentStore>());

            await contributor.ExportAsync(new PersonalDataExportContext(subject, export), TestContext.Current.CancellationToken);
            await contributor.AnonymizeAsync(new PersonalDataAnonymizeContext(subject, null, result), TestContext.Current.CancellationToken);
        });

        // Assert
        Assert.Equal(["a-mine", "a-phone-only"], export.Records.Select(record => record.Id).Order());
        Assert.Equal(2, result.Records[AppointmentPersonalDataContributor.AppointmentKind]);

        var bob = await harness.RunAsync(async services => await services.GetRequiredService<AppointmentStore>().FindByIdAsync("a-bob"));

        Assert.Equal("bob@example.com", bob.CustomerEmail);
        Assert.Equal("+17025550100", bob.CustomerPhone);
        Assert.Equal("Ann Lee", bob.CustomerName);
    }

    private static Appointment Appointment(string id, string email, string phone, AppointmentStatus status)
        => new()
        {
            ItemId = id,
            AppointmentTypeId = "type-1",
            StartUtc = new DateTime(2026, 10, 1, 14, 0, 0, DateTimeKind.Utc),
            EndUtc = new DateTime(2026, 10, 1, 15, 0, 0, DateTimeKind.Utc),
            Status = status,
            CustomerName = "Ann Lee",
            CustomerEmail = email,
            CustomerPhone = phone,
            Notes = "Please call Ann back ann@example.com",
            ManageToken = "token",
            CreatedUtc = new DateTime(2026, 9, 1, 14, 0, 0, DateTimeKind.Utc),
        };
}
