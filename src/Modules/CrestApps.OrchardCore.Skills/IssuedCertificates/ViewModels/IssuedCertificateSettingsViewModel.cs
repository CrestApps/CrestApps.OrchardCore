using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Skills.IssuedCertificates.ViewModels;

public class IssuedCertificateSettingsViewModel
{
    public bool GenerateNumbers { get; set; }

    public string NumberPrefix { get; set; }

    public string CertificateTemplateId { get; set; }

    public string CertificationId { get; set; }

    [BindNever]
    public List<SelectListItem> TemplateOptions { get; set; } = [];
}
