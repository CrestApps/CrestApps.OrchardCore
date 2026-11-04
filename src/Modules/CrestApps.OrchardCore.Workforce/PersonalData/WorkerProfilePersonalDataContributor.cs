using System.Globalization;
using CrestApps.OrchardCore.PersonalData;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Workforce.PersonalData;

/// <summary>
/// Exports and anonymizes the worker profile of a person's account. The profile keeps its employee number, employment
/// dates, teams and hours, which payroll and labor law need; the person's name, email, phone number, date of birth and
/// home base go.
/// </summary>
internal sealed class WorkerProfilePersonalDataContributor : IPersonalDataContributor
{
    public const string WorkerProfileKind = "WorkerProfile";

    private readonly IWorkerManager _workers;

    internal readonly IStringLocalizer S;

    public WorkerProfilePersonalDataContributor(
        IWorkerManager workers,
        IStringLocalizer<WorkerProfilePersonalDataContributor> stringLocalizer)
    {
        _workers = workers;
        S = stringLocalizer;
    }

    public async Task IdentifyAsync(PersonalDataSubject subject, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subject);

        foreach (var worker in await FindWorkersAsync(_workers, subject, cancellationToken))
        {
            subject.AddEmail(worker.Email);
            subject.AddPhoneNumber(worker.Phone);
            subject.AddName(worker.DisplayName);
        }
    }

    public async Task ExportAsync(PersonalDataExportContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var worker in await FindWorkersAsync(_workers, context.Subject, cancellationToken))
        {
            var record = context.Add(WorkerProfileKind, worker.ItemId, worker.DisplayName, worker.CreatedUtc);

            record.Fields[nameof(WorkerProfile.DisplayName)] = worker.DisplayName;
            record.Fields[nameof(WorkerProfile.Email)] = worker.Email;
            record.Fields[nameof(WorkerProfile.Phone)] = worker.Phone;
            record.Fields[nameof(WorkerProfile.EmployeeNumber)] = worker.EmployeeNumber;
            record.Fields[nameof(WorkerProfile.JobTitle)] = worker.JobTitle;
            record.Fields[nameof(WorkerProfile.EmploymentType)] = worker.EmploymentType.ToString();
            record.Fields[nameof(WorkerProfile.Status)] = worker.Status.ToString();
            record.Fields[nameof(WorkerProfile.HireDate)] = worker.HireDate?.ToString("O", CultureInfo.InvariantCulture);
            record.Fields[nameof(WorkerProfile.TerminationDate)] = worker.TerminationDate?.ToString("O", CultureInfo.InvariantCulture);
            record.Fields[nameof(WorkerProfile.DateOfBirth)] = worker.DateOfBirth?.ToString("O", CultureInfo.InvariantCulture);
            record.Fields[nameof(WorkerProfile.TimeZoneId)] = worker.TimeZoneId;
        }
    }

    public async Task AnonymizeAsync(PersonalDataAnonymizeContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var worker in await FindWorkersAsync(_workers, context.Subject, cancellationToken))
        {
            // A worker always has a name on the schedule and on timesheets, so it is replaced rather than cleared.
            worker.DisplayName = context.ReplacementText ?? S["Former worker"].Value;
            worker.Email = null;
            worker.Phone = null;
            worker.DateOfBirth = null;
            worker.HomeBase = null;

            await _workers.UpdateAsync(worker, cancellationToken: cancellationToken);
            context.Result.Count(WorkerProfileKind);
        }
    }

    /// <summary>
    /// Returns the worker profiles linked to the person's accounts.
    /// </summary>
    internal static async Task<IReadOnlyList<WorkerProfile>> FindWorkersAsync(IWorkerManager workers, PersonalDataSubject subject, CancellationToken cancellationToken)
    {
        var found = new List<WorkerProfile>();

        foreach (var userId in subject.UserIds)
        {
            var worker = await workers.FindByUserIdAsync(userId, cancellationToken);

            if (worker is not null && found.All(existing => existing.ItemId != worker.ItemId))
            {
                found.Add(worker);
            }
        }

        return found;
    }
}
