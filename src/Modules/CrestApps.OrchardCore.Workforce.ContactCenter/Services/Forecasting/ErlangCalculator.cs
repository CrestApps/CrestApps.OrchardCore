namespace CrestApps.OrchardCore.Workforce.ContactCenter.Services.Forecasting;

/// <summary>
/// What one interval needs answering: the work, how long it takes, and the goal.
/// </summary>
public sealed class StaffingRequest
{
    /// <summary>
    /// Gets or sets the work offered in the interval.
    /// </summary>
    public double Volume { get; set; }

    /// <summary>
    /// Gets or sets the interval's length in seconds.
    /// </summary>
    public double IntervalSeconds { get; set; } = 900;

    /// <summary>
    /// Gets or sets the average handle time in seconds.
    /// </summary>
    public double AverageHandleSeconds { get; set; }

    /// <summary>
    /// Gets or sets the share to answer within <see cref="ServiceLevelSeconds"/>, in percent, or <see langword="null"/>.
    /// </summary>
    public double? ServiceLevelPercent { get; set; }

    /// <summary>
    /// Gets or sets the service level's threshold in seconds.
    /// </summary>
    public double ServiceLevelSeconds { get; set; } = 20;

    /// <summary>
    /// Gets or sets the average speed of answer to stay within, in seconds, or <see langword="null"/>.
    /// </summary>
    public double? AverageSpeedOfAnswerSeconds { get; set; }

    /// <summary>
    /// Gets or sets the highest occupancy allowed, in percent, or <see langword="null"/>.
    /// </summary>
    public double? MaxOccupancyPercent { get; set; }

    /// <summary>
    /// Gets or sets how many pieces of work an agent handles at once, such as chats.
    /// </summary>
    public int Concurrency { get; set; } = 1;

    /// <summary>
    /// Gets or sets how long customers wait before they abandon, in seconds, for Erlang A; <see langword="null"/>
    /// for Erlang C, where nobody abandons.
    /// </summary>
    public double? PatienceSeconds { get; set; }
}

/// <summary>
/// The agents one interval needs and what they achieve.
/// </summary>
public sealed class StaffingResult
{
    /// <summary>
    /// Gets or sets the fewest agents that meet the goal.
    /// </summary>
    public int Agents { get; set; }

    /// <summary>
    /// Gets or sets the expected service level, as a fraction.
    /// </summary>
    public double ServiceLevel { get; set; }

    /// <summary>
    /// Gets or sets the expected average speed of answer in seconds.
    /// </summary>
    public double AverageSpeedOfAnswerSeconds { get; set; }

    /// <summary>
    /// Gets or sets the expected occupancy, as a fraction.
    /// </summary>
    public double Occupancy { get; set; }

    /// <summary>
    /// Gets or sets the expected share of customers who abandon, as a fraction; zero for Erlang C.
    /// </summary>
    public double AbandonRate { get; set; }
}

/// <summary>
/// The waiting and abandoning of an Erlang A queue.
/// </summary>
/// <param name="ProbabilityOfWaiting">The probability an arriving customer waits.</param>
/// <param name="AbandonRate">The share of customers who abandon.</param>
public readonly record struct ErlangAResult(double ProbabilityOfWaiting, double AbandonRate);

/// <summary>
/// Erlang C and Erlang A staffing. Pure and numerically stable: Erlang C comes from the Erlang B recursion, which
/// never forms a large power or factorial, and Erlang A from the birth-death chain with abandonment.
/// </summary>
public static class ErlangCalculator
{
    private const int MaxAgents = 10_000;

    /// <summary>
    /// Gets the probability that an arriving customer waits (Erlang C).
    /// </summary>
    /// <param name="agents">The agents.</param>
    /// <param name="traffic">The offered traffic in Erlangs.</param>
    /// <returns>The probability; one when the agents can't keep up.</returns>
    public static double ProbabilityOfWaiting(int agents, double traffic)
    {
        if (traffic <= 0)
        {
            return 0;
        }

        if (agents <= traffic)
        {
            return 1;
        }

        var blocking = ErlangB(agents, traffic);

        return agents * blocking / (agents - (traffic * (1 - blocking)));
    }

    /// <summary>
    /// Gets the share of customers answered within a threshold (Erlang C).
    /// </summary>
    /// <param name="agents">The agents.</param>
    /// <param name="traffic">The offered traffic in Erlangs.</param>
    /// <param name="averageHandleSeconds">The average handle time in seconds.</param>
    /// <param name="thresholdSeconds">The threshold in seconds.</param>
    /// <returns>The service level, as a fraction.</returns>
    public static double ServiceLevel(int agents, double traffic, double averageHandleSeconds, double thresholdSeconds)
    {
        if (traffic <= 0)
        {
            return 1;
        }

        if (agents <= traffic)
        {
            return 0;
        }

        return 1 - (ProbabilityOfWaiting(agents, traffic) * Math.Exp(-(agents - traffic) * thresholdSeconds / averageHandleSeconds));
    }

    /// <summary>
    /// Gets the average speed of answer in seconds (Erlang C).
    /// </summary>
    /// <param name="agents">The agents.</param>
    /// <param name="traffic">The offered traffic in Erlangs.</param>
    /// <param name="averageHandleSeconds">The average handle time in seconds.</param>
    /// <returns>The average speed of answer; infinity when the agents can't keep up.</returns>
    public static double AverageSpeedOfAnswer(int agents, double traffic, double averageHandleSeconds)
    {
        if (traffic <= 0)
        {
            return 0;
        }

        if (agents <= traffic)
        {
            return double.PositiveInfinity;
        }

        return ProbabilityOfWaiting(agents, traffic) * averageHandleSeconds / (agents - traffic);
    }

    /// <summary>
    /// Gets the probability of waiting and the share of customers who abandon when customers abandon after an
    /// exponentially distributed patience (Erlang A, the M/M/N+M queue).
    /// </summary>
    /// <param name="agents">The agents.</param>
    /// <param name="traffic">The offered traffic in Erlangs.</param>
    /// <param name="averageHandleSeconds">The average handle time in seconds.</param>
    /// <param name="patienceSeconds">The average patience in seconds.</param>
    /// <returns>The probability of waiting and the abandon rate.</returns>
    public static ErlangAResult ErlangA(int agents, double traffic, double averageHandleSeconds, double patienceSeconds)
    {
        if (traffic <= 0 || agents <= 0)
        {
            return new ErlangAResult(traffic > 0 ? 1 : 0, traffic > 0 ? 1 : 0);
        }

        // Rates per handle time: arrivals at the traffic, each agent serves at one, each waiting customer leaves at
        // the handle time over the patience. The chain's probabilities are worked out relative to the empty state.
        // The weights are rescaled whenever they grow large, so a large traffic never overflows.
        const double Large = 1e200;
        var abandonRate = averageHandleSeconds / patienceSeconds;
        var weight = 1d;
        var total = 1d;

        for (var n = 1; n <= agents; n++)
        {
            weight *= traffic / n;
            total += weight;

            if (weight > Large)
            {
                weight /= Large;
                total /= Large;
            }
        }

        var allBusy = weight;
        var waiting = 0d;
        var queueLength = 0d;

        for (var k = 1; k < 100_000; k++)
        {
            weight *= traffic / (agents + (k * abandonRate));
            waiting += weight;
            queueLength += k * weight;

            if (weight < 1e-15 * (total + waiting))
            {
                break;
            }
        }

        var normalizer = total + waiting;
        var probabilityOfWaiting = (allBusy + waiting) / normalizer;
        var abandoned = abandonRate * (queueLength / normalizer) / traffic;

        return new ErlangAResult(probabilityOfWaiting, Math.Clamp(abandoned, 0, 1));
    }

    /// <summary>
    /// Finds the fewest agents that meet a goal: the service level, the average speed of answer, or both, within the
    /// occupancy cap.
    /// </summary>
    /// <param name="request">The interval's work and goal.</param>
    /// <returns>The agents and what they achieve.</returns>
    public static StaffingResult Staff(StaffingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Volume <= 0 || request.AverageHandleSeconds <= 0 || request.IntervalSeconds <= 0)
        {
            return new StaffingResult { ServiceLevel = 1 };
        }

        var handle = request.AverageHandleSeconds / Math.Max(1, request.Concurrency);
        var traffic = request.Volume * handle / request.IntervalSeconds;
        var start = Math.Max(1, (int)Math.Floor(traffic) + 1);

        if (request.MaxOccupancyPercent is > 0 and < 100)
        {
            start = Math.Max(start, (int)Math.Ceiling(traffic / (request.MaxOccupancyPercent.Value / 100d) - 1e-9));
        }

        StaffingResult result = null;

        for (var agents = start; agents <= MaxAgents; agents++)
        {
            result = Evaluate(agents, traffic, handle, request);

            var meetsServiceLevel = request.ServiceLevelPercent is not { } percent || result.ServiceLevel >= (percent / 100d) - 1e-9;
            var meetsAnswerTime = request.AverageSpeedOfAnswerSeconds is not { } asa || result.AverageSpeedOfAnswerSeconds <= asa + 1e-9;

            if (meetsServiceLevel && meetsAnswerTime)
            {
                return result;
            }
        }

        return result;
    }

    private static StaffingResult Evaluate(int agents, double traffic, double handle, StaffingRequest request)
    {
        if (request.PatienceSeconds is not { } patience || patience <= 0)
        {
            return new StaffingResult
            {
                Agents = agents,
                ServiceLevel = ServiceLevel(agents, traffic, handle, request.ServiceLevelSeconds),
                AverageSpeedOfAnswerSeconds = AverageSpeedOfAnswer(agents, traffic, handle),
                Occupancy = Math.Min(1, traffic / agents),
            };
        }

        // Erlang A: the chain gives who waits and who abandons. The answer times are approximated by Erlang C on the
        // load that is answered, with the chain's probability of waiting.
        var erlangA = ErlangA(agents, traffic, handle, patience);
        var answered = traffic * (1 - erlangA.AbandonRate);
        var spare = Math.Max(1e-9, agents - answered);

        return new StaffingResult
        {
            Agents = agents,
            ServiceLevel = 1 - (erlangA.ProbabilityOfWaiting * Math.Exp(-spare * request.ServiceLevelSeconds / handle)),
            AverageSpeedOfAnswerSeconds = erlangA.ProbabilityOfWaiting * handle / spare,
            Occupancy = Math.Min(1, answered / agents),
            AbandonRate = erlangA.AbandonRate,
        };
    }

    private static double ErlangB(int agents, double traffic)
    {
        var blocking = 1d;

        for (var n = 1; n <= agents; n++)
        {
            blocking = traffic * blocking / (n + (traffic * blocking));
        }

        return blocking;
    }
}
