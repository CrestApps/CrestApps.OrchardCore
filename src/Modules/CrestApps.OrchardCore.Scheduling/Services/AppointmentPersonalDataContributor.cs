using System.Globalization;
using CrestApps.OrchardCore.PersonalData;
using CrestApps.OrchardCore.Scheduling.Core.Services;
using CrestApps.OrchardCore.Scheduling.Models;

namespace CrestApps.OrchardCore.Scheduling.Services;

/// <summary>
/// Exports and anonymizes the appointments a person booked, with their account, their email or their phone number.
/// An appointment keeps its time, type, resources and state, which the business needs for its schedule and reports; the
/// customer's name, email, phone number and manage link go, and their details are scrubbed from the notes.
/// </summary>
public sealed class AppointmentPersonalDataContributor : IPersonalDataContributor
{
    /// <summary>The record kind of an appointment.</summary>
    public const string AppointmentKind = "Appointment";

    private readonly AppointmentStore _appointments;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppointmentPersonalDataContributor"/> class.
    /// </summary>
    /// <param name="appointments">The appointment store.</param>
    public AppointmentPersonalDataContributor(AppointmentStore appointments)
    {
        _appointments = appointments;
    }

    /// <inheritdoc/>
    public async Task IdentifyAsync(PersonalDataSubject subject, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subject);

        // Only the name, for scrubbing free text: an email or a phone number on a record may be shared by other people,
        // so adding it would lead the other contributors to their records.
        foreach (var appointment in await ListAsync(subject, cancellationToken))
        {
            subject.AddName(appointment.CustomerName);
        }
    }

    /// <inheritdoc/>
    public async Task ExportAsync(PersonalDataExportContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var appointment in await ListAsync(context.Subject, cancellationToken))
        {
            var title = appointment.StartUtc.ToString("u", CultureInfo.InvariantCulture);
            var record = context.Add(AppointmentKind, appointment.ItemId, title, appointment.CreatedUtc);

            record.Fields[nameof(Appointment.AppointmentTypeId)] = appointment.AppointmentTypeId;
            record.Fields[nameof(Appointment.StartUtc)] = appointment.StartUtc.ToString("O", CultureInfo.InvariantCulture);
            record.Fields[nameof(Appointment.EndUtc)] = appointment.EndUtc.ToString("O", CultureInfo.InvariantCulture);
            record.Fields[nameof(Appointment.Status)] = appointment.Status.ToString();
            record.Fields[nameof(Appointment.CustomerName)] = appointment.CustomerName;
            record.Fields[nameof(Appointment.CustomerEmail)] = appointment.CustomerEmail;
            record.Fields[nameof(Appointment.CustomerPhone)] = appointment.CustomerPhone;
            record.Fields[nameof(Appointment.Notes)] = appointment.Notes;
            record.Fields[nameof(Appointment.CancelReason)] = appointment.CancelReason;
        }
    }

    /// <inheritdoc/>
    public async Task AnonymizeAsync(PersonalDataAnonymizeContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var appointment in await ListAsync(context.Subject, cancellationToken))
        {
            appointment.CustomerName = context.ReplaceName(appointment.CustomerName);
            appointment.CustomerEmail = null;
            appointment.CustomerPhone = null;
            appointment.ManageToken = null;
            appointment.Notes = context.Scrub(appointment.Notes);
            appointment.CancelReason = context.Scrub(appointment.CancelReason);

            await _appointments.UpdateAsync(appointment, cancellationToken);
            context.Result.Count(AppointmentKind);
        }
    }

    private async Task<IReadOnlyList<Appointment>> ListAsync(PersonalDataSubject subject, CancellationToken cancellationToken)
    {
        var appointments = await _appointments.ListForPersonAsync(subject.UserIds, subject.Emails, subject.PhoneNumbers, cancellationToken);

        return [.. appointments.Where(appointment => IsPersons(appointment, subject))];
    }

    // A phone number is often shared (a household, a front desk), so an appointment found by it is only the person's
    // when nothing on it says it is someone else's: it was booked by the person's account or email, or by phone alone.
    internal static bool IsPersons(Appointment appointment, PersonalDataSubject subject)
        => subject.IsUser(appointment.UserId) ||
            subject.IsEmail(appointment.CustomerEmail) ||
            (string.IsNullOrWhiteSpace(appointment.UserId) && string.IsNullOrWhiteSpace(appointment.CustomerEmail));
}
