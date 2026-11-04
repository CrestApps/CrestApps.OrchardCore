using CrestApps.OrchardCore.Workforce.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Tests.Workforce;

/// <summary>
/// The accrual engine on its own: which periods a policy pays for, tenure rates, the balance cap, carryover at the
/// reset date, the yearly grant, accrual per hour worked, and that a second run owes nothing.
/// </summary>
public sealed class TimeOffAccrualEngineTests
{
    [Fact]
    public void Monthly_PaysEachClosedMonth_FromTheEndOfTheWaitingPeriod_AndOnlyOnce()
    {
        // Arrange (hired 10 January with a 30-day wait, so February is the first month paid)
        var policy = Policy(TimeOffAccrualMethod.PerPeriod, 8, policy => policy.WaitingPeriodDays = 30);
        var input = Input(policy, hired: new DateOnly(2026, 1, 10), date: new DateOnly(2026, 5, 15));

        // Act
        var first = TimeOffAccrualEngine.Compute(input);

        input.Existing = first.ToList();

        var second = TimeOffAccrualEngine.Compute(input);

        // Assert
        Assert.Equal([new DateOnly(2026, 2, 28), new DateOnly(2026, 3, 31), new DateOnly(2026, 4, 30)], first.Select(entry => entry.EffectiveDate));
        Assert.All(first, entry => Assert.Equal(8m, entry.Amount));
        Assert.All(first, entry => Assert.Equal(TimeOffLedgerKind.Accrual, entry.Kind));
        Assert.Equal(3, first.Select(entry => entry.IdempotencyKey).Distinct().Count());
        Assert.Empty(second);
    }

    [Fact]
    public void TenureTier_RaisesTheRate_OnceTheMonthsOfServiceAreReached()
    {
        // Arrange
        var policy = Policy(TimeOffAccrualMethod.PerPeriod, 8, policy => policy.TenureTiers.Add(new TimeOffTenureTier { FromMonths = 12, Rate = 10 }));
        var input = Input(policy, hired: new DateOnly(2025, 1, 1), date: new DateOnly(2026, 2, 1), eligibleFrom: new DateOnly(2025, 11, 1));

        // Act
        var entries = TimeOffAccrualEngine.Compute(input);

        // Assert (30 November and 31 December are under a year; 31 January is a year and a month)
        Assert.Equal([8m, 8m, 10m], entries.Select(entry => entry.Amount));
    }

    [Fact]
    public void MaxBalance_StopsAccrual_AtTheCap_AndRecordsTheCappedPeriod()
    {
        // Arrange (10 hours opened before the first period)
        var policy = Policy(TimeOffAccrualMethod.PerPeriod, 8, policy => policy.MaxBalance = 20);
        var input = Input(policy, hired: new DateOnly(2026, 1, 1), date: new DateOnly(2026, 4, 30));

        input.Existing = [Entry(policy, TimeOffLedgerKind.Opening, 10, new DateOnly(2026, 1, 1))];

        // Act
        var entries = TimeOffAccrualEngine.Compute(input);

        input.Existing = input.Existing.Concat(entries).ToList();

        // Assert
        Assert.Equal([8m, 2m, 0m, 0m], entries.Select(entry => entry.Amount));
        Assert.NotNull(entries[2].Note);
        Assert.Empty(TimeOffAccrualEngine.Compute(input));
    }

    [Fact]
    public void Carryover_ExpiresWhatIsAboveTheLimit_AtTheResetDate()
    {
        // Arrange (12 hours at the end of the year, 5 may carry over)
        var policy = Policy(TimeOffAccrualMethod.PerPeriod, 8, policy => policy.CarryoverMax = 5);
        var input = Input(policy, hired: new DateOnly(2025, 1, 1), date: new DateOnly(2026, 1, 31), eligibleFrom: new DateOnly(2026, 1, 1));

        input.Existing = [Entry(policy, TimeOffLedgerKind.Opening, 12, new DateOnly(2025, 12, 1))];

        // Act
        var entries = TimeOffAccrualEngine.Compute(input);

        // Assert
        Assert.Collection(
            entries,
            expiry =>
            {
                Assert.Equal(TimeOffLedgerKind.Expiry, expiry.Kind);
                Assert.Equal(-7m, expiry.Amount);
                Assert.Equal(new DateOnly(2026, 1, 1), expiry.EffectiveDate);
            },
            accrual =>
            {
                Assert.Equal(TimeOffLedgerKind.Accrual, accrual.Kind);
                Assert.Equal(8m, accrual.Amount);
                Assert.Equal(new DateOnly(2026, 1, 31), accrual.EffectiveDate);
            });
    }

    [Fact]
    public void AnnualLumpSum_GrantsAShare_WhenEligible_ThenTheYearAtTheResetDate()
    {
        // Arrange (eligible 2 July: 183 of the year's 365 days are left)
        var policy = Policy(TimeOffAccrualMethod.AnnualLumpSum, 120);
        var input = Input(policy, hired: new DateOnly(2026, 7, 2), date: new DateOnly(2027, 1, 5));

        // Act
        var entries = TimeOffAccrualEngine.Compute(input);

        // Assert
        Assert.Equal([new DateOnly(2026, 7, 2), new DateOnly(2027, 1, 1)], entries.Select(entry => entry.EffectiveDate));
        Assert.Equal([60.16m, 120m], entries.Select(entry => entry.Amount));
    }

    [Fact]
    public void PerHourWorked_PaysForTheMinutesWorkedInEachPeriod()
    {
        // Arrange (two-week periods from Monday 5 January)
        var policy = Policy(TimeOffAccrualMethod.PerHourWorked, 0.05m, policy =>
        {
            policy.Frequency = TimeOffAccrualFrequency.Biweekly;
            policy.AnchorDate = new DateOnly(2026, 1, 5);
        });
        var input = Input(policy, hired: new DateOnly(2026, 1, 5), date: new DateOnly(2026, 2, 1));

        input.WorkedMinutes = new Dictionary<DateOnly, int>
        {
            [new DateOnly(2026, 1, 18)] = 80 * 60,
            [new DateOnly(2026, 2, 1)] = 72 * 60 + 30,
        };

        // Act
        var entries = TimeOffAccrualEngine.Compute(input);

        // Assert
        Assert.Equal([4m, 3.625m], entries.Select(entry => entry.Amount));
    }

    [Fact]
    public void EligibleTo_StopsAccrual_AfterTheWorkerLeaves()
    {
        // Arrange
        var policy = Policy(TimeOffAccrualMethod.PerPeriod, 8);
        var input = Input(policy, hired: new DateOnly(2026, 1, 1), date: new DateOnly(2026, 6, 30));

        input.EligibleTo = new DateOnly(2026, 3, 31);

        // Act
        var entries = TimeOffAccrualEngine.Compute(input);

        // Assert
        Assert.Equal([new DateOnly(2026, 1, 31), new DateOnly(2026, 2, 28), new DateOnly(2026, 3, 31)], entries.Select(entry => entry.EffectiveDate));
    }

    [Theory]
    [InlineData(TimeOffAccrualFrequency.Weekly, "2026-01-05..2026-01-11 2026-01-12..2026-01-18 2026-01-19..2026-01-25 2026-01-26..2026-02-01")]
    [InlineData(TimeOffAccrualFrequency.Biweekly, "2026-01-05..2026-01-18 2026-01-19..2026-02-01")]
    [InlineData(TimeOffAccrualFrequency.SemiMonthly, "2026-01-01..2026-01-15 2026-01-16..2026-01-31")]
    [InlineData(TimeOffAccrualFrequency.Monthly, "2026-01-01..2026-01-31")]
    public void Periods_EndWithinTheRange(TimeOffAccrualFrequency frequency, string expected)
    {
        // Arrange
        var policy = Policy(TimeOffAccrualMethod.PerPeriod, 1, policy =>
        {
            policy.Frequency = frequency;
            policy.AnchorDate = new DateOnly(2026, 1, 5);
        });

        // Act
        var periods = TimeOffAccrualEngine.Periods(policy, new DateOnly(2026, 1, 5), new DateOnly(2026, 2, 1));

        // Assert
        Assert.Equal(expected, string.Join(' ', periods.Select(period => $"{period.Start:yyyy-MM-dd}..{period.End:yyyy-MM-dd}")));
    }

    private static TimeOffPolicy Policy(TimeOffAccrualMethod method, decimal rate, Action<TimeOffPolicy> configure = null)
    {
        var policy = new TimeOffPolicy
        {
            ItemId = "policy1",
            Name = "Vacation",
            TypeId = "vacation",
            AccrualMethod = method,
            Rate = rate,
            CreatedUtc = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };

        configure?.Invoke(policy);

        return policy;
    }

    private static TimeOffAccrualInput Input(TimeOffPolicy policy, DateOnly hired, DateOnly date, DateOnly? eligibleFrom = null)
    {
        return new TimeOffAccrualInput
        {
            Policy = policy,
            Worker = new WorkerProfile { ItemId = "worker1", DisplayName = "Ana", HireDate = hired },
            EligibleFrom = eligibleFrom ?? hired,
            Date = date,
        };
    }

    private static TimeOffLedgerEntry Entry(TimeOffPolicy policy, TimeOffLedgerKind kind, decimal amount, DateOnly date)
    {
        return new TimeOffLedgerEntry
        {
            ItemId = "entry-" + date.ToString("yyyyMMdd", null),
            WorkerId = "worker1",
            PolicyId = policy.ItemId,
            Kind = kind,
            Amount = amount,
            EffectiveDate = date,
            IdempotencyKey = "opening:" + date.ToString("yyyyMMdd", null),
        };
    }
}
