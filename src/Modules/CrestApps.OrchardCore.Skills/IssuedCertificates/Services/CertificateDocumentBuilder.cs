using System.Globalization;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Pdf;
using CrestApps.OrchardCore.Pdf.Models;
using CrestApps.OrchardCore.Skills.IssuedCertificates.Models;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using Fluid;
using Fluid.Values;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;
using OrchardCore.Liquid;
using OrchardCore.Media;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Skills.IssuedCertificates.Services;

/// <summary>
/// Prints a certificate: the certification's template (or the default design) with its Liquid blocks rendered for the
/// certificate, the background image from the media library, the template's font, and the QR code of the verification
/// page.
/// </summary>
public sealed class CertificateDocumentBuilder
{
    private const int MaxBackgroundBytes = 10 * 1024 * 1024;

    private readonly IContentManager _contentManager;
    private readonly ILiquidTemplateManager _liquidTemplateManager;
    private readonly IMediaFileStore _mediaFileStore;
    private readonly IPdfDocumentRenderer _renderer;
    private readonly ISiteService _siteService;
    private readonly TemplateOptions _templateOptions;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CertificateDocumentBuilder"/> class.
    /// </summary>
    public CertificateDocumentBuilder(
        IContentManager contentManager,
        ILiquidTemplateManager liquidTemplateManager,
        IMediaFileStore mediaFileStore,
        IPdfDocumentRenderer renderer,
        ISiteService siteService,
        IOptions<TemplateOptions> templateOptions,
        ILogger<CertificateDocumentBuilder> logger,
        IStringLocalizer<CertificateDocumentBuilder> stringLocalizer)
    {
        _contentManager = contentManager;
        _liquidTemplateManager = liquidTemplateManager;
        _mediaFileStore = mediaFileStore;
        _renderer = renderer;
        _siteService = siteService;
        _templateOptions = templateOptions.Value;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <summary>
    /// Prints a certificate.
    /// </summary>
    /// <param name="record">The certificate.</param>
    /// <param name="certification">Its certification.</param>
    /// <param name="verificationUrl">The absolute address of its verification page.</param>
    /// <returns>The PDF file.</returns>
    public async Task<byte[]> BuildAsync(CertificationRecord record, Certification certification, string verificationUrl)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(certification);

        var templateItem = string.IsNullOrEmpty(certification.CertificateTemplateId)
            ? null
            : await _contentManager.GetAsync(certification.CertificateTemplateId, VersionOptions.Published);
        var template = templateItem is not null && templateItem.TryGet<CertificateTemplatePart>(out var part) && part.Blocks.Count > 0
            ? part
            : CertificateTemplateDefaults.CreatePart();
        var site = await _siteService.GetSiteSettingsAsync();

        var size = string.Equals(template.PageSize, "A4", StringComparison.OrdinalIgnoreCase) ? PdfPageSize.A4 : PdfPageSize.Letter;

        if (template.Landscape)
        {
            size = size.Landscape();
        }

        var accent = PdfColor.Parse(template.AccentColor, new PdfColor(30, 58, 138));
        var textColor = PdfColor.Parse(template.TextColor, new PdfColor(17, 24, 39));
        var page = new PdfPageLayout(size)
        {
            BackgroundImage = await ReadBackgroundAsync(templateItem),
        };

        if (template.ShowBorder)
        {
            page.Elements.Add(new PdfRectangleElement { X = 24, Top = 24, Width = size.Width - 48, Height = size.Height - 48, Outline = accent, OutlineThickness = 3 });
            page.Elements.Add(new PdfRectangleElement { X = 32, Top = 32, Width = size.Width - 64, Height = size.Height - 64, Outline = accent, OutlineThickness = 0.75 });
        }

        var properties = new Dictionary<string, FluidValue>
        {
            ["Certificate"] = FluidValue.Create(new CertificateLiquidValues
            {
                HolderName = record.SubjectName,
                Number = record.Number,
                IssuedOn = FormatDate(record.IssuedOn),
                ExpiresOn = FormatDate(record.ExpiresOn),
                Evidence = record.Evidence,
                VerificationCode = CertificateVerificationCodes.Format(record.VerificationCode),
                VerificationUrl = verificationUrl,
            }, _templateOptions),
            ["Certification"] = FluidValue.Create(new CertificationLiquidValues
            {
                Name = certification.Name,
                Issuer = string.IsNullOrWhiteSpace(certification.Issuer) ? site.SiteName : certification.Issuer,
                Description = certification.Description,
            }, _templateOptions),
        };

        foreach (var block in template.Blocks.Where(block => !string.IsNullOrWhiteSpace(block.Text)))
        {
            var text = await RenderAsync(block.Text, properties);

            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            page.Elements.Add(new PdfTextElement
            {
                Text = text.Trim(),
                X = block.X,
                Top = block.Top,
                Width = block.Width > 0 ? block.Width : Math.Max(0, size.Width - (2 * block.X)),
                FontSize = block.FontSize > 0 ? block.FontSize : 14,
                Bold = block.Bold,
                Alignment = block.Alignment,
                Color = block.Accent ? accent : textColor,
                MaxLines = block.MaxLines,
            });
        }

        if (template.ShowQrCode && !string.IsNullOrEmpty(verificationUrl) && template.QrSize > 0)
        {
            page.Elements.Add(new PdfQrCodeElement { Value = verificationUrl, X = template.QrX, Top = template.QrTop, Size = template.QrSize, Color = accent });
        }

        if (record.Status == CertificationStatus.Revoked)
        {
            page.Elements.Add(new PdfTextElement
            {
                Text = S["Revoked"],
                X = 0,
                Top = (size.Height / 2) - 40,
                Width = size.Width,
                FontSize = 72,
                Bold = true,
                Alignment = PdfTextAlignment.Center,
                Color = new PdfColor(220, 38, 38),
            });
        }

        var layout = new PdfDocumentLayout
        {
            Title = $"{certification.Name} {record.Number}".Trim(),
            Author = string.IsNullOrWhiteSpace(certification.Issuer) ? site.SiteName : certification.Issuer,
        };

        var fonts = PdfFontSet.FromInstalledFamily(template.FontFamily);

        layout.RegularFont = fonts.Regular;
        layout.BoldFont = fonts.Bold;
        layout.Pages.Add(page);

        return _renderer.Render(layout);
    }

    private async Task<string> RenderAsync(string template, Dictionary<string, FluidValue> properties)
    {
        try
        {
            return await _liquidTemplateManager.RenderStringAsync(template, NullEncoder.Default, properties);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A block with broken Liquid prints as written, so the certificate still prints and the mistake shows.
            _logger.LogWarning(exception, "A certificate template block could not be rendered.");

            return template;
        }
    }

    private async Task<byte[]> ReadBackgroundAsync(ContentItem templateItem)
    {
        // Read the media field from the item as JSON: indexing the dynamic Content hands back a wrapper, never a JsonArray.
        var content = templateItem is null ? null : (JsonObject)templateItem.Content;
        var path = (content?["CertificateTemplatePart"]?["BackgroundImage"]?["Paths"] as JsonArray)?.FirstOrDefault()?.GetValue<string>();

        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            var entry = await _mediaFileStore.GetFileInfoAsync(path);

            if (entry is null || entry.Length > MaxBackgroundBytes)
            {
                return null;
            }

            await using var stream = await _mediaFileStore.GetFileStreamAsync(entry);
            using var memory = new MemoryStream();

            await stream.CopyToAsync(memory);

            return memory.ToArray();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "The background image '{Path}' of a certificate template could not be read.", path);

            return null;
        }
    }

    // Certificate dates are calendar days, not moments: they are written out as they are, in the site's culture,
    // without the day of the week.
    private static string FormatDate(DateOnly? date)
    {
        if (date is not { } value)
        {
            return null;
        }

        var culture = CultureInfo.CurrentCulture;
        var pattern = culture.DateTimeFormat.LongDatePattern.Replace("dddd, ", string.Empty).Replace("dddd ", string.Empty).Replace(", dddd", string.Empty);

        return value.ToString(pattern, culture);
    }
}
