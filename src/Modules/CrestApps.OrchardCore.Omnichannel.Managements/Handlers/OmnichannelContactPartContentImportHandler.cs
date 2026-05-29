using System.Data;
using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.ContentTransfer.Handlers;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentFields.Fields;
using OrchardCore.ContentManagement;
using OrchardCore.Flows.Models;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Handlers;

/// <summary>
/// Handles import and export of omnichannel contact communication methods
/// (email, cell phone, home phone) for the <see cref="OmnichannelContactPart"/>.
/// Maps these columns to and from the BagPart named "ContactMethods".
/// </summary>
public sealed class OmnichannelContactPartContentImportHandler : ContentImportHandlerBase, IContentPartImportHandler
{
    internal readonly IStringLocalizer S;

    private ImportColumn _emailColumn;
    private ImportColumn _cellPhoneColumn;
    private ImportColumn _homePhoneColumn;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelContactPartContentImportHandler"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public OmnichannelContactPartContentImportHandler(IStringLocalizer<OmnichannelContactPartContentImportHandler> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<ImportColumn> GetColumns(ImportContentPartContext context)
    {
        _emailColumn ??= new ImportColumn()
        {
            Name = $"{OmnichannelConstants.NamedParts.ContactMethods}_Email",
            Description = S["The primary email address for the contact."],
            AdditionalNames = ["Email", "EmailAddress", "Email Address"],
        };

        _cellPhoneColumn ??= new ImportColumn()
        {
            Name = $"{OmnichannelConstants.NamedParts.ContactMethods}_CellPhone",
            Description = S["The primary cell phone number for the contact."],
            AdditionalNames = ["CellPhone", "Cell Phone", "Cell", "Mobile", "MobilePhone", "Mobile Phone"],
        };

        _homePhoneColumn ??= new ImportColumn()
        {
            Name = $"{OmnichannelConstants.NamedParts.ContactMethods}_HomePhone",
            Description = S["The primary home/landline phone number for the contact."],
            AdditionalNames = ["HomePhone", "Home Phone", "Phone", "PhoneNumber", "Phone Number", "Landline"],
        };

        return [_emailColumn, _cellPhoneColumn, _homePhoneColumn];
    }

    /// <inheritdoc/>
    public Task ImportAsync(ContentPartImportMapContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.ContentItem, nameof(context.ContentItem));
        ArgumentNullException.ThrowIfNull(context.Columns, nameof(context.Columns));
        ArgumentNullException.ThrowIfNull(context.Row, nameof(context.Row));

        var columns = GetColumns(context);
        string email = null;
        string cellPhone = null;
        string homePhone = null;

        foreach (DataColumn column in context.Columns)
        {
            if (Is(column.ColumnName, _emailColumn))
            {
                email = context.Row[column]?.ToString()?.Trim();
            }
            else if (Is(column.ColumnName, _cellPhoneColumn))
            {
                cellPhone = context.Row[column]?.ToString()?.Trim();
            }
            else if (Is(column.ColumnName, _homePhoneColumn))
            {
                homePhone = context.Row[column]?.ToString()?.Trim();
            }
        }

        if (string.IsNullOrEmpty(email) && string.IsNullOrEmpty(cellPhone) && string.IsNullOrEmpty(homePhone))
        {
            return Task.CompletedTask;
        }

        var bagPart = context.ContentItem.GetOrCreate<BagPart>(OmnichannelConstants.NamedParts.ContactMethods);
        bagPart.ContentItems ??= [];

        if (!string.IsNullOrEmpty(email))
        {
            var emailItem = CreateEmailAddressContentItem(email);
            bagPart.ContentItems.Add(emailItem);
        }

        if (!string.IsNullOrEmpty(cellPhone))
        {
            var cellPhoneItem = CreatePhoneNumberContentItem(cellPhone, "Cell");
            bagPart.ContentItems.Add(cellPhoneItem);
        }

        if (!string.IsNullOrEmpty(homePhone))
        {
            var homePhoneItem = CreatePhoneNumberContentItem(homePhone, "Home");
            bagPart.ContentItems.Add(homePhoneItem);
        }

        context.ContentItem.Apply(OmnichannelConstants.NamedParts.ContactMethods, bagPart);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ExportAsync(ContentPartExportMapContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.ContentItem, nameof(context.ContentItem));
        ArgumentNullException.ThrowIfNull(context.Row, nameof(context.Row));

        if (!context.ContentItem.TryGet<BagPart>(OmnichannelConstants.NamedParts.ContactMethods, out var bagPart) ||
            bagPart.ContentItems is null ||
            bagPart.ContentItems.Count == 0)
        {
            return Task.CompletedTask;
        }

        string email = null;
        string cellPhone = null;
        string homePhone = null;

        foreach (var contentMethod in bagPart.ContentItems)
        {
            if (email == null &&
                contentMethod.ContentType == OmnichannelConstants.ContentTypes.EmailAddress &&
                contentMethod.TryGet<EmailInfoPart>(out var emailPart) &&
                !string.IsNullOrEmpty(emailPart.Email?.Text))
            {
                email = emailPart.Email.Text;
            }

            if (cellPhone == null &&
                contentMethod.ContentType == OmnichannelConstants.ContentTypes.PhoneNumber &&
                contentMethod.TryGet<PhoneNumberInfoPart>(out var cellPart) &&
                cellPart.Type?.Text == "Cell" &&
                !string.IsNullOrEmpty(cellPart.Number?.Text))
            {
                cellPhone = cellPart.Number.Text;
            }

            if (homePhone == null &&
                contentMethod.ContentType == OmnichannelConstants.ContentTypes.PhoneNumber &&
                contentMethod.TryGet<PhoneNumberInfoPart>(out var homePart) &&
                homePart.Type?.Text == "Home" &&
                !string.IsNullOrEmpty(homePart.Number?.Text))
            {
                homePhone = homePart.Number.Text;
            }

            if (email != null && cellPhone != null && homePhone != null)
            {
                break;
            }
        }

        if (_emailColumn?.Name != null && email != null)
        {
            context.Row[_emailColumn.Name] = email;
        }

        if (_cellPhoneColumn?.Name != null && cellPhone != null)
        {
            context.Row[_cellPhoneColumn.Name] = cellPhone;
        }

        if (_homePhoneColumn?.Name != null && homePhone != null)
        {
            context.Row[_homePhoneColumn.Name] = homePhone;
        }

        return Task.CompletedTask;
    }

    private static ContentItem CreateEmailAddressContentItem(string email)
    {
        var contentItem = new ContentItem();
        contentItem.ContentType = OmnichannelConstants.ContentTypes.EmailAddress;
        contentItem.DisplayText = email;

        contentItem.Alter<EmailInfoPart>(part =>
        {
            part.Email = new TextField { Text = email };
        });

        return contentItem;
    }

    private static ContentItem CreatePhoneNumberContentItem(string number, string type)
    {
        var contentItem = new ContentItem();
        contentItem.ContentType = OmnichannelConstants.ContentTypes.PhoneNumber;
        contentItem.DisplayText = $"{type}: {number}";

        contentItem.Alter<PhoneNumberInfoPart>(part =>
        {
            part.Number = new TextField { Text = number };
            part.Type = new TextField { Text = type };
        });

        return contentItem;
    }
}
