using QRCoder;

namespace CrestApps.OrchardCore.Pdf;

/// <summary>
/// Draws QR codes as vector squares.
/// </summary>
internal static class PdfQrCode
{
    /// <summary>
    /// Encodes a value, with medium error correction, as the modules of a QR code.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>
    /// The modules, <see langword="true"/> for a dark one, by row then column; the four light modules of the quiet zone
    /// are included on every side.
    /// </returns>
    public static bool[,] GetModules(string value)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(value ?? string.Empty, QRCodeGenerator.ECCLevel.M);

        var matrix = data.ModuleMatrix;
        var modules = new bool[matrix.Count, matrix.Count];

        for (var row = 0; row < matrix.Count; row++)
        {
            for (var column = 0; column < matrix.Count; column++)
            {
                modules[row, column] = matrix[row][column];
            }
        }

        return modules;
    }

    /// <summary>
    /// Draws a QR code on a white square: one rectangle for each run of dark modules in a row.
    /// </summary>
    /// <param name="canvas">The canvas.</param>
    /// <param name="value">The value.</param>
    /// <param name="x">The left of the code.</param>
    /// <param name="top">The top of the code.</param>
    /// <param name="size">The width and height of the code, quiet zone included.</param>
    /// <param name="color">The color of the dark modules.</param>
    public static void Draw(PdfCanvas canvas, string value, double x, double top, double size, Models.PdfColor color)
    {
        if (string.IsNullOrEmpty(value) || size <= 0)
        {
            return;
        }

        var modules = GetModules(value);
        var count = modules.GetLength(0);
        var module = size / count;

        canvas.FillRectangle(x, top, size, size, Models.PdfColor.White);

        for (var row = 0; row < count; row++)
        {
            var column = 0;

            while (column < count)
            {
                if (!modules[row, column])
                {
                    column++;

                    continue;
                }

                var start = column;

                while (column < count && modules[row, column])
                {
                    column++;
                }

                // Each row reaches a hair into the next, so no reader shows a seam between rows of dark modules.
                var height = row + 1 < count && modules[row + 1, start] ? module * 1.02 : module;

                canvas.FillRectangle(x + (start * module), top + (row * module), (column - start) * module, height, color);
            }
        }
    }
}
