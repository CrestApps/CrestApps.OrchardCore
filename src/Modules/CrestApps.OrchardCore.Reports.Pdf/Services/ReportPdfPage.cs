using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Graphics.Core;
using UglyToad.PdfPig.Graphics.Operations;
using UglyToad.PdfPig.Graphics.Operations.General;
using UglyToad.PdfPig.Graphics.Operations.PathConstruction;
using UglyToad.PdfPig.Graphics.Operations.PathPainting;
using UglyToad.PdfPig.Writer;

namespace CrestApps.OrchardCore.Reports.Pdf.Services;

/// <summary>
/// Draws on a page of a report PDF. Positions are in points from the top left of the page, as the report is laid out;
/// the page turns them into the coordinates of PDF, which start at the bottom left.
/// </summary>
internal sealed class ReportPdfPage
{
    private readonly PdfPageBuilder _page;
    private readonly double _height;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportPdfPage"/> class.
    /// </summary>
    /// <param name="page">The page of the PdfPig document.</param>
    /// <param name="height">The height of the page, in points.</param>
    public ReportPdfPage(
        PdfPageBuilder page,
        double height)
    {
        _page = page;
        _height = height;
    }

    private List<IGraphicsStateOperation> Operations => _page.CurrentStream.Operations;

    /// <summary>
    /// Fills a rectangle, without an outline.
    /// </summary>
    /// <param name="x">The left of the rectangle.</param>
    /// <param name="top">The top of the rectangle.</param>
    /// <param name="width">The width of the rectangle.</param>
    /// <param name="height">The height of the rectangle.</param>
    /// <param name="color">The color.</param>
    public void FillRectangle(double x, double top, double width, double height, ReportPdfColor color)
    {
        if (width <= 0 || height <= 0)
        {
            return;
        }

        SetFillColor(color);
        Operations.Add(new AppendRectangle(x, _height - top - height, width, height));
        Operations.Add(FillPathNonZeroWinding.Value);
    }

    /// <summary>
    /// Draws a straight line.
    /// </summary>
    /// <param name="x1">The left of the first point.</param>
    /// <param name="top1">The top of the first point.</param>
    /// <param name="x2">The left of the second point.</param>
    /// <param name="top2">The top of the second point.</param>
    /// <param name="color">The color.</param>
    /// <param name="width">The width of the line.</param>
    public void DrawLine(double x1, double top1, double x2, double top2, ReportPdfColor color, double width)
    {
        DrawPolyline([(x1, top1), (x2, top2)], color, width);
    }

    /// <summary>
    /// Draws the segments joining points.
    /// </summary>
    /// <param name="points">The points, from the top left of the page.</param>
    /// <param name="color">The color.</param>
    /// <param name="width">The width of the line.</param>
    public void DrawPolyline(IReadOnlyList<(double X, double Top)> points, ReportPdfColor color, double width)
    {
        if (points.Count < 2)
        {
            return;
        }

        Operations.Add(new SetStrokeColorDeviceRgb(color.R / 255d, color.G / 255d, color.B / 255d));
        Operations.Add(new SetLineWidth(width));
        Operations.Add(new SetLineJoin(LineJoinStyle.Round));
        Operations.Add(new BeginNewSubpath(points[0].X, _height - points[0].Top));

        for (var index = 1; index < points.Count; index++)
        {
            Operations.Add(new AppendStraightLineSegment(points[index].X, _height - points[index].Top));
        }

        Operations.Add(StrokePath.Value);
    }

    /// <summary>
    /// Fills the polygon of points, without an outline.
    /// </summary>
    /// <param name="points">The corners of the polygon, from the top left of the page.</param>
    /// <param name="color">The color.</param>
    public void FillPolygon(IReadOnlyList<(double X, double Top)> points, ReportPdfColor color)
    {
        if (points.Count < 3)
        {
            return;
        }

        SetFillColor(color);
        Operations.Add(new BeginNewSubpath(points[0].X, _height - points[0].Top));

        for (var index = 1; index < points.Count; index++)
        {
            Operations.Add(new AppendStraightLineSegment(points[index].X, _height - points[index].Top));
        }

        Operations.Add(CloseSubpath.Value);
        Operations.Add(FillPathNonZeroWinding.Value);
    }

    /// <summary>
    /// Fills a circle.
    /// </summary>
    /// <param name="x">The left of the center.</param>
    /// <param name="top">The top of the center.</param>
    /// <param name="radius">The radius.</param>
    /// <param name="color">The color.</param>
    public void FillCircle(double x, double top, double radius, ReportPdfColor color)
    {
        FillWedge(x, top, radius, 0, 0, 2 * Math.PI, color);
    }

    /// <summary>
    /// Fills a slice of a pie, or of a ring when it has an inner radius.
    /// </summary>
    /// <param name="x">The left of the center.</param>
    /// <param name="top">The top of the center.</param>
    /// <param name="radius">The outer radius.</param>
    /// <param name="innerRadius">The inner radius, or 0 for a slice of a pie.</param>
    /// <param name="start">The angle the slice starts at, in radians clockwise from twelve o'clock.</param>
    /// <param name="sweep">The angle of the slice, in radians.</param>
    /// <param name="color">The color.</param>
    public void FillWedge(double x, double top, double radius, double innerRadius, double start, double sweep, ReportPdfColor color)
    {
        if (radius <= 0 || sweep <= 0)
        {
            return;
        }

        var centerY = _height - top;
        var (startX, startY) = Point(x, centerY, radius, start);

        SetFillColor(color);
        Operations.Add(new BeginNewSubpath(startX, startY));
        AppendArc(x, centerY, radius, start, sweep);

        if (innerRadius > 0)
        {
            var (innerX, innerY) = Point(x, centerY, innerRadius, start + sweep);

            Operations.Add(new AppendStraightLineSegment(innerX, innerY));
            AppendArc(x, centerY, innerRadius, start + sweep, -sweep);
        }
        else if (sweep < 2 * Math.PI)
        {
            Operations.Add(new AppendStraightLineSegment(x, centerY));
        }

        Operations.Add(CloseSubpath.Value);
        Operations.Add(FillPathNonZeroWinding.Value);
    }

    /// <summary>
    /// Writes a line of prepared text, outlining its letters when the face simulates bold.
    /// </summary>
    /// <param name="text">The text, from <see cref="ReportPdfFont.Prepare"/>, without line breaks.</param>
    /// <param name="font">The font.</param>
    /// <param name="size">The font size, in points.</param>
    /// <param name="x">The left of the text.</param>
    /// <param name="top">The top of the line of text.</param>
    /// <param name="color">The color.</param>
    public void DrawText(string text, ReportPdfFont font, double size, double x, double top, ReportPdfColor color)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        SetFillColor(color);

        if (font.SimulatesBold)
        {
            // A face without a bold file is made bold by outlining its letters in their own color.
            Operations.Add(new SetStrokeColorDeviceRgb(color.R / 255d, color.G / 255d, color.B / 255d));
            Operations.Add(new SetLineWidth(size / 30));
            _page.SetTextRenderingMode(TextRenderingMode.FillThenStroke);
        }

        font.Write(_page, text, size, x, _height - top - (font.Ascent * size));

        if (font.SimulatesBold)
        {
            _page.SetTextRenderingMode(TextRenderingMode.Fill);
        }
    }

    private void SetFillColor(ReportPdfColor color)
    {
        Operations.Add(new SetNonStrokeColorDeviceRgb(color.R / 255d, color.G / 255d, color.B / 255d));
    }

    // An arc is drawn as Bézier curves of at most a quarter turn each, which stay within a fraction of a point of the
    // circle.
    private void AppendArc(double x, double y, double radius, double start, double sweep)
    {
        var segments = (int)Math.Ceiling(Math.Abs(sweep) / (Math.PI / 2));
        var step = sweep / segments;
        var handle = 4d / 3 * Math.Tan(step / 4) * radius;

        for (var index = 0; index < segments; index++)
        {
            var from = start + (index * step);
            var to = from + step;
            var (fromX, fromY) = Point(x, y, radius, from);
            var (toX, toY) = Point(x, y, radius, to);

            // The tangent of a point clockwise from twelve o'clock, with y up, is (cos, -sin).
            Operations.Add(new AppendDualControlPointBezierCurve(
                fromX + (handle * Math.Cos(from)),
                fromY - (handle * Math.Sin(from)),
                toX - (handle * Math.Cos(to)),
                toY + (handle * Math.Sin(to)),
                toX,
                toY));
        }
    }

    private static (double X, double Y) Point(double x, double y, double radius, double angle)
    {
        return (x + (radius * Math.Sin(angle)), y + (radius * Math.Cos(angle)));
    }
}
