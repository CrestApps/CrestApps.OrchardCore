using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Over-dialing places calls before any agent is reserved, so every call it places beyond the free agents is a bet that
/// somebody will not answer. A lost bet is a person who answers and hears the abandoned-call message, which regulators
/// cap. These pin the calculation that sizes the bet: it never exceeds a limit, it backs off as the measured abandonment
/// climbs and as answers become likelier, and it refuses to bet at all whenever a measurement is missing or too thin.
/// </summary>
public sealed class PredictiveOverDialCalculatorTests
{
    private const double Tolerance = 1e-12;

    [Fact]
    public void Calculate_WhenThePolicyDoesNotPermitDialing_PlacesNothing()
    {
        // Arrange
        var input = Input();
        input.PolicyPermitted = false;

        // Act
        var decision = PredictiveOverDialCalculator.Calculate(input);

        // Assert
        Assert.Equal(PredictivePacingDecisionMode.Suppressed, decision.Mode);
        Assert.Equal(PredictivePacingReason.PolicySuppressed, decision.Reason);
        Assert.Equal(0, decision.DialCount);
    }

    public static TheoryData<string, PredictivePacingReason> FailClosedCases => new()
    {
        { "no answer rate", PredictivePacingReason.AnswerRateUnavailable },
        { "answer rate is not a number", PredictivePacingReason.AnswerRateUnavailable },
        { "answer rate is negative", PredictivePacingReason.AnswerRateUnavailable },
        { "answer rate above one", PredictivePacingReason.AnswerRateUnavailable },
        { "answer rate sample below floor", PredictivePacingReason.AnswerRateSampleBelowFloor },
        { "no rolling abandonment rate", PredictivePacingReason.AbandonmentRateUnavailable },
        { "rolling abandonment rate is infinite", PredictivePacingReason.AbandonmentRateUnavailable },
        { "abandonment sample below floor", PredictivePacingReason.AbandonmentSampleBelowFloor },
        { "no compliance rate", PredictivePacingReason.ComplianceRateUnavailable },
        { "compliance rate at cap", PredictivePacingReason.ComplianceRateAtCap },
        { "compliance rate over cap", PredictivePacingReason.ComplianceRateAtCap },
        { "rolling rate at cap", PredictivePacingReason.RollingRateAtCap },
        { "rolling rate over cap", PredictivePacingReason.RollingRateAtCap },
        { "no cap enforced", PredictivePacingReason.InvalidSettings },
        { "cap over one hundred", PredictivePacingReason.InvalidSettings },
        { "target at cap", PredictivePacingReason.InvalidSettings },
        { "target above cap", PredictivePacingReason.InvalidSettings },
        { "target zero", PredictivePacingReason.InvalidSettings },
        { "lines per agent below one", PredictivePacingReason.InvalidSettings },
        { "lines per agent not a number", PredictivePacingReason.InvalidSettings },
        { "no calls in flight allowed", PredictivePacingReason.InvalidSettings },
        { "no dials per cycle allowed", PredictivePacingReason.InvalidSettings },
        { "credit percent above one hundred", PredictivePacingReason.InvalidSettings },
    };

    [Theory]
    [MemberData(nameof(FailClosedCases))]
    public void Calculate_WhenAMeasurementOrLimitCannotBeTrusted_FallsBackToReservingAnAgentPerCall(string scenario, PredictivePacingReason expectedReason)
    {
        // Arrange
        var input = Input();
        Apply(input, scenario);

        // Act
        var decision = PredictiveOverDialCalculator.Calculate(input);

        // Assert
        Assert.Equal(PredictivePacingDecisionMode.ReservedFallback, decision.Mode);
        Assert.Equal(expectedReason, decision.Reason);
        Assert.Equal(0, decision.DialCount);
    }

    [Fact]
    public void Calculate_WhenTheThrottleReachesZero_PlacesNoCallWithoutAnAgent()
    {
        // Arrange: the measured rate is at the cap, where the throttle leaves none of the over-dial.
        var input = Input();
        input.AbandonmentRatePercent = input.MaxAbandonmentRatePercent;

        // Act
        var decision = PredictiveOverDialCalculator.Calculate(input);

        // Assert
        Assert.Equal(0d, PredictiveOverDialCalculator.CalculateThrottle(input.MaxAbandonmentRatePercent, input.TargetAbandonmentRatePercent, input.MaxAbandonmentRatePercent));
        Assert.NotEqual(PredictivePacingDecisionMode.OverDial, decision.Mode);
        Assert.True(decision.DialCount <= input.AvailableAgents);
        Assert.Equal(0, decision.DialCount);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 1)]
    [InlineData(0, 2)]
    public void Calculate_WhenFewerThanOneAgentCanTakeACall_PlacesNothing(int availableAgents, int freeingAgents)
    {
        // Arrange: with 40% of the freeing agents credited, two of them still make less than one agent.
        var input = Input();
        input.AvailableAgents = availableAgents;
        input.FreeingAgents = freeingAgents;
        input.CreditAgentsFreeingUp = true;
        input.FreeUpCreditPercent = 40;

        // Act
        var decision = PredictiveOverDialCalculator.Calculate(input);

        // Assert
        Assert.Equal(PredictivePacingReason.NoAgents, decision.Reason);
        Assert.Equal(0, decision.DialCount);
        Assert.Equal(0, decision.TargetCalls);
    }

    [Fact]
    public void Calculate_WhenFreeingAgentsAreNotCredited_IgnoresThem()
    {
        // Arrange
        var input = Input();
        input.AvailableAgents = 0;
        input.FreeingAgents = 10;
        input.CreditAgentsFreeingUp = false;

        // Act
        var decision = PredictiveOverDialCalculator.Calculate(input);

        // Assert
        Assert.Equal(PredictivePacingReason.NoAgents, decision.Reason);
        Assert.Equal(0, decision.DialCount);
    }

    [Fact]
    public void Calculate_WhenFreeingAgentsAreCredited_CountsOnlyTheCreditedShareAndRaisesTheLimitByItsWholePart()
    {
        // Arrange
        var input = Input();
        input.AvailableAgents = 4;
        input.FreeingAgents = 5;
        input.CreditAgentsFreeingUp = true;
        input.FreeUpCreditPercent = 50;
        input.MaxLinesPerAgent = 1;
        input.AnswerRate = 0.05;

        // Act
        var decision = PredictiveOverDialCalculator.Calculate(input);

        // Assert: 4 free plus half of 5 is 6.5 agents; one line per free agent plus the 2 whole credited agents is 6.
        Assert.Equal(6.5, decision.EffectiveAgents, Tolerance);
        Assert.Equal(6, decision.CallLimit);
        Assert.Equal(6, decision.TargetCalls);
    }

    [Fact]
    public void Calculate_ForAKnownCase_PicksTheLargestCallCountWithinTheTarget()
    {
        // Arrange: one agent, a 5% answer rate and a 5% target with the throttle fully open.
        // Two calls: E[overflow] = P(2) = 0.0025, over 0.1 expected answers = 2.5%.
        // Three calls: 3(0.05^2)(0.95) + 2(0.05^3) = 0.007375, over 0.15 = 4.92%.
        // Four calls: 0.0135375 + 2(0.000475) + 3(0.00000625) = 0.01450625, over 0.2 = 7.25%, above the target.
        var input = Input();
        input.AvailableAgents = 1;
        input.AnswerRate = 0.05;
        input.TargetAbandonmentRatePercent = 5;
        input.MaxAbandonmentRatePercent = 10;
        input.AbandonmentRatePercent = 0;

        // Act
        var decision = PredictiveOverDialCalculator.Calculate(input);

        // Assert
        Assert.Equal(PredictivePacingDecisionMode.OverDial, decision.Mode);
        Assert.Equal(PredictivePacingReason.Paced, decision.Reason);
        Assert.Equal(3, decision.TargetCalls);
        Assert.Equal(3, decision.DialCount);
        Assert.Equal(1d, decision.Throttle);
        Assert.Equal(0.007375 / 0.15 * 100, decision.ExpectedAbandonmentPercent, 1e-9);
    }

    [Fact]
    public void Calculate_WhenEveryCallIsAnswered_PlacesOneCallPerAgent()
    {
        // Arrange
        var input = Input();
        input.AvailableAgents = 10;
        input.AnswerRate = 1;

        // Act
        var decision = PredictiveOverDialCalculator.Calculate(input);

        // Assert: an eleventh call abandons one answer in eleven, far above the target.
        Assert.Equal(10, decision.TargetCalls);
        Assert.Equal(0d, decision.ExpectedAbandonmentPercent);
    }

    [Fact]
    public void Calculate_WhenTheMeasuredAnswerRateIsTiny_UsesTheMinimumAnswerRate()
    {
        // Arrange
        var input = Input();
        input.AnswerRate = 0.0001;

        // Act
        var decision = PredictiveOverDialCalculator.Calculate(input);

        // Assert
        Assert.Equal(PredictiveOverDialCalculator.MinimumAnswerRate, decision.AnswerRate);
    }

    [Fact]
    public void Calculate_AsTheAnswerRateFalls_PlacesMoreCalls()
    {
        // Arrange
        double[] answerRates = [1, 0.9, 0.7, 0.5, 0.3, 0.2, 0.1, 0.05];
        var previous = -1;

        foreach (var answerRate in answerRates)
        {
            var input = Input();
            input.AvailableAgents = 10;
            input.AnswerRate = answerRate;

            // Act
            var decision = PredictiveOverDialCalculator.Calculate(input);

            // Assert
            Assert.True(decision.TargetCalls >= previous, $"At an answer rate of {answerRate} the dialer placed {decision.TargetCalls} calls, fewer than the {previous} it placed at a higher rate.");
            previous = decision.TargetCalls;
        }

        Assert.True(previous > 10);
    }

    [Fact]
    public void Calculate_AsTheMeasuredAbandonmentRises_PlacesFewerCalls()
    {
        // Arrange
        double[] measuredRates = [0, 0.5, 1, 1.5, 2, 2.5, 2.9, 2.99];
        var previous = int.MaxValue;
        var first = -1;

        foreach (var measured in measuredRates)
        {
            var input = Input();
            input.AvailableAgents = 20;
            input.AnswerRate = 0.3;
            input.AbandonmentRatePercent = measured;

            // Act
            var decision = PredictiveOverDialCalculator.Calculate(input);

            // Assert
            Assert.True(decision.TargetCalls <= previous, $"At a measured rate of {measured}% the dialer placed {decision.TargetCalls} calls, more than the {previous} it placed at a lower rate.");
            previous = decision.TargetCalls;

            if (first < 0)
            {
                first = decision.TargetCalls;
            }
        }

        Assert.True(previous < first);
    }

    [Fact]
    public void Calculate_RaisingTheTarget_NeverPlacesFewerCalls()
    {
        // Arrange
        double[] targets = [0.5, 1, 2, 3, 4];
        var previous = -1;

        foreach (var target in targets)
        {
            var input = Input();
            input.TargetAbandonmentRatePercent = target;
            input.MaxAbandonmentRatePercent = 5;
            input.AbandonmentRatePercent = 0;

            // Act
            var decision = PredictiveOverDialCalculator.Calculate(input);

            // Assert
            Assert.True(decision.TargetCalls >= previous);
            previous = decision.TargetCalls;
        }
    }

    [Fact]
    public void Calculate_NeverExceedsAnyLimit()
    {
        int[] agentCounts = [0, 1, 3, 10, 25];
        double[] answerRates = [0.01, 0.1, 0.5, 1];
        double[] measuredRates = [0, 1.6, 2.4];
        double[] linesPerAgent = [1, 1.5, 50];
        int[] callsInFlightLimits = [1, 7, 5000];
        int[] dialsPerCycleLimits = [1, 25];
        int[] callsInFlight = [0, 40];

        foreach (var agents in agentCounts)
        {
            foreach (var answerRate in answerRates)
            {
                foreach (var measured in measuredRates)
                {
                    foreach (var lines in linesPerAgent)
                    {
                        foreach (var inFlightLimit in callsInFlightLimits)
                        {
                            foreach (var dialsLimit in dialsPerCycleLimits)
                            {
                                foreach (var inFlight in callsInFlight)
                                {
                                    var input = Input();
                                    input.AvailableAgents = agents;
                                    input.AnswerRate = answerRate;
                                    input.AbandonmentRatePercent = measured;
                                    input.MaxLinesPerAgent = lines;
                                    input.MaxCallsInFlight = inFlightLimit;
                                    input.MaxDialsPerCycle = dialsLimit;
                                    input.CallsInFlight = inFlight;

                                    var decision = PredictiveOverDialCalculator.Calculate(input);
                                    var scenario = $"agents {agents}, answer rate {answerRate}, measured {measured}%, lines {lines}, in-flight limit {inFlightLimit}, dial limit {dialsLimit}, in flight {inFlight}";

                                    Assert.True(decision.DialCount >= 0, scenario);
                                    Assert.True(decision.DialCount <= dialsLimit, scenario);
                                    Assert.True(decision.TargetCalls <= inFlightLimit, scenario);
                                    Assert.True(decision.TargetCalls <= PredictiveDialingDefaults.MaxCallsInFlight, scenario);
                                    Assert.True(decision.TargetCalls <= (int)Math.Floor(Math.Min(lines, PredictiveDialingDefaults.MaxLinesPerAgent) * agents), scenario);
                                    Assert.True(decision.DialCount <= Math.Max(0, decision.TargetCalls - inFlight), scenario);

                                    // Calls beyond one per agent are only placed when their expected abandonment is within the target.
                                    if (decision.TargetCalls > agents)
                                    {
                                        Assert.True(decision.ExpectedAbandonmentPercent <= decision.EffectiveTargetPercent + 1e-9, scenario);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public void Calculate_WhenCallsAreAlreadyInFlight_PlacesOnlyTheDifference()
    {
        // Arrange
        var input = Input();
        var target = PredictiveOverDialCalculator.Calculate(input).TargetCalls;
        input.CallsInFlight = target - 2;

        // Act
        var decision = PredictiveOverDialCalculator.Calculate(input);

        // Assert
        Assert.Equal(2, decision.DialCount);
    }

    [Fact]
    public void Calculate_WhenMoreCallsAreInFlightThanTheTarget_PlacesNothing()
    {
        // Arrange
        var input = Input();
        input.CallsInFlight = 500;

        // Act
        var decision = PredictiveOverDialCalculator.Calculate(input);

        // Assert
        Assert.Equal(PredictivePacingDecisionMode.OverDial, decision.Mode);
        Assert.Equal(0, decision.DialCount);
    }

    [Fact]
    public void Calculate_WhenTheLinesPerAgentAreSetAboveTheHardCeiling_KeepsToTheCeiling()
    {
        // Arrange
        var input = Input();
        input.AvailableAgents = 4;
        input.AnswerRate = 0.05;
        input.MaxLinesPerAgent = 50;

        // Act
        var decision = PredictiveOverDialCalculator.Calculate(input);

        // Assert
        Assert.Equal((int)(PredictiveDialingDefaults.MaxLinesPerAgent * 4), decision.CallLimit);
        Assert.True(decision.TargetCalls <= decision.CallLimit);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(2, 0.5)]
    [InlineData(2.5, 0.25)]
    [InlineData(3, 0)]
    [InlineData(4, 0)]
    public void CalculateThrottle_IsFullUpToHalfTheTargetThenFallsInAStraightLineToNoneAtTheCap(double measured, double expected)
    {
        // Act
        var throttle = PredictiveOverDialCalculator.CalculateThrottle(measured, targetPercent: 2, capPercent: 3);

        // Assert
        Assert.Equal(expected, throttle, Tolerance);
    }

    [Fact]
    public void Calculate_AppliesTheThrottleToTheTarget()
    {
        // Arrange
        var input = Input();
        input.AbandonmentRatePercent = 2;

        // Act
        var decision = PredictiveOverDialCalculator.Calculate(input);

        // Assert
        Assert.Equal(0.5, decision.Throttle, Tolerance);
        Assert.Equal(1, decision.EffectiveTargetPercent, Tolerance);
    }

    [Theory]
    [InlineData(2, 0.5, 1, 0.25)]
    [InlineData(3, 0.5, 1, 0.625)]
    [InlineData(2, 0.5, 1.5, 0.125)]
    [InlineData(5, 0.9, 5, 0)]
    [InlineData(4, 1, 2.5, 1.5)]
    [InlineData(4, 0, 1, 0)]
    [InlineData(0, 0.5, 0, 0)]
    public void ExpectedOverflow_MatchesTheBinomialExpectation(int calls, double answerRate, double agents, double expected)
    {
        // Act
        var overflow = PredictiveOverDialCalculator.ExpectedOverflow(calls, answerRate, agents);

        // Assert
        Assert.Equal(expected, overflow, Tolerance);
    }

    [Fact]
    public void ExpectedOverflow_WithManyCallsAndAHighAnswerRate_DoesNotUnderflow()
    {
        // Act: 1000 calls answered 95% of the time against 900 agents. Fewer than 900 answers is seven standard deviations
        // away, so the overflow is the expected answers less the agents.
        var overflow = PredictiveOverDialCalculator.ExpectedOverflow(1000, 0.95, 900);

        // Assert
        Assert.Equal(50, overflow, 1e-6);
    }

    [Theory]
    [InlineData(2, 0.5, 1, 0.25)]
    [InlineData(3, 0.5, 1, 0.625 / 1.5)]
    [InlineData(0, 0.5, 1, 0)]
    public void ExpectedAbandonmentRate_IsTheOverflowOverTheExpectedAnswers(int calls, double answerRate, double agents, double expected)
    {
        // Act
        var rate = PredictiveOverDialCalculator.ExpectedAbandonmentRate(calls, answerRate, agents);

        // Assert
        Assert.Equal(expected, rate, Tolerance);
    }

    [Fact]
    public void ForProfile_WhenTheProfileDoesNotEnforceTheCap_LeavesNoCapSoNothingIsOverDialed()
    {
        // Arrange
        var profile = new DialerProfile
        {
            Mode = DialerMode.Predictive,
            PredictivePacingModel = PredictivePacingModel.OverDial,
            EnforceAbandonmentCap = false,
            MaxAbandonmentRatePercent = 3,
        };

        var input = PredictivePacingInput.ForProfile(profile, new ContactCenterPredictiveDialingOptions());
        input.PolicyPermitted = true;
        input.AvailableAgents = 10;
        input.AnswerRate = 0.3;
        input.AnswerRateSampleSize = 1000;
        input.AbandonmentRatePercent = 0;
        input.AbandonmentSampleSize = 1000;
        input.ComplianceAbandonmentRatePercent = 0;

        // Act
        var decision = PredictiveOverDialCalculator.Calculate(input);

        // Assert
        Assert.Equal(0, input.MaxAbandonmentRatePercent);
        Assert.Equal(PredictivePacingDecisionMode.ReservedFallback, decision.Mode);
        Assert.Equal(PredictivePacingReason.InvalidSettings, decision.Reason);
    }

    [Fact]
    public void ForProfile_CarriesTheProfileAndTenantLimits()
    {
        // Arrange
        var profile = new DialerProfile
        {
            EnforceAbandonmentCap = true,
            MaxAbandonmentRatePercent = 3,
            TargetAbandonmentRatePercent = 1.5,
            AnswerRateSampleFloor = 70,
            AbandonmentSampleFloor = 40,
            MaxLinesPerAgent = 2.5,
            MaxCallsInFlight = 60,
            CreditAgentsFreeingUp = true,
            FreeUpCreditPercent = 30,
        };

        // Act
        var input = PredictivePacingInput.ForProfile(profile, new ContactCenterPredictiveDialingOptions { MaxDialsPerCycle = 9 });

        // Assert
        Assert.Equal(3, input.MaxAbandonmentRatePercent);
        Assert.Equal(1.5, input.TargetAbandonmentRatePercent);
        Assert.Equal(70, input.AnswerRateSampleFloor);
        Assert.Equal(40, input.AbandonmentSampleFloor);
        Assert.Equal(2.5, input.MaxLinesPerAgent);
        Assert.Equal(60, input.MaxCallsInFlight);
        Assert.Equal(9, input.MaxDialsPerCycle);
        Assert.True(input.CreditAgentsFreeingUp);
        Assert.Equal(30, input.FreeUpCreditPercent);
        Assert.False(input.PolicyPermitted);
        Assert.Null(input.AnswerRate);
        Assert.Null(input.AbandonmentRatePercent);
        Assert.Null(input.ComplianceAbandonmentRatePercent);
    }

    private static PredictivePacingInput Input()
    {
        return new PredictivePacingInput
        {
            PolicyPermitted = true,
            AvailableAgents = 10,
            FreeingAgents = 0,
            CallsInFlight = 0,
            AnswerRate = 0.3,
            AnswerRateSampleSize = 500,
            AbandonmentRatePercent = 0.5,
            AbandonmentSampleSize = 200,
            ComplianceAbandonmentRatePercent = 1,
            TargetAbandonmentRatePercent = 2,
            MaxAbandonmentRatePercent = 3,
            AnswerRateSampleFloor = 50,
            AbandonmentSampleFloor = 30,
            MaxLinesPerAgent = 5,
            MaxCallsInFlight = 1000,
            MaxDialsPerCycle = 1000,
            CreditAgentsFreeingUp = false,
            FreeUpCreditPercent = 50,
        };
    }

    private static void Apply(PredictivePacingInput input, string scenario)
    {
        switch (scenario)
        {
            case "no answer rate": input.AnswerRate = null; break;
            case "answer rate is not a number": input.AnswerRate = double.NaN; break;
            case "answer rate is negative": input.AnswerRate = -0.1; break;
            case "answer rate above one": input.AnswerRate = 1.2; break;
            case "answer rate sample below floor": input.AnswerRateSampleSize = input.AnswerRateSampleFloor - 1; break;
            case "no rolling abandonment rate": input.AbandonmentRatePercent = null; break;
            case "rolling abandonment rate is infinite": input.AbandonmentRatePercent = double.PositiveInfinity; break;
            case "abandonment sample below floor": input.AbandonmentSampleSize = input.AbandonmentSampleFloor - 1; break;
            case "no compliance rate": input.ComplianceAbandonmentRatePercent = null; break;
            case "compliance rate at cap": input.ComplianceAbandonmentRatePercent = input.MaxAbandonmentRatePercent; break;
            case "compliance rate over cap": input.ComplianceAbandonmentRatePercent = input.MaxAbandonmentRatePercent + 1; break;
            case "rolling rate at cap": input.AbandonmentRatePercent = input.MaxAbandonmentRatePercent; break;
            case "rolling rate over cap": input.AbandonmentRatePercent = input.MaxAbandonmentRatePercent + 0.5; break;
            case "no cap enforced": input.MaxAbandonmentRatePercent = 0; break;
            case "cap over one hundred": input.MaxAbandonmentRatePercent = 101; input.TargetAbandonmentRatePercent = 2; break;
            case "target at cap": input.TargetAbandonmentRatePercent = input.MaxAbandonmentRatePercent; break;
            case "target above cap": input.TargetAbandonmentRatePercent = input.MaxAbandonmentRatePercent + 1; break;
            case "target zero": input.TargetAbandonmentRatePercent = 0; break;
            case "lines per agent below one": input.MaxLinesPerAgent = 0.5; break;
            case "lines per agent not a number": input.MaxLinesPerAgent = double.NaN; break;
            case "no calls in flight allowed": input.MaxCallsInFlight = 0; break;
            case "no dials per cycle allowed": input.MaxDialsPerCycle = 0; break;
            case "credit percent above one hundred": input.FreeUpCreditPercent = 101; break;
            default: throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null);
        }
    }
}
