using System.Linq.Expressions;
using CrestApps.OrchardCore.Calendars.Core.Indexes;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using YesSql;
using YesSql.Services;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Calendars.Reports.DataSources;

/// <summary>
/// One row per calendar the principal may read: its name, type, owner, visibility and settings. Calendars the
/// principal can only see as busy time are left out. The private feed token is never exposed.
/// </summary>
internal sealed class CalendarsReportDataSet : IndexedReportDataSet<Calendar, CalendarIndex>
{
    private readonly CalendarReportAccess _access;

    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarsReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session calendars are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="access">Works out which calendars the principal may read.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CalendarsReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        CalendarReportAccess access,
        IStringLocalizer<CalendarsReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                CalendarReportDataSets.Calendars,
                stringLocalizer["Calendars"],
                stringLocalizer["One row per calendar you can read: its name, type, owner, visibility and settings."]),
            session,
            authorizationService,
            CalendarsConstants.CollectionName,
            CalendarPermissions.AccessCalendars)
    {
        _access = access;

        var S = stringLocalizer;
        var calendar = S["Calendar"].Value;
        var dates = S["Dates"].Value;

        AddField(CalendarReportDataSets.ItemIdField, S["Calendar ID"], DataFieldType.Text, record => record.ItemId, calendar).Identifier();
        AddField(nameof(Calendar.Name), S["Name"], DataFieldType.Text, record => record.Name, calendar);
        AddField(nameof(Calendar.Type), S["Type"], DataFieldType.Text, record => record.Type, calendar).Description = S["The calendar type, such as General, Personal, Team, Room or Office."].Value;
        AddField(nameof(Calendar.OwnerKind), S["Owner kind"], DataFieldType.Text, record => record.OwnerKind, calendar).Description = S["None, User, Role or Resource."].Value;
        AddField(nameof(Calendar.OwnerId), S["Owner"], DataFieldType.Text, record => record.OwnerId, calendar).Description = S["The user ID, role name or resource the calendar belongs to, by its owner kind."].Value;
        AddField("OwnerUserId", S["Owner user ID"], DataFieldType.Text, record => record.OwnerKind == CalendarOwnerKind.User ? record.OwnerId : null, calendar)
            .Identifier(ReportDataReferences.User())
            .Description = S["The user the calendar belongs to; empty unless a user owns it."].Value;
        AddField(nameof(Calendar.Visibility), S["Visibility"], DataFieldType.Text, record => record.Visibility, calendar).Description = S["Private, Shared or Public."].Value;
        AddField("ShareCount", S["Shared with"], DataFieldType.Integer, record => record.Shares?.Count ?? 0, calendar).Description = S["How many users and roles it is shared with."].Value;
        AddField(nameof(Calendar.Enabled), S["Enabled"], DataFieldType.Boolean, record => record.Enabled, calendar);
        AddField(nameof(Calendar.TimeZoneId), S["Time zone"], DataFieldType.Text, record => record.TimeZoneId, calendar);
        AddField(nameof(Calendar.ConflictPolicy), S["Conflicts"], DataFieldType.Text, record => record.ConflictPolicy, calendar).Description = S["What happens when events overlap."].Value;
        AddField(nameof(Calendar.Capacity), S["Capacity"], DataFieldType.Integer, record => record.Capacity, calendar).Description = S["How many events may overlap when the calendar limits them."].Value;
        AddField(nameof(Calendar.Source), S["Source"], DataFieldType.Text, record => record.Source, calendar).Description = S["The module that owns the calendar; empty for calendars made on the calendar screens."].Value;

        AddField(nameof(Calendar.CreatedUtc), S["Created"], DataFieldType.DateTime, record => record.CreatedUtc, dates);
        AddField(nameof(Calendar.ModifiedUtc), S["Last changed"], DataFieldType.DateTime, record => record.ModifiedUtc, dates);
        AddField(nameof(Calendar.CreatedBy), S["Created by"], DataFieldType.Text, record => record.CreatedBy, dates).Identifier(ReportDataReferences.User());

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override bool CanAggregate => false;

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<CalendarIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<CalendarIndex, string>>>(StringComparer.Ordinal)
        {
            [CalendarReportDataSets.ItemIdField] = index => index.ItemId,
        };

    /// <inheritdoc/>
    protected override async Task<Func<IQueryIndex<CalendarIndex>, IQueryIndex<CalendarIndex>>> RestrictAsync(
        DataSourceContext context,
        CancellationToken cancellationToken)
    {
        var scope = await _access.GetScopeAsync(context?.User, cancellationToken);

        if (scope is null)
        {
            return null;
        }

        if (scope.EveryCalendar)
        {
            return rows => rows;
        }

        var ids = scope.CalendarIds.ToArray();

        return rows => rows.Where(index => index.ItemId.IsIn(ids));
    }
}
