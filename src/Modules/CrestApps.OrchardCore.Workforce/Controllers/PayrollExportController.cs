using System.Text;
using CrestApps.OrchardCore.Payroll;
using CrestApps.OrchardCore.Workforce.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.Modules;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Workforce.Controllers;

/// <summary>
/// The files the time clock's payroll export wrote before the export became a data pipeline over the Pay Inputs data
/// set, kept to download until they age out.
/// </summary>
[Feature(WorkforceConstants.Features.TimeClock)]
public sealed class PayrollExportController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly PayrollExportBatchStore _batches;

    public PayrollExportController(IAuthorizationService authorizationService, PayrollExportBatchStore batches)
    {
        _authorizationService = authorizationService;
        _batches = batches;
    }

    [Admin("workforce/payroll-export", "WorkforcePayrollExport")]
    public async Task<IActionResult> Index(PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, PayrollPermissions.ExportPayrollData))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var (total, batches) = await _batches.PageAsync(pager.GetStartIndex(), pager.PageSize, HttpContext.RequestAborted);

        return View(new PayrollExportViewModel
        {
            List = new AdminListContext<PayrollExportBatch>
            {
                Source = [.. batches],
                Pager = await shapeFactory.PagerAsync(pager, total),
            },
        });
    }

    [Admin("workforce/payroll-export/{id}/download", "WorkforcePayrollExportDownload")]
    public async Task<IActionResult> Download(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, PayrollPermissions.ExportPayrollData))
        {
            return Forbid();
        }

        var batch = string.IsNullOrEmpty(id) ? null : await _batches.FindByIdAsync(id, HttpContext.RequestAborted);

        if (batch is null)
        {
            return NotFound();
        }

        return File(Encoding.UTF8.GetBytes(batch.Content ?? string.Empty), batch.ContentType ?? "text/csv", batch.FileName ?? "payroll.csv");
    }
}
