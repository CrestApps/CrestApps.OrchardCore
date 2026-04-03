# Forms Examples

## Example 1: Contact Form with Workflow Processing

### Enabling Required Features

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Forms",
        "OrchardCore.Flows",
        "OrchardCore.Widgets",
        "OrchardCore.Workflows",
        "OrchardCore.Workflows.Http",
        "OrchardCore.Email"
      ],
      "disable": []
    }
  ]
}
```

### Contact Form Workflow Script - Validation

```javascript
// ScriptTask: Validate submitted contact form fields
var name = requestFormAsDict["ContactName"];
var email = requestFormAsDict["ContactEmail"];
var subject = requestFormAsDict["ContactSubject"];
var message = requestFormAsDict["ContactMessage"];

if (!name || name.trim() === "") {
    addModelError("ContactName", "Please enter your name.");
}

if (!email || !email.includes("@")) {
    addModelError("ContactEmail", "Please enter a valid email address.");
}

if (!subject || subject.trim() === "") {
    addModelError("ContactSubject", "Please enter a subject.");
}

if (!message || message.trim().length < 10) {
    addModelError("ContactMessage", "Message must be at least 10 characters.");
}

if (modelState.isValid) {
    setProperty("ContactName", name.trim());
    setProperty("ContactEmail", email.trim());
    setProperty("ContactSubject", subject.trim());
    setProperty("ContactMessage", message.trim());
    setOutcome("Valid");
} else {
    setOutcome("Invalid");
}
```

### Contact Form Workflow Script - Process Submission

```javascript
// ScriptTask: Prepare email content after validation passes
var name = workflow.properties["ContactName"];
var email = workflow.properties["ContactEmail"];
var subject = workflow.properties["ContactSubject"];

setProperty("EmailSubject", "Contact Form: " + subject);
setProperty("EmailSender", email);
setProperty("SenderName", name);

setOutcome("Done");
```

## Example 2: Building a Form Page Content Type via Migration

### Migration

```csharp
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;

public sealed class Migrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public Migrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterTypeDefinitionAsync("FormPage", type => type
            .DisplayedAs("Form Page")
            .Creatable()
            .Listable()
            .Draftable()
            .WithPart("TitlePart", part => part
                .WithPosition("0")
            )
            .WithPart("AutoroutePart", part => part
                .WithPosition("1")
                .WithSettings(new AutoroutePartSettings
                {
                    AllowCustomPath = true,
                    Pattern = "{{ ContentItem | display_text | slugify }}"
                })
            )
            .WithPart("FlowPart", part => part
                .WithPosition("2")
            )
        );

        return 1;
    }
}
```

## Example 3: Custom File Upload Form Element

### Content Part

```csharp
using OrchardCore.ContentManagement;

public sealed class FileUploadPart : ContentPart
{
    public string Name { get; set; }
    public string Label { get; set; }
    public string AcceptedFileTypes { get; set; } = ".pdf,.doc,.docx";
    public int MaxFileSizeKB { get; set; } = 5120;
    public bool IsRequired { get; set; }
}
```

### View Model

```csharp
public class FileUploadPartViewModel
{
    public string Name { get; set; }
    public string Label { get; set; }
    public string AcceptedFileTypes { get; set; }
    public int MaxFileSizeKB { get; set; }
    public bool IsRequired { get; set; }
}
```

### Display Driver

```csharp
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;

public sealed class FileUploadPartDisplayDriver : ContentPartDisplayDriver<FileUploadPart>
{
    public override IDisplayResult Display(FileUploadPart part, BuildPartDisplayContext context)
    {
        return Initialize<FileUploadPartViewModel>("FileUploadPart", model =>
        {
            model.Name = part.Name;
            model.Label = part.Label;
            model.AcceptedFileTypes = part.AcceptedFileTypes;
            model.MaxFileSizeKB = part.MaxFileSizeKB;
            model.IsRequired = part.IsRequired;
        }).Location("Detail", "Content");
    }

    public override IDisplayResult Edit(FileUploadPart part, BuildPartEditorContext context)
    {
        return Initialize<FileUploadPartViewModel>("FileUploadPart_Edit", model =>
        {
            model.Name = part.Name;
            model.Label = part.Label;
            model.AcceptedFileTypes = part.AcceptedFileTypes;
            model.MaxFileSizeKB = part.MaxFileSizeKB;
            model.IsRequired = part.IsRequired;
        }).Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(
        FileUploadPart part,
        UpdatePartEditorContext context)
    {
        var model = new FileUploadPartViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        part.Name = model.Name;
        part.Label = model.Label;
        part.AcceptedFileTypes = model.AcceptedFileTypes;
        part.MaxFileSizeKB = model.MaxFileSizeKB;
        part.IsRequired = model.IsRequired;

        return Edit(part, context);
    }
}
```

### Migration

```csharp
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;

public sealed class FileUploadMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public FileUploadMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync("FileUploadPart", part => part
            .Attachable()
            .WithDescription("Renders a file upload input element in a form.")
        );

        await _contentDefinitionManager.AlterTypeDefinitionAsync("FileUpload", type => type
            .DisplayedAs("File Upload")
            .Stereotype("Widget")
            .WithPart("FileUploadPart")
        );

        return 1;
    }
}
```

### Startup Registration

```csharp
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.Modules;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContentPart<FileUploadPart>()
            .UseDisplayDriver<FileUploadPartDisplayDriver>();
    }
}
```

### Razor View (FileUploadPart.cshtml)

```html
@model FileUploadPartViewModel

<div class="mb-3">
    @if (!string.IsNullOrEmpty(Model.Label))
    {
        <label for="@Model.Name" class="form-label">@Model.Label</label>
    }
    <input type="file"
           id="@Model.Name"
           name="@Model.Name"
           class="form-control"
           accept="@Model.AcceptedFileTypes"
           @(Model.IsRequired ? "required" : "") />
    <div class="form-text">
        Accepted file types: @Model.AcceptedFileTypes. Max size: @(Model.MaxFileSizeKB / 1024) MB.
    </div>
</div>
```

## Example 4: Custom Form Submission Handler

### Controller-Based Form Handler

```csharp
using Microsoft.AspNetCore.Mvc;
using OrchardCore.DisplayManagement.Notify;

public sealed class ContactFormController : Controller
{
    private readonly INotifier _notifier;

    public ContactFormController(INotifier notifier)
    {
        _notifier = notifier;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(ContactFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await _notifier.ErrorAsync(new LocalizedHtmlString("FormError",
                "Please correct the errors and try again."));

            return Redirect(Request.Headers["Referer"].ToString());
        }

        // Process the form submission
        await _notifier.SuccessAsync(new LocalizedHtmlString("FormSuccess",
            "Thank you for your message. We will get back to you soon."));

        return RedirectToAction("ThankYou");
    }
}
```

### View Model with Validation Attributes

```csharp
using System.ComponentModel.DataAnnotations;

public class ContactFormViewModel
{
    [Required(ErrorMessage = "Name is required.")]
    [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
    public string Name { get; set; }

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    public string Email { get; set; }

    [Required(ErrorMessage = "Subject is required.")]
    [StringLength(200, ErrorMessage = "Subject cannot exceed 200 characters.")]
    public string Subject { get; set; }

    [Required(ErrorMessage = "Message is required.")]
    [StringLength(2000, ErrorMessage = "Message cannot exceed 2000 characters.")]
    public string Message { get; set; }
}
```

## Example 5: AJAX Form Submission with Anti-Forgery Token

### JavaScript for AJAX Form Submission

```javascript
document.querySelector("#contactForm").addEventListener("submit", function (e) {
    e.preventDefault();

    var form = e.target;
    var formData = new FormData(form);
    var token = form.querySelector('input[name="__RequestVerificationToken"]').value;

    fetch(form.action, {
        method: "POST",
        headers: {
            "RequestVerificationToken": token
        },
        body: formData
    })
    .then(function (response) {
        if (response.ok) {
            return response.json();
        }
        throw new Error("Form submission failed.");
    })
    .then(function (data) {
        document.querySelector("#formResult").textContent = data.message;
        form.reset();
    })
    .catch(function (error) {
        document.querySelector("#formResult").textContent = error.message;
    });
});
```

## Example 6: Multi-Step Form Using Workflow Properties

### Workflow Script - Step 1: Capture Personal Info

```javascript
var firstName = requestFormAsDict["FirstName"];
var lastName = requestFormAsDict["LastName"];
var email = requestFormAsDict["Email"];

if (!firstName || !lastName || !email) {
    addModelError("", "All fields are required.");
    setOutcome("Invalid");
} else {
    setProperty("FirstName", firstName.trim());
    setProperty("LastName", lastName.trim());
    setProperty("Email", email.trim());
    setProperty("CurrentStep", "2");
    setOutcome("Valid");
}
```

### Workflow Script - Step 2: Capture Address Info

```javascript
var address = requestFormAsDict["Address"];
var city = requestFormAsDict["City"];
var postalCode = requestFormAsDict["PostalCode"];

if (!address || !city || !postalCode) {
    addModelError("", "All address fields are required.");
    setOutcome("Invalid");
} else {
    setProperty("Address", address.trim());
    setProperty("City", city.trim());
    setProperty("PostalCode", postalCode.trim());
    setProperty("CurrentStep", "3");
    setOutcome("Valid");
}
```

### Workflow Script - Final Step: Create Content Item

```javascript
// ScriptTask: Build the content item from collected workflow properties
var firstName = workflow.properties["FirstName"];
var lastName = workflow.properties["LastName"];
var email = workflow.properties["Email"];
var address = workflow.properties["Address"];
var city = workflow.properties["City"];
var postalCode = workflow.properties["PostalCode"];

setProperty("FullName", firstName + " " + lastName);
setProperty("Summary", firstName + " " + lastName + " from " + city);

setOutcome("Done");
```

## Example 7: Select Element with Dynamic Options via Liquid

### Liquid Template for Dynamic Select Options

```liquid
{% assign categories = ContentItem | query: type: "Category" %}
<select name="Category" class="form-select">
    <option value="">-- Select a Category --</option>
    {% for category in categories %}
        <option value="{{ category.ContentItemId }}">
            {{ category | display_text }}
        </option>
    {% endfor %}
</select>
```

## Example 8: Form Content Definition Recipe

```json
{
  "steps": [
    {
      "name": "ContentDefinition",
      "ContentTypes": [
        {
          "Name": "Form",
          "DisplayName": "Form",
          "Settings": {
            "ContentTypeSettings": {
              "Stereotype": "Widget"
            }
          },
          "ContentTypePartDefinitionRecords": [
            {
              "PartName": "FormPart",
              "Name": "FormPart",
              "Settings": {
                "ContentTypePartSettings": {
                  "Position": "0"
                }
              }
            },
            {
              "PartName": "FlowPart",
              "Name": "FlowPart",
              "Settings": {
                "ContentTypePartSettings": {
                  "Position": "1"
                }
              }
            }
          ]
        },
        {
          "Name": "Input",
          "DisplayName": "Input",
          "Settings": {
            "ContentTypeSettings": {
              "Stereotype": "Widget"
            }
          },
          "ContentTypePartDefinitionRecords": [
            {
              "PartName": "FormElementPart",
              "Name": "FormElementPart",
              "Settings": {
                "ContentTypePartSettings": {
                  "Position": "0"
                }
              }
            },
            {
              "PartName": "FormInputElementPart",
              "Name": "FormInputElementPart",
              "Settings": {
                "ContentTypePartSettings": {
                  "Position": "1"
                }
              }
            }
          ]
        },
        {
          "Name": "Button",
          "DisplayName": "Button",
          "Settings": {
            "ContentTypeSettings": {
              "Stereotype": "Widget"
            }
          },
          "ContentTypePartDefinitionRecords": [
            {
              "PartName": "FormElementPart",
              "Name": "FormElementPart",
              "Settings": {
                "ContentTypePartSettings": {
                  "Position": "0"
                }
              }
            },
            {
              "PartName": "ButtonPart",
              "Name": "ButtonPart",
              "Settings": {
                "ContentTypePartSettings": {
                  "Position": "1"
                }
              }
            }
          ]
        }
      ]
    }
  ]
}
```
