using System.Globalization;
using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Reads and writes the breaks and activities inside a shift as lines of text, one per line, such as
/// <c>13:00-13:30 Meal</c>. The activity is an activity code's name or short code.
/// </summary>
internal static class SegmentFormat
{
    private const int MinutesPerDay = 24 * 60;

    public static IList<SegmentLine> Read(string text, IEnumerable<ActivityCode> codes, ICollection<string> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        var known = (codes ?? []).ToArray();
        var segments = new List<SegmentLine>();

        foreach (var rawLine in (text ?? string.Empty).Split('\n'))
        {
            var line = rawLine.Trim();

            if (line.Length == 0)
            {
                continue;
            }

            var space = line.IndexOf(' ', StringComparison.Ordinal);
            var range = (space < 0 ? line : line.Substring(0, space)).Split('-');
            var name = space < 0 ? string.Empty : line.Substring(space + 1).Trim();
            var code = known.FirstOrDefault(code => string.Equals(code.Name, name, StringComparison.OrdinalIgnoreCase))
                ?? known.FirstOrDefault(code => !string.IsNullOrEmpty(code.Code) && string.Equals(code.Code, name, StringComparison.OrdinalIgnoreCase));

            if (code is null || range.Length != 2 || !TryParseTime(range[0], out var start) || !TryParseTime(range[1], out var end) || Length(start, end) is <= 0 or > 12 * 60)
            {
                errors.Add(line);

                continue;
            }

            segments.Add(new SegmentLine(code.ItemId, start, end));
        }

        return segments;
    }

    public static string Write(IEnumerable<SegmentLine> segments, IEnumerable<ActivityCode> codes, TimeOnly shiftStart)
    {
        var names = (codes ?? []).ToDictionary(code => code.ItemId, code => code.Name, StringComparer.Ordinal);

        return string.Join(Environment.NewLine, (segments ?? [])
            .OrderBy(segment => Offset(shiftStart, segment.Start))
            .Select(segment => $"{segment.Start:HH\\:mm}-{segment.End:HH\\:mm} {names.GetValueOrDefault(segment.ActivityCodeId ?? string.Empty, segment.ActivityCodeId)}"));
    }

    public static IList<ShiftTemplateSegment> ToTemplate(IEnumerable<SegmentLine> segments, TimeOnly shiftStart)
    {
        return (segments ?? []).Select(segment => new ShiftTemplateSegment
        {
            ActivityCodeId = segment.ActivityCodeId,
            OffsetMinutes = Offset(shiftStart, segment.Start),
            LengthMinutes = Length(segment.Start, segment.End),
        }).ToList();
    }

    public static IEnumerable<SegmentLine> FromTemplate(ShiftTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);

        return template.Segments.Select(segment => new SegmentLine(
            segment.ActivityCodeId,
            template.Start.AddMinutes(segment.OffsetMinutes),
            template.Start.AddMinutes(segment.OffsetMinutes + segment.LengthMinutes)));
    }

    public static IList<DraftShiftSegment> ToDraft(IEnumerable<SegmentLine> segments, DateTime shiftStart)
    {
        var start = TimeOnly.FromDateTime(shiftStart);

        return (segments ?? []).Select(segment =>
        {
            var from = shiftStart.AddMinutes(Offset(start, segment.Start));

            return new DraftShiftSegment
            {
                ActivityCodeId = segment.ActivityCodeId,
                Start = from,
                End = from.AddMinutes(Length(segment.Start, segment.End)),
            };
        }).ToList();
    }

    public static IEnumerable<SegmentLine> FromDraft(DraftShift shift)
    {
        ArgumentNullException.ThrowIfNull(shift);

        return shift.Segments.Select(segment => new SegmentLine(segment.ActivityCodeId, TimeOnly.FromDateTime(segment.Start), TimeOnly.FromDateTime(segment.End)));
    }

    private static int Offset(TimeOnly shiftStart, TimeOnly time)
        => (int)(((time.ToTimeSpan() - shiftStart.ToTimeSpan()).TotalMinutes + MinutesPerDay) % MinutesPerDay);

    private static int Length(TimeOnly start, TimeOnly end)
        => (int)(((end.ToTimeSpan() - start.ToTimeSpan()).TotalMinutes + MinutesPerDay) % MinutesPerDay);

    private static bool TryParseTime(string text, out TimeOnly time)
        => TimeOnly.TryParseExact(text.Trim(), ["HH:mm", "H:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out time);
}
