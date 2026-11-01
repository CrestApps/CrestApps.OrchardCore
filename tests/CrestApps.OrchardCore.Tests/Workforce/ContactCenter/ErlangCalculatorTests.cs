using CrestApps.OrchardCore.Workforce.ContactCenter.Services.Forecasting;

namespace CrestApps.OrchardCore.Tests.Workforce.ContactCenter;

/// <summary>
/// Erlang C and Erlang A against hand-worked values: the probability of waiting, the service level, the speed of answer
/// and the fewest agents that meet a goal.
/// </summary>
public sealed class ErlangCalculatorTests
{
    [Fact]
    public void ErlangC_MatchesTheFormula()
    {
        // A = 2 Erlangs on 3 agents: (8/6 × 3) / (1 + 2 + 2 + 4) = 4/9.
        Assert.Equal(4d / 9d, ErlangCalculator.ProbabilityOfWaiting(3, 2), 10);
        Assert.Equal(1d, ErlangCalculator.ProbabilityOfWaiting(2, 2));
        Assert.Equal(0.28527, ErlangCalculator.ProbabilityOfWaiting(13, 10), 4);
    }

    [Fact]
    public void TheClassicExample_Needs14Agents()
    {
        // 100 calls in 30 minutes, 3 minutes each: 10 Erlangs. 80% in 20 seconds needs 14 agents; 13 reach 79.6%.
        var staffing = ErlangCalculator.Staff(new StaffingRequest
        {
            Volume = 100,
            IntervalSeconds = 1800,
            AverageHandleSeconds = 180,
            ServiceLevelPercent = 80,
            ServiceLevelSeconds = 20,
        });

        Assert.Equal(14, staffing.Agents);
        Assert.Equal(0.8884, staffing.ServiceLevel, 3);
        Assert.Equal(7.84, staffing.AverageSpeedOfAnswerSeconds, 1);
        Assert.Equal(10d / 14d, staffing.Occupancy, 6);
        Assert.Equal(0.7956, ErlangCalculator.ServiceLevel(13, 10, 180, 20), 3);
    }

    [Fact]
    public void AnAnswerTimeGoal_AndAnOccupancyCap_AreMet()
    {
        var byAsa = ErlangCalculator.Staff(new StaffingRequest { Volume = 100, IntervalSeconds = 1800, AverageHandleSeconds = 180, AverageSpeedOfAnswerSeconds = 5 });
        var capped = ErlangCalculator.Staff(new StaffingRequest { Volume = 100, IntervalSeconds = 1800, AverageHandleSeconds = 180, ServiceLevelPercent = 80, ServiceLevelSeconds = 20, MaxOccupancyPercent = 65 });

        Assert.True(byAsa.AverageSpeedOfAnswerSeconds <= 5);
        Assert.True(ErlangCalculator.AverageSpeedOfAnswer(byAsa.Agents - 1, 10, 180) > 5);
        Assert.Equal(16, capped.Agents);
    }

    [Fact]
    public void Chats_DivideTheHandleTimeByTheConcurrency()
    {
        var voice = ErlangCalculator.Staff(new StaffingRequest { Volume = 100, IntervalSeconds = 1800, AverageHandleSeconds = 360, ServiceLevelPercent = 80, ServiceLevelSeconds = 20 });
        var chat = ErlangCalculator.Staff(new StaffingRequest { Volume = 100, IntervalSeconds = 1800, AverageHandleSeconds = 360, ServiceLevelPercent = 80, ServiceLevelSeconds = 20, Concurrency = 2 });

        Assert.Equal(14, chat.Agents);
        Assert.True(voice.Agents > chat.Agents);
    }

    [Fact]
    public void NoWork_NeedsNoAgents()
    {
        Assert.Equal(0, ErlangCalculator.Staff(new StaffingRequest { Volume = 0, IntervalSeconds = 900, AverageHandleSeconds = 300, ServiceLevelPercent = 80, ServiceLevelSeconds = 20 }).Agents);
    }

    [Fact]
    public void ErlangA_ApproachesErlangC_AsPatienceGrows_AndNeedsNoMoreAgents()
    {
        var erlangC = ErlangCalculator.ProbabilityOfWaiting(13, 10);
        var patient = ErlangCalculator.ErlangA(13, 10, 180, patienceSeconds: 1_000_000);
        var impatient = ErlangCalculator.ErlangA(13, 10, 180, patienceSeconds: 60);

        Assert.Equal(erlangC, patient.ProbabilityOfWaiting, 3);
        Assert.True(patient.AbandonRate < 0.001);
        Assert.True(impatient.AbandonRate > 0.01);
        Assert.True(impatient.ProbabilityOfWaiting < erlangC);

        var withPatience = ErlangCalculator.Staff(new StaffingRequest { Volume = 100, IntervalSeconds = 1800, AverageHandleSeconds = 180, ServiceLevelPercent = 80, ServiceLevelSeconds = 20, PatienceSeconds = 60 });

        Assert.True(withPatience.Agents <= 14);
        Assert.True(withPatience.AbandonRate > 0);
    }
}
