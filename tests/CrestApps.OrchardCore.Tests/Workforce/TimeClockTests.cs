using CrestApps.Core;
using CrestApps.OrchardCore.Workforce.Core;
using CrestApps.OrchardCore.Workforce.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Workforce;

/// <summary>
/// The time clock's services on a real SQLite store: punches in order and once per key, the policy's sources,
/// geofence and early clock-in, timesheets through submit, approve and lock, payroll exported once, corrections,
/// attendance points, kiosk PINs, and worked time for accrual.
/// </summary>
public sealed class TimeClockTests
{
    private static readonly DateOnly _monday = TimeClockKit.Monday;

    [Fact]
    public async Task Punches_FollowEachOther()
    {
        await using var kit = await TimeClockKit.CreateAsync();
        var team = await kit.AddTeamAsync("Front desk");
        var ana = await PlanningKit.AddMemberAsync(kit.Harness, "Ana", team);

        kit.Now = TimeClockKit.Local(_monday, 9);

        Assert.True((await kit.PunchAsync(ana, TimePunchKind.In)).Succeeded);
        Assert.False((await kit.PunchAsync(ana, TimePunchKind.In)).Succeeded);

        kit.Now = TimeClockKit.Local(_monday, 12);
        Assert.False((await kit.PunchAsync(ana, TimePunchKind.MealEnd)).Succeeded);
        Assert.True((await kit.PunchAsync(ana, TimePunchKind.MealStart)).Succeeded);

        var status = await kit.Harness.RunAsync(services => services.GetRequiredService<ITimeClockService>().GetStatusAsync(ana), commit: false);

        Assert.Equal(TimeClockState.OnMeal, status.State);
        Assert.Equal([TimePunchKind.MealEnd, TimePunchKind.Out], status.NextPunches);
        Assert.Equal(TimeClockKit.Local(_monday, 9), status.SinceUtc);

        kit.Now = TimeClockKit.Local(_monday, 17);
        Assert.True((await kit.PunchAsync(ana, TimePunchKind.Out)).Succeeded);

        var again = await kit.PunchAsync(ana, TimePunchKind.Out);

        Assert.Equal(["Ana isn't clocked in."], again.Errors);
    }

    [Fact]
    public async Task AWorkerWhoForgotToClockOut_CanClockInTheNextDay()
    {
        await using var kit = await TimeClockKit.CreateAsync();
        var team = await kit.AddTeamAsync("Front desk");
        var ana = await PlanningKit.AddMemberAsync(kit.Harness, "Ana", team);

        kit.Now = TimeClockKit.Local(_monday, 9);
        await kit.PunchAsync(ana, TimePunchKind.In);
        kit.Now = TimeClockKit.Local(_monday.AddDays(1), 9);

        Assert.True((await kit.PunchAsync(ana, TimePunchKind.In)).Succeeded);
    }

    [Fact]
    public async Task APunchSentTwiceWithItsKey_CountsOnce_SoContactCenterAgentsNeverPunchTwice()
    {
        await using var kit = await TimeClockKit.CreateAsync();
        var team = await kit.AddTeamAsync("Agents", policy => policy.AllowedSources = [TimePunchSource.ContactCenter]);
        var ana = await PlanningKit.AddMemberAsync(kit.Harness, "Ana", team);

        async Task<TimePunch> SignInOrOut(TimePunchKind kind, string eventId, int hour)
        {
            kit.Now = TimeClockKit.Local(_monday, hour);

            var result = await kit.PunchAsync(ana, kind, request =>
            {
                request.Source = TimePunchSource.ContactCenter;
                request.IdempotencyKey = eventId;
            });

            Assert.True(result.Succeeded, string.Join(" ", result.Errors));

            return result.Value;
        }

        var signIn = await SignInOrOut(TimePunchKind.In, "event-1", 8);
        var replayed = await SignInOrOut(TimePunchKind.In, "event-1", 12);

        await SignInOrOut(TimePunchKind.Out, "event-2", 16);
        await SignInOrOut(TimePunchKind.Out, "event-2", 17);

        var web = await kit.PunchAsync(ana, TimePunchKind.In);

        kit.Now = TimeClockKit.Local(_monday.AddDays(1), 12);

        var timesheet = await kit.TimesheetAsync(ana);

        Assert.Equal(signIn.ItemId, replayed.ItemId);
        Assert.Equal(8m, kit.Hours(timesheet, "REG"));
        Assert.False(web.Succeeded);
    }

    [Fact]
    public async Task TheGeofence_NeedsALocation_AndAPunchAwayFromTheSite_IsAnException()
    {
        await using var kit = await TimeClockKit.CreateAsync();
        var team = await kit.AddTeamAsync("Warehouse", policy =>
        {
            policy.RequireGeofence = true;
            policy.GeofenceRadiusMeters = 200;
        });

        team = await PlanningKit.UpdateTeamAsync(kit.Harness, team, stored => stored.Location = new WorkforceLocation { Latitude = 40.7128, Longitude = -74.0060 });

        var ana = await PlanningKit.AddMemberAsync(kit.Harness, "Ana", team);

        kit.Now = TimeClockKit.Local(_monday, 9);

        var noLocation = await kit.PunchAsync(ana, TimePunchKind.In);
        var away = await kit.PunchAsync(ana, TimePunchKind.In, request =>
        {
            request.Latitude = 40.7306;
            request.Longitude = -73.9352;
        });

        kit.Now = TimeClockKit.Local(_monday, 17);

        var atSite = await kit.PunchAsync(ana, TimePunchKind.Out, request =>
        {
            request.Latitude = 40.7130;
            request.Longitude = -74.0062;
        });

        await kit.TimesheetAsync(ana);

        Assert.False(noLocation.Succeeded);
        Assert.False(away.Value.WithinGeofence);
        Assert.True(atSite.Value.WithinGeofence);
        Assert.Equal([AttendanceExceptionKind.OutsideGeofence], (await kit.ExceptionsAsync(ana)).Select(exception => exception.Kind));
    }

    [Fact]
    public async Task ClockingInLongBeforeTheShift_IsRefused()
    {
        await using var kit = await TimeClockKit.CreateAsync(services => services.AddWorkforceTimeOffCore());
        var team = await kit.AddTeamAsync("Front desk", policy => policy.BlockEarlyPunchInMinutes = 30);
        var ana = await PlanningKit.AddMemberAsync(kit.Harness, "Ana", team);
        var period = await PlanningKit.CreatePeriodAsync(kit.Harness, team);

        await PlanningKit.AddDraftAsync(kit.Harness, period, ana.ItemId, _monday, new TimeOnly(9, 0), new TimeOnly(17, 0));
        await PlanningKit.PublishAsync(kit.Harness, period);

        kit.Now = TimeClockKit.Local(_monday, 8);

        var early = await kit.PunchAsync(ana, TimePunchKind.In);

        kit.Now = TimeClockKit.Local(_monday, 8, 35);

        var inTime = await kit.PunchAsync(ana, TimePunchKind.In);

        Assert.Equal(["It's too early to clock in. You can clock in from 8:30 AM."], early.Errors);
        Assert.True(inTime.Succeeded);
    }

    [Fact]
    public async Task ATimesheet_IsSubmitted_Approved_Locked_AndExportedOnce()
    {
        await using var kit = await TimeClockKit.CreateAsync();
        var team = await kit.AddTeamAsync("Front desk");
        var ana = await PlanningKit.AddMemberAsync(kit.Harness, "Ana", team, worker => worker.ExternalId = "E-100");

        for (var day = 0; day < 5; day++)
        {
            await kit.WorkAsync(ana, _monday.AddDays(day), 8, 17);
        }

        kit.Now = TimeClockKit.Local(_monday.AddDays(6), 12);

        var timesheet = await kit.TimesheetAsync(ana);

        Assert.Equal((_monday.AddDays(-1), _monday.AddDays(5)), (timesheet.PeriodStart, timesheet.PeriodEnd));
        Assert.Equal(40m, kit.Hours(timesheet, "REG"));
        Assert.Equal(5m, kit.Hours(timesheet, "OT"));

        var service = (Func<IServiceProvider, ITimesheetService>)(services => services.GetRequiredService<ITimesheetService>());
        var unattested = await kit.Harness.RunAsync(services => service(services).SubmitAsync(timesheet.ItemId, " ", "ana"));
        var submitted = await kit.Harness.RunAsync(services => service(services).SubmitAsync(timesheet.ItemId, "These hours are right.", "ana"));
        var lateEdit = await kit.PunchAsync(ana, TimePunchKind.In, request =>
        {
            request.Source = TimePunchSource.Manager;
            request.Utc = TimeClockKit.Local(_monday.AddDays(5), 8);
        });
        var sentBack = await kit.Harness.RunAsync(services => service(services).RejectAsync(timesheet.ItemId, "Friday looks long.", "manager"));
        var resubmitted = await kit.Harness.RunAsync(services => service(services).SubmitAsync(timesheet.ItemId, "Checked.", "ana"));
        var approved = await kit.Harness.RunAsync(services => service(services).ApproveAsync(timesheet.ItemId, "manager"));
        var locked = await kit.Harness.RunAsync(services => service(services).LockPeriodAsync(kit.Calendar.ItemId, timesheet.PeriodStart, "payroll"));
        var reopened = await kit.Harness.RunAsync(services => service(services).ReopenAsync(timesheet.ItemId, "manager"));

        Assert.False(unattested.Succeeded);
        Assert.True(submitted.Succeeded);
        Assert.False(lateEdit.Succeeded);
        Assert.Equal(TimesheetStatus.Rejected, sentBack.Value.Status);
        Assert.True(resubmitted.Succeeded);
        Assert.Equal(TimesheetStatus.Approved, approved.Value.Status);
        Assert.Equal([timesheet.ItemId], locked.Select(item => item.ItemId));
        Assert.False(reopened.Succeeded);

        var export = (Func<IServiceProvider, IPayrollExportService>)(services => services.GetRequiredService<IPayrollExportService>());
        var first = await kit.Harness.RunAsync(services => export(services).RunAsync(CsvPayrollExportProvider.ProviderName, kit.Calendar.ItemId, timesheet.PeriodStart, false, "payroll"));
        var second = await kit.Harness.RunAsync(services => export(services).RunAsync(CsvPayrollExportProvider.ProviderName, kit.Calendar.ItemId, timesheet.PeriodStart, false, "payroll"));
        var again = await kit.Harness.RunAsync(services => export(services).RunAsync(CsvPayrollExportProvider.ProviderName, kit.Calendar.ItemId, timesheet.PeriodStart, true, "payroll"));

        Assert.True(first.Succeeded, string.Join(" ", first.Errors));
        Assert.Equal(
            "EmployeeId,Name,PeriodStart,PeriodEnd,PayCode,Hours,Rate,Amount\r\n"
            + "E-100,Ana,2026-11-08,2026-11-14,OT,5.00,,\r\n"
            + "E-100,Ana,2026-11-08,2026-11-14,REG,40.00,,\r\n",
            first.Value.Content);
        Assert.Equal(2, first.Value.RowCount);
        Assert.False(second.Succeeded);
        Assert.True(again.Succeeded);
        Assert.NotEqual(first.Value.ItemId, again.Value.ItemId);
    }

    [Fact]
    public async Task APeriodStillRunning_IsntLocked()
    {
        await using var kit = await TimeClockKit.CreateAsync();
        var team = await kit.AddTeamAsync("Front desk");
        var ana = await PlanningKit.AddMemberAsync(kit.Harness, "Ana", team);

        await kit.WorkAsync(ana, _monday, 8, 17);
        kit.Now = TimeClockKit.Local(_monday.AddDays(5), 12);

        var timesheet = await kit.TimesheetAsync(ana);
        var service = (Func<IServiceProvider, ITimesheetService>)(services => services.GetRequiredService<ITimesheetService>());

        await kit.Harness.RunAsync(services => service(services).ApproveAsync(timesheet.ItemId, "manager"));

        var early = await kit.Harness.RunAsync(services => service(services).LockPeriodAsync(kit.Calendar.ItemId, timesheet.PeriodStart, "payroll"));

        kit.Now = TimeClockKit.Local(_monday.AddDays(6), 0, 30);

        var ended = await kit.Harness.RunAsync(services => service(services).LockPeriodAsync(kit.Calendar.ItemId, timesheet.PeriodStart, "payroll"));

        Assert.Empty(early);
        Assert.Single(ended);
    }

    [Fact]
    public async Task ThePayrollFile_UsesTheColumnsSet_RatesAndMultipliers_AndKeepsFormulasOut()
    {
        await using var kit = await TimeClockKit.CreateAsync();
        var team = await kit.AddTeamAsync("Front desk", policy => policy.WeeklyOvertimeHours = 8);
        var ana = await PlanningKit.AddMemberAsync(kit.Harness, "=Ana", team, worker =>
        {
            worker.EmployeeNumber = "100";
            worker.Put(new WorkerPayRates { Rates = [new WorkerPayRate { EffectiveFrom = new DateOnly(2026, 1, 1), HourlyRate = 20 }] });
        });

        await kit.WorkAsync(ana, _monday, 8, 18);
        kit.Now = TimeClockKit.Local(_monday.AddDays(6), 12);

        var timesheet = await kit.TimesheetAsync(ana);

        await kit.Harness.RunAsync(async services =>
        {
            var export = services.GetRequiredService<IPayrollExportService>();

            await export.SaveCsvSettingsAsync(new PayrollCsvSettings
            {
                Columns =
                [
                    new PayrollCsvColumn { Header = "Badge", Field = PayrollCsvField.EmployeeNumber },
                    new PayrollCsvColumn { Header = "Who", Field = PayrollCsvField.WorkerName },
                    new PayrollCsvColumn { Header = "Day", Field = PayrollCsvField.Date },
                    new PayrollCsvColumn { Header = "Code", Field = PayrollCsvField.PayCode },
                    new PayrollCsvColumn { Header = "Pay", Field = PayrollCsvField.Amount },
                ],
            });
            await services.GetRequiredService<ITimesheetService>().ApproveAsync(timesheet.ItemId, "manager");
            await services.GetRequiredService<ITimesheetService>().LockPeriodAsync(kit.Calendar.ItemId, timesheet.PeriodStart, "payroll");
        });

        var batch = await kit.Harness.RunAsync(services => services.GetRequiredService<IPayrollExportService>().RunAsync(CsvPayrollExportProvider.ProviderName, kit.Calendar.ItemId, timesheet.PeriodStart, false, "payroll"));

        Assert.Equal(
            "Badge,Who,Day,Code,Pay\r\n"
            + "100,'=Ana,2026-11-09,OT,60.00\r\n"
            + "100,'=Ana,2026-11-09,REG,160.00\r\n",
            batch.Value.Content);
    }

    [Fact]
    public async Task AMissedClockOut_IsRaised_AndAnApprovedCorrection_FixesIt()
    {
        await using var kit = await TimeClockKit.CreateAsync();
        var team = await kit.AddTeamAsync("Front desk");
        var ana = await PlanningKit.AddMemberAsync(kit.Harness, "Ana", team);

        await kit.PunchAtAsync(ana, TimePunchKind.In, _monday, 9);
        kit.Now = TimeClockKit.Local(_monday.AddDays(2), 9);

        var before = await kit.TimesheetAsync(ana);
        var missed = await kit.ExceptionsAsync(ana);
        var correction = await kit.Harness.RunAsync(services => services.GetRequiredService<IAttendanceService>().RequestCorrectionAsync(ana, TimePunchKind.Out, TimeClockKit.Local(_monday, 17), "Forgot to clock out."));
        var pending = await kit.Harness.RunAsync(services => services.GetRequiredService<IAttendanceService>().ListPendingCorrectionsAsync([team.ItemId], null), commit: false);
        var decided = await kit.Harness.RunAsync(services => services.GetRequiredService<IAttendanceService>().DecideCorrectionAsync(correction.Value.ItemId, true, "manager"));
        var after = await kit.TimesheetAsync(ana);

        Assert.Equal(0m, kit.Hours(before, "REG"));
        Assert.Equal(1, before.OpenExceptions);
        Assert.Equal([AttendanceExceptionKind.MissedPunch], missed.Select(exception => exception.Kind));
        Assert.Equal([correction.Value.ItemId], pending.Select(item => item.ItemId));
        Assert.True(decided.Succeeded, string.Join(" ", decided.Errors));
        Assert.Equal(8m, kit.Hours(after, "REG"));
        Assert.Equal(0, after.OpenExceptions);
        Assert.Empty(await kit.ExceptionsAsync(ana));
    }

    [Fact]
    public async Task AManagerCorrectsAndRemovesPunches_KeepingTheOriginals()
    {
        await using var kit = await TimeClockKit.CreateAsync();
        var team = await kit.AddTeamAsync("Front desk");
        var ana = await PlanningKit.AddMemberAsync(kit.Harness, "Ana", team);
        var clockIn = await kit.PunchAtAsync(ana, TimePunchKind.In, _monday, 9);
        var clockOut = await kit.PunchAtAsync(ana, TimePunchKind.Out, _monday, 17);
        var extraIn = await kit.PunchAtAsync(ana, TimePunchKind.In, _monday, 18);

        kit.Now = TimeClockKit.Local(_monday.AddDays(3), 9);

        var service = (Func<IServiceProvider, ITimeClockService>)(services => services.GetRequiredService<ITimeClockService>());
        var noReason = await kit.Harness.RunAsync(services => service(services).CorrectAsync(clockIn.ItemId, TimePunchKind.In, TimeClockKit.Local(_monday, 8), "", "manager"));
        var corrected = await kit.Harness.RunAsync(services => service(services).CorrectAsync(clockIn.ItemId, TimePunchKind.In, TimeClockKit.Local(_monday, 8), "Started early to open up.", "manager"));
        var twice = await kit.Harness.RunAsync(services => service(services).CorrectAsync(clockIn.ItemId, TimePunchKind.In, TimeClockKit.Local(_monday, 7), "Again.", "manager"));
        var removed = await kit.Harness.RunAsync(services => service(services).RemoveAsync(extraIn.ItemId, "Clocked in by mistake.", "manager"));
        var punches = await kit.Harness.RunAsync(services => service(services).ListAsync(ana.ItemId, TimeClockKit.Local(_monday, 0), TimeClockKit.Local(_monday.AddDays(1), 0)), commit: false);
        var timesheet = await kit.TimesheetAsync(ana);

        Assert.False(noReason.Succeeded);
        Assert.Equal(clockIn.ItemId, corrected.Value.ReplacesPunchId);
        Assert.False(twice.Succeeded);
        Assert.True(removed.Value.Removes);
        Assert.Equal([corrected.Value.ItemId, clockOut.ItemId], punches.Select(punch => punch.ItemId));
        Assert.Equal(9m, kit.Hours(timesheet, "REG"));
        Assert.Empty(await kit.ExceptionsAsync(ana));
    }

    [Fact]
    public async Task ExcusedExceptions_StayExcused_AndPointsReachingTheThreshold_AreHeardOnce()
    {
        var handler = new RecordingPointsHandler();
        await using var kit = await TimeClockKit.CreateAsync(services => services.AddSingleton<IAttendancePointsHandler>(handler));
        var team = await kit.AddTeamAsync("Front desk", policy =>
        {
            policy.AttendancePoints = [new AttendancePointRule { Kind = AttendanceExceptionKind.MissedPunch, Points = 1 }];
            policy.PointsThreshold = 2;
        });
        var ana = await PlanningKit.AddMemberAsync(kit.Harness, "Ana", team);

        await kit.PunchAtAsync(ana, TimePunchKind.In, _monday, 9);
        await kit.PunchAtAsync(ana, TimePunchKind.In, _monday.AddDays(1), 9);
        kit.Now = TimeClockKit.Local(_monday.AddDays(2), 12);
        await kit.TimesheetAsync(ana);

        var exceptions = await kit.ExceptionsAsync(ana);
        var first = exceptions.Single(exception => exception.Date == _monday);
        var excused = await kit.Harness.RunAsync(services => services.GetRequiredService<IAttendanceService>().ExcuseAsync(first.ItemId, "System outage.", "manager"));
        var points = await kit.Harness.RunAsync(services => services.GetRequiredService<IAttendanceService>().GetPointsAsync(ana, _monday.AddDays(2)), commit: false);

        await kit.TimesheetAsync(ana);

        Assert.Equal(2, exceptions.Count);
        Assert.Equal([2m], handler.Points);
        Assert.True(excused.Succeeded);
        Assert.Equal(1m, points);
        Assert.Equal(AttendanceExceptionStatus.Excused, (await kit.ExceptionsAsync(ana)).Single(exception => exception.Date == _monday).Status);
        Assert.Equal([2m], handler.Points);
    }

    [Fact]
    public async Task TheKiosk_KnowsWorkersByBadgeAndPin_AndLocksOutAfterFiveWrongPins()
    {
        await using var kit = await TimeClockKit.CreateAsync();
        var team = await kit.AddTeamAsync("Warehouse");
        var noBadge = await PlanningKit.AddMemberAsync(kit.Harness, "Ben", team);
        var ana = await PlanningKit.AddMemberAsync(kit.Harness, "Ana", team, worker => worker.EmployeeNumber = "4410");
        var service = (Func<IServiceProvider, ITimeClockService>)(services => services.GetRequiredService<ITimeClockService>());

        var needsBadge = await kit.Harness.RunAsync(services => service(services).SetPinAsync(noBadge, "1234"));
        var tooShort = await kit.Harness.RunAsync(services => service(services).SetPinAsync(ana, "12"));
        var set = await kit.Harness.RunAsync(services => service(services).SetPinAsync(ana, "2468"));
        var known = await kit.Harness.RunAsync(services => service(services).IdentifyAsync("4410", "2468"));

        for (var attempt = 0; attempt < 5; attempt++)
        {
            Assert.False((await kit.Harness.RunAsync(services => service(services).IdentifyAsync("4410", "0000"))).Succeeded);
        }

        var lockedOut = await kit.Harness.RunAsync(services => service(services).IdentifyAsync("4410", "2468"));

        kit.Now = kit.Now.AddMinutes(16);

        var afterwards = await kit.Harness.RunAsync(services => service(services).IdentifyAsync("4410", "2468"));

        Assert.False(needsBadge.Succeeded);
        Assert.False(tooShort.Succeeded);
        Assert.True(set.Succeeded);
        Assert.Equal(ana.ItemId, known.Value.ItemId);
        Assert.Equal(["Too many wrong PINs. Try again in a few minutes, or ask a manager."], lockedOut.Errors);
        Assert.True(afterwards.Succeeded);

        var kiosk = await kit.Harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<ITimeClockKioskManager>();
            var item = await manager.NewAsync();

            item.Name = "Back door";
            await manager.CreateAsync(item);

            return item;
        });
        var token = await kit.Harness.RunAsync(services => service(services).RenewKioskTokenAsync(kiosk));
        var found = await kit.Harness.RunAsync(services => service(services).FindKioskAsync(token), commit: false);
        var renewed = await kit.Harness.RunAsync(services => service(services).RenewKioskTokenAsync(kiosk));
        var old = await kit.Harness.RunAsync(services => service(services).FindKioskAsync(token), commit: false);

        Assert.Equal(kiosk.ItemId, found.ItemId);
        Assert.Null(old);
        Assert.NotEqual(token, renewed);
    }

    [Fact]
    public async Task ApprovedPredictabilityPay_IsPaid_AndMarkedExported()
    {
        await using var kit = await TimeClockKit.CreateAsync(services => services.AddWorkforceLaborRulesCore());
        var team = await kit.AddTeamAsync("Front desk");
        var ana = await PlanningKit.AddMemberAsync(kit.Harness, "Ana", team);
        var entry = new PredictabilityPayEntry
        {
            ItemId = "pp1",
            WorkerId = ana.ItemId,
            TeamId = team.ItemId,
            ShiftStartUtc = TimeClockKit.Local(_monday.AddDays(2), 9),
            Hours = 2,
            Status = PredictabilityPayStatus.Approved,
        };

        await kit.Harness.RunAsync(services => services.GetRequiredService<PredictabilityPayStore>().CreateAsync(entry).AsTask());
        kit.Now = TimeClockKit.Local(_monday.AddDays(6), 12);

        var timesheet = await kit.TimesheetAsync(ana);

        await kit.Harness.RunAsync(async services =>
        {
            await services.GetRequiredService<ITimesheetService>().ApproveAsync(timesheet.ItemId, "manager");
            await services.GetRequiredService<ITimesheetService>().LockPeriodAsync(kit.Calendar.ItemId, timesheet.PeriodStart, "payroll");
        });
        await kit.Harness.RunAsync(services => services.GetRequiredService<IPayrollExportService>().RunAsync(CsvPayrollExportProvider.ProviderName, kit.Calendar.ItemId, timesheet.PeriodStart, false, "payroll"));

        var stored = await kit.Harness.RunAsync(services => services.GetRequiredService<PredictabilityPayStore>().FindByIdAsync("pp1").AsTask(), commit: false);

        Assert.Equal(2m, kit.Hours(timesheet, "PP", _monday.AddDays(2)));
        Assert.Equal(PredictabilityPayStatus.Exported, stored.Status);
    }

    [Fact]
    public async Task WorkedTime_ForAccrual_ComesFromPunches()
    {
        await using var kit = await TimeClockKit.CreateAsync(services => services.AddWorkforceTimeOffCore());
        var team = await kit.AddTeamAsync("Front desk");
        var ana = await PlanningKit.AddMemberAsync(kit.Harness, "Ana", team);

        await kit.WorkAsync(ana, _monday, 9, 15);
        await kit.WorkAsync(ana, _monday.AddDays(1), 22, 26);
        kit.Now = TimeClockKit.Local(_monday.AddDays(3), 9);

        var minutes = await kit.Harness.RunAsync(services => services.GetRequiredService<IWorkedTimeSource>().GetWorkedMinutesAsync(ana, _monday, _monday.AddDays(1)), commit: false);

        Assert.Equal((6 * 60) + (2 * 60), minutes);
    }

    [Fact]
    public async Task ATeamWithoutAPolicy_HasNoTimeClock()
    {
        await using var kit = await TimeClockKit.CreateAsync();
        var team = await WorkforceTests.AddTeamAsync(kit.Harness, "Volunteers", TeamKind.Team);
        var ana = await PlanningKit.AddMemberAsync(kit.Harness, "Ana", team);

        var punch = await kit.PunchAsync(ana, TimePunchKind.In);
        var timesheet = await kit.Harness.RunAsync(services => services.GetRequiredService<ITimesheetService>().GetForDateAsync(ana, _monday));

        Assert.Equal(["Ana's team has no time clock policy, so their time isn't clocked."], punch.Errors);
        Assert.False(timesheet.Succeeded);
    }

    private sealed class RecordingPointsHandler : IAttendancePointsHandler
    {
        public List<decimal> Points { get; } = [];

        public Task ThresholdReachedAsync(WorkerProfile worker, AttendanceException exception, decimal points, TimeClockPolicy policy, CancellationToken cancellationToken = default)
        {
            Points.Add(points);

            return Task.CompletedTask;
        }
    }
}
