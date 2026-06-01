using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.ContentTransfer.ViewModels;

public class ContentImportViewModel
{
    [Required]
    [DataType(DataType.Upload)]
    public IFormFile File { get; set; }

    [BindNever]
    public string AcceptedFileTypes { get; set; }
}
