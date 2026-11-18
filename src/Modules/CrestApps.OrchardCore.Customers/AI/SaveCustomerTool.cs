using System.Security.Claims;
using System.Text.Json;
using CrestApps.Core;
using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Customers.Services;
using CrestApps.OrchardCore.Customers.ViewModels;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Customers.AI;

/// <summary>
/// Creates a customer or changes one with the customer form's rules (<see cref="CustomerEditorRules"/>): a business needs a
/// company name, a customer needs a name, a company or an email to be found again, a guest's email must be valid, and a
/// customer is anonymized only by erasing them. Only the fields given change. Stricter than the form: an anonymized
/// customer is refused with a sentence, and an email given for a customer with an account is refused instead of ignored,
/// since it comes from their user account. It needs <see cref="CustomerPermissions.ManageCustomers"/>.
/// </summary>
internal sealed class SaveCustomerTool : CustomersAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveCustomer";

    private static readonly string[] _fieldNames =
    [
        "kind", "status", "displayName", "firstName", "lastName", "companyName", "taxId", "isTaxExempt", "taxExemptionReason", "email", "phone", "acceptsMarketing",
    ];

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "customerId": {
          "type": "string",
          "description": "The id of the customer to change, from listCustomers. Leave it out to create a customer."
        },
        "kind": {
          "type": "string",
          "enum": ["Person", "Business"],
          "description": "Whether the customer is a person or a business. A business needs a company name."
        },
        "status": {
          "type": "string",
          "enum": ["Active", "Disabled"],
          "description": "Active, or Disabled when the store stopped doing business with the customer. Their orders and history stay."
        },
        "displayName": {
          "type": "string",
          "description": "The name the store addresses the customer by. An empty text removes it."
        },
        "firstName": {
          "type": "string",
          "description": "The first name. An empty text removes it."
        },
        "lastName": {
          "type": "string",
          "description": "The last name. An empty text removes it."
        },
        "companyName": {
          "type": "string",
          "description": "The company of a business customer. An empty text removes it."
        },
        "taxId": {
          "type": "string",
          "description": "The tax id of a business customer, such as a VAT number. An empty text removes it."
        },
        "isTaxExempt": {
          "type": "boolean",
          "description": "Whether the customer pays no tax, such as a reseller or a charity the store holds a certificate for."
        },
        "taxExemptionReason": {
          "type": "string",
          "description": "Why the customer is exempt, such as a resale certificate number. Kept only while isTaxExempt is true."
        },
        "email": {
          "type": "string",
          "description": "The email of a guest customer. A customer with an account takes the email of their user account, so it cannot be set here."
        },
        "phone": {
          "type": "string",
          "description": "The phone number. It is kept in international form when it can be read. An empty text removes it."
        },
        "acceptsMarketing": {
          "type": "boolean",
          "description": "Whether the customer agreed to receive marketing. Order and payment messages are always sent."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Creates a customer or updates one: kind, status (Active or Disabled), names, company, tax id and exemption, phone, marketing consent, and email for guests only. A business needs a company name; an anonymized customer cannot be edited. Only the fields given change.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => CustomerPermissions.ManageCustomers;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var now = services.GetRequiredService<IClock>().UtcNow;
        Customer customer;
        var created = !Has(arguments, "customerId");

        if (created)
        {
            customer = new Customer
            {
                ItemId = UniqueId.GenerateId(),
                CreatedUtc = now,
                LastActivityUtc = now,
            };
        }
        else
        {
            (customer, var error) = await FindCustomerAsync(services, arguments, cancellationToken);

            if (customer is null)
            {
                return error;
            }

            if (customer.Status == CustomerStatus.Anonymized)
            {
                return "Nothing was changed: the customer's personal data was erased, so there is nothing personal to edit.";
            }

            if (!_fieldNames.Any(name => Has(arguments, name)))
            {
                return "Nothing was changed: give at least one field to change.";
            }
        }

        if (Has(arguments, "email") && !string.IsNullOrEmpty(customer.UserId))
        {
            return "Nothing was changed: the customer has an account, and their email comes from their user account. Leave email out.";
        }

        var model = created ? new CustomerEditViewModel() : CustomerEditorRules.ToViewModel(customer);
        var problems = new List<string>();

        if (Has(arguments, "kind"))
        {
            if (TryReadEnum(arguments, "kind", [CustomerKind.Person, CustomerKind.Business], out CustomerKind kind))
            {
                model.Kind = kind;
            }
            else
            {
                problems.Add("kind must be Person or Business.");
            }
        }

        if (Has(arguments, "status"))
        {
            if (TryReadEnum(arguments, "status", [CustomerStatus.Active, CustomerStatus.Disabled], out CustomerStatus status))
            {
                model.Status = status;
            }
            else
            {
                problems.Add("status must be Active or Disabled. A customer is anonymized by erasing their data with eraseCustomerData.");
            }
        }

        model.DisplayName = Has(arguments, "displayName") ? ReadString(arguments, "displayName") : model.DisplayName;
        model.FirstName = Has(arguments, "firstName") ? ReadString(arguments, "firstName") : model.FirstName;
        model.LastName = Has(arguments, "lastName") ? ReadString(arguments, "lastName") : model.LastName;
        model.CompanyName = Has(arguments, "companyName") ? ReadString(arguments, "companyName") : model.CompanyName;
        model.TaxId = Has(arguments, "taxId") ? ReadString(arguments, "taxId") : model.TaxId;
        model.IsTaxExempt = Has(arguments, "isTaxExempt") ? ReadBool(arguments, "isTaxExempt") ?? model.IsTaxExempt : model.IsTaxExempt;
        model.TaxExemptionReason = Has(arguments, "taxExemptionReason") ? ReadString(arguments, "taxExemptionReason") : model.TaxExemptionReason;
        model.Email = Has(arguments, "email") ? ReadString(arguments, "email") : model.Email;
        model.Phone = Has(arguments, "phone") ? ReadString(arguments, "phone") : model.Phone;
        model.AcceptsMarketing = Has(arguments, "acceptsMarketing") ? ReadBool(arguments, "acceptsMarketing") ?? model.AcceptsMarketing : model.AcceptsMarketing;

        foreach (var problem in CustomerEditorRules.Check(model, customer))
        {
            problems.Add(problem switch
            {
                CustomerEditProblem.InvalidEmail => "Enter a valid email address.",
                CustomerEditProblem.CompanyNameRequired => "A business needs a company name.",
                CustomerEditProblem.NameRequired => "Enter a name, a company or an email, so the customer can be found again.",
                _ => "A customer is anonymized by erasing them, not by changing their status.",
            });
        }

        if (problems.Count > 0)
        {
            return $"Nothing was saved: {string.Join(" ", problems)}";
        }

        CustomerEditorRules.Apply(model, customer, now);

        if (created)
        {
            await services.GetRequiredService<ICustomerStore>().CreateAsync(customer, cancellationToken);
        }
        else if (await SaveCustomerAsync(services, customer, cancellationToken) is { } saveError)
        {
            return saveError;
        }

        return Json(new
        {
            id = customer.ItemId,
            name = customer.GetName(),
            kind = customer.Kind.ToString(),
            status = customer.Status.ToString(),
            companyName = customer.CompanyName,
            taxId = customer.TaxId,
            isTaxExempt = customer.IsTaxExempt,
            taxExemptionReason = customer.TaxExemptionReason,
            email = customer.Email,
            phone = customer.Phone,
            acceptsMarketing = customer.AcceptsMarketing,
            created,
            message = created ? $"The customer {customer.GetName()} was added." : $"The customer {customer.GetName()} was saved.",
        });
    }
}
