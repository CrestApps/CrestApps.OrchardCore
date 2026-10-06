using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Works out how many calls an over-dialing Predictive campaign may place without reserving agents.
/// </summary>
/// <remarks>
/// <para>
/// Over-dialing places more calls than there are free agents, betting that most will not be answered. A call a person
/// answers when every agent is taken is abandoned, and abandoned calls are what regulators cap. The calculation therefore
/// sizes the bet from the measured answer rate so that the expected share of answered calls no agent can take stays under
/// the profile's target, and refuses to bet at all whenever a measurement it needs is missing, too thin, or already at the
/// cap.
/// </para>
/// <para>
/// With <c>N</c> calls in flight, each answered independently with the measured rate <c>r</c>, the number answered is
/// binomial: <c>X ~ Binomial(N, r)</c>. With <c>E</c> agents able to take a call, the answered calls nobody can take are
/// <c>max(X - E, 0)</c>, whose expectation is the sum over <c>k &gt; E</c> of <c>(k - E) P(X = k)</c>. Dividing by the
/// expected answers <c>N r</c> gives the expected abandonment rate, and the calculation picks the largest <c>N</c> whose
/// rate stays within the (throttled) target. Calls already ringing are counted as though they were just placed, which
/// overstates how many will still be answered and so errs on the side of fewer calls.
/// </para>
/// <para>
/// The calculation is a pure function of its input, so it can be tested exhaustively and replayed from a logged decision.
/// </para>
/// </remarks>
public static class PredictiveOverDialCalculator
{
    /// <summary>
    /// The lowest answer rate the calculation will use, whatever was measured. A rate near zero would ask for an unbounded
    /// number of calls; the line limits stop that too, but no measurement should be trusted that far.
    /// </summary>
    public const double MinimumAnswerRate = 0.05;

    /// <summary>
    /// The share of the target abandonment rate below which the over-dial is not throttled. Above it, the over-dial is
    /// reduced in a straight line, reaching none at the cap.
    /// </summary>
    public const double ThrottleThreshold = 0.5;

    /// <summary>
    /// Decides how many calls to place now.
    /// </summary>
    /// <param name="input">The measurements and limits.</param>
    /// <returns>The decision. Any doubt about the input yields a decision that places no call without an agent.</returns>
    public static PredictivePacingDecision Calculate(PredictivePacingInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (!input.PolicyPermitted)
        {
            return PredictivePacingDecision.Suppressed(PredictivePacingReason.PolicySuppressed);
        }

        if (!HasValidSettings(input))
        {
            return PredictivePacingDecision.ReservedFallback(PredictivePacingReason.InvalidSettings);
        }

        if (!IsMeasured(input.AnswerRate) || input.AnswerRate > 1)
        {
            return PredictivePacingDecision.ReservedFallback(PredictivePacingReason.AnswerRateUnavailable);
        }

        if (input.AnswerRateSampleSize < input.AnswerRateSampleFloor)
        {
            return PredictivePacingDecision.ReservedFallback(PredictivePacingReason.AnswerRateSampleBelowFloor);
        }

        if (!IsMeasured(input.AbandonmentRatePercent))
        {
            return PredictivePacingDecision.ReservedFallback(PredictivePacingReason.AbandonmentRateUnavailable);
        }

        if (input.AbandonmentSampleSize < input.AbandonmentSampleFloor)
        {
            return PredictivePacingDecision.ReservedFallback(PredictivePacingReason.AbandonmentSampleBelowFloor);
        }

        if (!IsMeasured(input.ComplianceAbandonmentRatePercent))
        {
            return PredictivePacingDecision.ReservedFallback(PredictivePacingReason.ComplianceRateUnavailable);
        }

        var cap = input.MaxAbandonmentRatePercent;

        if (input.ComplianceAbandonmentRatePercent.Value >= cap)
        {
            return PredictivePacingDecision.ReservedFallback(PredictivePacingReason.ComplianceRateAtCap);
        }

        var measured = input.AbandonmentRatePercent.Value;

        if (measured >= cap)
        {
            return PredictivePacingDecision.ReservedFallback(PredictivePacingReason.RollingRateAtCap);
        }

        var throttle = CalculateThrottle(measured, input.TargetAbandonmentRatePercent, cap);

        if (throttle <= 0)
        {
            return PredictivePacingDecision.ReservedFallback(PredictivePacingReason.ThrottledToZero);
        }

        var effectiveTarget = input.TargetAbandonmentRatePercent * throttle;
        var available = Math.Max(0, input.AvailableAgents);
        var credited = input.CreditAgentsFreeingUp
            ? Math.Max(0, input.FreeingAgents) * input.FreeUpCreditPercent / 100d
            : 0;
        var agents = available + credited;
        var answerRate = Math.Clamp(input.AnswerRate.Value, MinimumAnswerRate, 1);

        if (agents < 1)
        {
            return new PredictivePacingDecision
            {
                Mode = PredictivePacingDecisionMode.OverDial,
                Reason = PredictivePacingReason.NoAgents,
                EffectiveAgents = agents,
                AnswerRate = answerRate,
                Throttle = throttle,
                EffectiveTargetPercent = effectiveTarget,
            };
        }

        var linesPerAgent = Math.Clamp(input.MaxLinesPerAgent, PredictiveDialingDefaults.MinLinesPerAgent, PredictiveDialingDefaults.MaxLinesPerAgent);
        var lineLimit = (int)Math.Floor(linesPerAgent * available) + (int)Math.Floor(credited);
        var callLimit = Math.Min(lineLimit, Math.Min(input.MaxCallsInFlight, PredictiveDialingDefaults.MaxCallsInFlight));

        var logFactorials = LogFactorials(callLimit);
        var allowed = effectiveTarget / 100;
        var calls = Math.Min((int)Math.Floor(agents), callLimit);

        // The expected rate grows with every extra call, so the first call that would exceed the target ends the search.
        while (calls < callLimit &&
            ExpectedAbandonmentRate(calls + 1, answerRate, agents, logFactorials) <= allowed)
        {
            calls++;
        }

        var dials = Math.Clamp(calls - Math.Max(0, input.CallsInFlight), 0, input.MaxDialsPerCycle);

        return new PredictivePacingDecision
        {
            Mode = PredictivePacingDecisionMode.OverDial,
            Reason = PredictivePacingReason.Paced,
            DialCount = dials,
            TargetCalls = calls,
            EffectiveAgents = agents,
            CallLimit = callLimit,
            AnswerRate = answerRate,
            Throttle = throttle,
            EffectiveTargetPercent = effectiveTarget,
            ExpectedAbandonmentPercent = ExpectedAbandonmentRate(calls, answerRate, agents, logFactorials) * 100,
        };
    }

    /// <summary>
    /// Works out how much of the over-dial is left as the measured abandonment rate climbs: all of it up to half the
    /// target, then less in a straight line, and none once the rate reaches the cap.
    /// </summary>
    /// <param name="abandonmentRatePercent">The measured rolling abandonment rate, in percent.</param>
    /// <param name="targetPercent">The target abandonment rate, in percent.</param>
    /// <param name="capPercent">The abandonment cap, in percent.</param>
    /// <returns>The share of the over-dial left, from 0 to 1.</returns>
    public static double CalculateThrottle(double abandonmentRatePercent, double targetPercent, double capPercent)
    {
        var start = targetPercent * ThrottleThreshold;

        if (abandonmentRatePercent >= capPercent)
        {
            return 0;
        }

        if (abandonmentRatePercent <= start)
        {
            return 1;
        }

        return Math.Clamp((capPercent - abandonmentRatePercent) / (capPercent - start), 0, 1);
    }

    /// <summary>
    /// Works out the expected number of answered calls nobody can take: the expectation of <c>max(X - agents, 0)</c> for
    /// <c>X ~ Binomial(calls, answerRate)</c>.
    /// </summary>
    /// <param name="calls">The calls in flight.</param>
    /// <param name="answerRate">The chance each is answered, from 0 to 1.</param>
    /// <param name="agents">The agents able to take a call; may be fractional when agents freeing up are credited.</param>
    /// <returns>The expected number of abandoned calls.</returns>
    public static double ExpectedOverflow(int calls, double answerRate, double agents)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(calls);
        ArgumentOutOfRangeException.ThrowIfNegative(answerRate);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(answerRate, 1);

        return ExpectedOverflow(calls, answerRate, agents, LogFactorials(calls));
    }

    /// <summary>
    /// Works out the expected abandonment rate, as a share of answered calls, with the given calls in flight.
    /// </summary>
    /// <param name="calls">The calls in flight.</param>
    /// <param name="answerRate">The chance each is answered, from 0 to 1.</param>
    /// <param name="agents">The agents able to take a call.</param>
    /// <returns>The expected abandoned calls divided by the expected answered calls, from 0 to 1.</returns>
    public static double ExpectedAbandonmentRate(int calls, double answerRate, double agents)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(calls);
        ArgumentOutOfRangeException.ThrowIfNegative(answerRate);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(answerRate, 1);

        return ExpectedAbandonmentRate(calls, answerRate, agents, LogFactorials(calls));
    }

    private static double ExpectedAbandonmentRate(int calls, double answerRate, double agents, double[] logFactorials)
    {
        var expectedAnswers = calls * answerRate;

        if (expectedAnswers <= 0)
        {
            return 0;
        }

        return ExpectedOverflow(calls, answerRate, agents, logFactorials) / expectedAnswers;
    }

    private static double ExpectedOverflow(int calls, double answerRate, double agents, double[] logFactorials)
    {
        var first = Math.Max(0, (int)Math.Floor(agents) + 1);

        if (calls < first || answerRate <= 0)
        {
            return 0;
        }

        if (answerRate >= 1)
        {
            return calls - agents;
        }

        // In logarithms: with many calls and a high answer rate the individual probabilities underflow a double long
        // before their weighted sum stops mattering.
        var logRate = Math.Log(answerRate);
        var logMiss = Math.Log(1 - answerRate);
        var overflow = 0d;

        for (var answered = first; answered <= calls; answered++)
        {
            var logProbability = logFactorials[calls] - logFactorials[answered] - logFactorials[calls - answered]
                + (answered * logRate) + ((calls - answered) * logMiss);

            overflow += (answered - agents) * Math.Exp(logProbability);
        }

        return overflow;
    }

    private static double[] LogFactorials(int max)
    {
        var values = new double[Math.Max(max, 0) + 1];

        for (var i = 1; i < values.Length; i++)
        {
            values[i] = values[i - 1] + Math.Log(i);
        }

        return values;
    }

    private static bool IsMeasured(double? value)
        => value is { } actual && !double.IsNaN(actual) && !double.IsInfinity(actual) && actual >= 0;

    private static bool HasValidSettings(PredictivePacingInput input)
    {
        var cap = input.MaxAbandonmentRatePercent;
        var target = input.TargetAbandonmentRatePercent;

        return cap > 0 && cap <= 100 &&
            target > 0 && target < cap &&
            !double.IsNaN(input.MaxLinesPerAgent) && input.MaxLinesPerAgent >= PredictiveDialingDefaults.MinLinesPerAgent &&
            input.MaxCallsInFlight >= 1 &&
            input.MaxDialsPerCycle >= 1 &&
            input.AnswerRateSampleFloor >= 0 &&
            input.AbandonmentSampleFloor >= 0 &&
            input.FreeUpCreditPercent is >= 0 and <= 100;
    }
}
