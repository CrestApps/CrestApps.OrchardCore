# OrchardCore.Email Practical Examples

## Recipe: Full SMTP Configuration

Apply this recipe to configure SMTP email delivery with TLS encryption:

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Email",
        "OrchardCore.Email.Smtp"
      ]
    },
    {
      "name": "Settings",
      "SmtpSettings": {
        "DefaultSender": "noreply@mysite.com",
        "Host": "smtp.mysite.com",
        "Port": 587,
        "EncryptionMethod": "STARTTLS",
        "AutoSelectEncryption": false,
        "RequireCredentials": true,
        "UserName": "email-user@mysite.com",
        "Password": "secure-password"
      }
    }
  ]
}
```

## Recipe: Azure Communication Services Configuration

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Email",
        "OrchardCore.Email.Azure"
      ]
    },
    {
      "name": "Settings",
      "AzureEmailSettings": {
        "DefaultSender": "DoNotReply@notify.mysite.com",
        "ConnectionString": "endpoint=https://myresource.communication.azure.com/;accesskey=BASE64_ACCESS_KEY"
      }
    }
  ]
}
```

## Sending a Simple Email

A minimal service that sends a plain-text email:

```csharp
using OrchardCore.Email;

public sealed class SimpleEmailSender
{
    private readonly ISmtpService _smtpService;

    public SimpleEmailSender(ISmtpService smtpService)
    {
        _smtpService = smtpService;
    }

    public async Task<bool> SendPlainTextAsync(string to, string subject, string body)
    {
        var message = new MailMessage
        {
            To = to,
            Subject = subject,
            Body = body,
            IsHtmlBody = false
        };

        var result = await _smtpService.SendAsync(message);

        return result.Succeeded;
    }
}
```

## Sending an HTML Email with CC and BCC

```csharp
using OrchardCore.Email;

public sealed class TeamNotificationService
{
    private readonly ISmtpService _smtpService;

    public TeamNotificationService(ISmtpService smtpService)
    {
        _smtpService = smtpService;
    }

    public async Task<SmtpResult> NotifyTeamAsync(
        string primaryRecipient,
        string ccRecipients,
        string bccRecipients,
        string projectName)
    {
        var message = new MailMessage
        {
            To = primaryRecipient,
            Cc = ccRecipients,
            Bcc = bccRecipients,
            Subject = $"Project Update: {projectName}",
            Body = $@"
<html>
<body>
    <h2>Project Update</h2>
    <p>The project <strong>{projectName}</strong> has been updated.</p>
    <p>Please review the latest changes at your earliest convenience.</p>
</body>
</html>",
            IsHtmlBody = true
        };

        return await _smtpService.SendAsync(message);
    }
}
```

## Liquid-Templated Email with Dynamic Content

Render a Liquid template with model data before sending:

```csharp
using OrchardCore.Email;
using OrchardCore.Liquid;

public sealed class InvoiceEmailService
{
    private readonly ISmtpService _smtpService;
    private readonly ILiquidTemplateManager _liquidTemplateManager;

    public InvoiceEmailService(
        ISmtpService smtpService,
        ILiquidTemplateManager liquidTemplateManager)
    {
        _smtpService = smtpService;
        _liquidTemplateManager = liquidTemplateManager;
    }

    public async Task<SmtpResult> SendInvoiceNotificationAsync(
        string recipientEmail,
        string customerName,
        string invoiceNumber,
        decimal totalAmount,
        DateTime dueDate)
    {
        var template = @"
<html>
<body>
    <h1>Invoice {{ InvoiceNumber }}</h1>
    <p>Dear {{ CustomerName }},</p>
    <p>Your invoice details:</p>
    <table border='1' cellpadding='8'>
        <tr><td><strong>Invoice Number</strong></td><td>{{ InvoiceNumber }}</td></tr>
        <tr><td><strong>Total Amount</strong></td><td>${{ TotalAmount }}</td></tr>
        <tr><td><strong>Due Date</strong></td><td>{{ DueDate | date: '%B %d, %Y' }}</td></tr>
    </table>
    {% if TotalAmount > 1000 %}
    <p style='color: red;'><strong>Note:</strong> This invoice exceeds $1,000. Please ensure timely payment.</p>
    {% endif %}
    <p>Thank you for your business.</p>
</body>
</html>";

        var model = new
        {
            CustomerName = customerName,
            InvoiceNumber = invoiceNumber,
            TotalAmount = totalAmount,
            DueDate = dueDate
        };

        var body = await _liquidTemplateManager.RenderStringAsync(
            template,
            System.Text.Encodings.Web.HtmlEncoder.Default,
            model);

        var message = new MailMessage
        {
            To = recipientEmail,
            Subject = $"Invoice {invoiceNumber} - Amount Due: ${totalAmount:F2}",
            Body = body,
            IsHtmlBody = true
        };

        return await _smtpService.SendAsync(message);
    }
}
```

## Liquid Template with Collection Iteration

```csharp
using OrchardCore.Email;
using OrchardCore.Liquid;

public sealed class OrderSummaryEmailService
{
    private readonly ISmtpService _smtpService;
    private readonly ILiquidTemplateManager _liquidTemplateManager;

    public OrderSummaryEmailService(
        ISmtpService smtpService,
        ILiquidTemplateManager liquidTemplateManager)
    {
        _smtpService = smtpService;
        _liquidTemplateManager = liquidTemplateManager;
    }

    public async Task<SmtpResult> SendOrderSummaryAsync(
        string recipientEmail,
        string orderId,
        IEnumerable<OrderLineItem> lineItems)
    {
        var template = @"
<h2>Order Summary: {{ OrderId }}</h2>
<table border='1' cellpadding='6'>
    <tr>
        <th>Product</th>
        <th>Quantity</th>
        <th>Price</th>
    </tr>
    {% for item in LineItems %}
    <tr>
        <td>{{ item.ProductName }}</td>
        <td>{{ item.Quantity }}</td>
        <td>${{ item.Price }}</td>
    </tr>
    {% endfor %}
</table>
<p><strong>Total Items:</strong> {{ LineItems | size }}</p>";

        var model = new
        {
            OrderId = orderId,
            LineItems = lineItems
        };

        var body = await _liquidTemplateManager.RenderStringAsync(
            template,
            System.Text.Encodings.Web.HtmlEncoder.Default,
            model);

        var message = new MailMessage
        {
            To = recipientEmail,
            Subject = $"Order Summary - {orderId}",
            Body = body,
            IsHtmlBody = true
        };

        return await _smtpService.SendAsync(message);
    }
}

public sealed class OrderLineItem
{
    public string ProductName { get; set; }
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}
```

## Email with File Attachments

```csharp
using OrchardCore.Email;

public sealed class DocumentEmailService
{
    private readonly ISmtpService _smtpService;

    public DocumentEmailService(ISmtpService smtpService)
    {
        _smtpService = smtpService;
    }

    public async Task<SmtpResult> SendDocumentAsync(
        string recipientEmail,
        string documentName,
        byte[] documentBytes,
        string mimeType)
    {
        var memoryStream = new MemoryStream(documentBytes);

        var message = new MailMessage
        {
            To = recipientEmail,
            Subject = $"Document: {documentName}",
            Body = $"<p>Please find the attached document: <strong>{documentName}</strong>.</p>",
            IsHtmlBody = true
        };

        message.Attachments.Add(new MailMessageAttachment
        {
            Filename = documentName,
            Stream = memoryStream
        });

        return await _smtpService.SendAsync(message);
    }
}
```

## Sending to Multiple Recipients

```csharp
using OrchardCore.Email;

public sealed class BulkNotificationService
{
    private readonly ISmtpService _smtpService;

    public BulkNotificationService(ISmtpService smtpService)
    {
        _smtpService = smtpService;
    }

    public async Task<IList<SmtpResult>> SendToMultipleRecipientsAsync(
        IEnumerable<string> recipientEmails,
        string subject,
        string htmlBody)
    {
        var results = new List<SmtpResult>();

        foreach (var email in recipientEmails)
        {
            var message = new MailMessage
            {
                To = email,
                Subject = subject,
                Body = htmlBody,
                IsHtmlBody = true
            };

            var result = await _smtpService.SendAsync(message);
            results.Add(result);
        }

        return results;
    }
}
```

## Custom Email Notification Event Handler

Track all outgoing emails for auditing purposes:

```csharp
using Microsoft.Extensions.Logging;
using OrchardCore.Email;
using OrchardCore.Email.Events;

public sealed class EmailAuditLogger : IEmailNotificationEvents
{
    private readonly ILogger<EmailAuditLogger> _logger;

    public EmailAuditLogger(ILogger<EmailAuditLogger> logger)
    {
        _logger = logger;
    }

    public Task SendingAsync(MailMessage message)
    {
        _logger.LogInformation(
            "Preparing to send email. To: {To}, Subject: {Subject}",
            message.To,
            message.Subject);

        return Task.CompletedTask;
    }

    public Task SentAsync(MailMessage message)
    {
        _logger.LogInformation(
            "Email delivered successfully. To: {To}, Subject: {Subject}",
            message.To,
            message.Subject);

        return Task.CompletedTask;
    }

    public Task FailedAsync(MailMessage message)
    {
        _logger.LogError(
            "Email delivery failed. To: {To}, Subject: {Subject}",
            message.To,
            message.Subject);

        return Task.CompletedTask;
    }
}
```

Register in `Startup`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Email.Events;
using OrchardCore.Modules;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IEmailNotificationEvents, EmailAuditLogger>();
    }
}
```

## Controller Action: Contact Form Email

Handle a form submission and send an email from a controller:

```csharp
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Email;

public sealed class ContactController : Controller
{
    private readonly ISmtpService _smtpService;

    public ContactController(ISmtpService smtpService)
    {
        _smtpService = smtpService;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(ContactFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var message = new MailMessage
        {
            To = "support@example.com",
            ReplyTo = model.Email,
            Subject = $"Contact Form: {model.Subject}",
            Body = $@"
<p><strong>From:</strong> {model.Name} ({model.Email})</p>
<p><strong>Subject:</strong> {model.Subject}</p>
<hr />
<p>{model.Message}</p>",
            IsHtmlBody = true
        };

        var result = await _smtpService.SendAsync(message);

        if (result.Succeeded)
        {
            TempData["Success"] = "Your message has been sent.";
            return RedirectToAction("ThankYou");
        }

        ModelState.AddModelError(string.Empty, "Unable to send your message. Please try again later.");

        return View(model);
    }
}

public class ContactFormViewModel
{
    public string Name { get; set; }
    public string Email { get; set; }
    public string Subject { get; set; }
    public string Message { get; set; }
}
```

## Workflow Liquid Examples

### Notify Author on Content Approval

Use the following Liquid in a **Send Email** workflow activity body to notify a content author:

```
<p>Hello {{ ContentItem.Owner }},</p>
<p>Your content item <strong>{{ ContentItem.DisplayText }}</strong> has been approved and is now published.</p>
<p>Content Type: {{ ContentItem.ContentType }}</p>
<p>Published Date: {{ ContentItem.PublishedUtc | date: "%B %d, %Y at %I:%M %p" }}</p>
```

### Password Reset Notification

```
<p>Hello {{ User.UserName }},</p>
<p>We received a request to reset your password.</p>
<p>Click the link below to reset your password:</p>
<p><a href="{{ ResetUrl }}">Reset Password</a></p>
<p>If you did not request this, please ignore this email.</p>
```

### Weekly Summary with Conditional Content

```
<h2>Weekly Summary for {{ SiteName }}</h2>
<p>Here is your weekly activity summary:</p>
<ul>
    <li>New Users: {{ Stats.NewUsers }}</li>
    <li>Published Articles: {{ Stats.PublishedArticles }}</li>
    <li>Comments: {{ Stats.Comments }}</li>
</ul>
{% if Stats.NewUsers > 100 %}
<p style="color: green;"><strong>Great week!</strong> User signups exceeded 100.</p>
{% endif %}
{% if Stats.PublishedArticles == 0 %}
<p style="color: orange;"><strong>Heads up:</strong> No articles were published this week.</p>
{% endif %}
```

## Recipe: Enable Email with Workflow

Enable both email and workflows features together and configure SMTP in a single recipe:

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Email",
        "OrchardCore.Email.Smtp",
        "OrchardCore.Workflows",
        "OrchardCore.Workflows.Http"
      ]
    },
    {
      "name": "Settings",
      "SmtpSettings": {
        "DefaultSender": "workflows@mysite.com",
        "Host": "smtp.mysite.com",
        "Port": 465,
        "EncryptionMethod": "SSLTLS",
        "RequireCredentials": true,
        "UserName": "workflows@mysite.com",
        "Password": "workflow-email-password"
      }
    }
  ]
}
```

## Configuration via appsettings.json

Override email settings per environment without recipes:

```json
{
  "OrchardCore_Email_Smtp": {
    "DefaultSender": "dev-noreply@localhost",
    "Host": "localhost",
    "Port": 1025,
    "EncryptionMethod": "None",
    "RequireCredentials": false
  }
}
```

This is useful for local development with tools like MailHog or Papercut that capture emails on a local SMTP port without actual delivery.
