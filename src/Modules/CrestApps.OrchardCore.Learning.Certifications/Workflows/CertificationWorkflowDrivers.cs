using CrestApps.OrchardCore.Skills.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Workflows.Display;

namespace CrestApps.OrchardCore.Learning.Certifications.Workflows;

public class CertificationPathEventViewModel
{
    public string[] CertificationIds { get; set; } = [];

    [BindNever]
    public List<SelectListItem> CertificationOptions { get; set; } = [];
}

public class StartCertificationPathTaskViewModel
{
    public string Certification { get; set; }

    public string UserId { get; set; }
}

public sealed class CertificationPathEventDisplayDriver<TActivity> : ActivityDisplayDriver<TActivity, CertificationPathEventViewModel>
    where TActivity : CertificationPathEventActivity
{
    private readonly ICertificationManager _certifications;

    public CertificationPathEventDisplayDriver(ICertificationManager certifications)
    {
        _certifications = certifications;
    }

    protected override async ValueTask EditActivityAsync(TActivity activity, CertificationPathEventViewModel model)
    {
        model.CertificationIds = activity.CertificationIds.ToArray();
        model.CertificationOptions = (await _certifications.GetEnabledAsync())
            .Select(certification => new SelectListItem(certification.Name, certification.ItemId, model.CertificationIds.Contains(certification.ItemId)))
            .ToList();
    }

    protected override void UpdateActivity(CertificationPathEventViewModel model, TActivity activity)
        => activity.CertificationIds = (model.CertificationIds ?? []).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
}

public sealed class StartCertificationPathTaskDisplayDriver : ActivityDisplayDriver<StartCertificationPathTask, StartCertificationPathTaskViewModel>
{
    protected override void EditActivity(StartCertificationPathTask activity, StartCertificationPathTaskViewModel model)
    {
        model.Certification = activity.Certification;
        model.UserId = activity.UserId;
    }

    protected override void UpdateActivity(StartCertificationPathTaskViewModel model, StartCertificationPathTask activity)
    {
        activity.Certification = model.Certification?.Trim();
        activity.UserId = model.UserId?.Trim();
    }
}
