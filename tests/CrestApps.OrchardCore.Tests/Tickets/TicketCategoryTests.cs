using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Conditions;
using CrestApps.OrchardCore.Tickets;
using CrestApps.OrchardCore.Tickets.Categories;
using CrestApps.OrchardCore.Tickets.Core.Services;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Portal;
using CrestApps.OrchardCore.Tickets.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Tests.Tickets;

/// <summary>
/// Ticket categories: read from a taxonomy with the routing each inherits, offered to the type's field rules, and the
/// fields customers don't fill in treated as hidden on their form.
/// </summary>
public sealed class TicketCategoryTests
{
    [Fact]
    public void ACategory_InheritsWhatItLeavesEmpty_FromTheCategoryAbove()
    {
        // Arrange: Hardware (desk IT, team Hardware, hidden from customers) > Laptop (priority Urgent) > Battery.
        var taxonomy = Taxonomy(
            Term("hardware", "Hardware", new TicketCategoryPart { DeskId = "it", TeamId = "hardware-team", HiddenFromCustomers = true },
                Term("laptop", "Laptop", new TicketCategoryPart { PriorityId = "urgent", CustomerDescription = "Have the serial number ready." },
                    Term("battery", "Battery", null))),
            Term("software", "Software", null));

        // Act
        var categories = TicketCategoryService.Read(taxonomy);

        // Assert
        var battery = categories.Single(category => category.Id == "battery");
        var software = categories.Single(category => category.Id == "software");
        Assert.Equal(["hardware", "laptop", "battery", "software"], categories.Select(category => category.Id));
        Assert.Equal("it", battery.DeskId);
        Assert.Equal("hardware-team", battery.TeamId);
        Assert.Equal("urgent", battery.PriorityId);
        Assert.Equal("/hardware/laptop/battery/", battery.Path);
        Assert.Equal("Hardware > Laptop > Battery", battery.PathText);
        Assert.Equal(2, battery.Level);
        Assert.False(battery.CustomersCanChoose);
        Assert.True(categories.Single(category => category.Id == "laptop").HasChildren);
        Assert.False(battery.HasChildren);
        Assert.True(software.CustomersCanChoose);
        Assert.Null(software.DeskId);
    }

    [Fact]
    public void ThePicker_LeavesOutCategoriesWhoseParentIsntOffered_AndMarksParentsWhenOnlyTheMostSpecificCounts()
    {
        // Arrange: Laptop's parent isn't offered (hidden from customers).
        var categories = new[]
        {
            new TicketCategory { Id = "software", Name = "Software" },
            new TicketCategory { Id = "access", Name = "Access", ParentId = "software", CustomerDescription = "Which system?" },
            new TicketCategory { Id = "laptop", Name = "Laptop", ParentId = "hardware" },
        };

        // Act
        var items = TicketCategoryForm.Items(categories, leavesOnly: true);

        // Assert
        Assert.Equal(["software", "access"], items.Select(item => item.Id));
        Assert.False(items[0].Selectable);
        Assert.True(items[1].Selectable);
        Assert.Equal("Which system?", items[1].Description);
    }

    [Fact]
    public async Task TheConditionSource_ReadsTheCategoryWithTheCategoriesAboveIt()
    {
        // Arrange
        var categories = new Mock<ITicketCategoryService>();
        categories.Setup(service => service.ListAsync("Request", false, It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new TicketCategory { Id = "hardware", Name = "Hardware", Path = "/hardware/" },
            new TicketCategory { Id = "laptop", Name = "Laptop", ParentId = "hardware", Level = 1, Path = "/hardware/laptop/" },
        ]);
        var source = new TicketCategoryConditionSource(categories.Object, new PassThroughStringLocalizer());
        var ticket = new ContentItem { ContentType = "Request" };
        ticket.Alter<TicketPart>(part =>
        {
            part.CategoryId = "laptop";
            part.CategoryPath = "/hardware/laptop/";
        });

        // Act
        var value = await source.ReadAsync(TicketsConstants.CategoryConditionSource, ticket);
        var other = await source.ReadAsync("Request.Model", ticket);
        var descriptor = Assert.Single(await source.DescribeAsync(RequestType()));

        // Assert
        Assert.Equal(["laptop"], value.Values);
        Assert.Equal(["hardware"], value.Ancestors);
        Assert.True(ConditionEvaluator.IsMet(new Condition { Source = "x", Operator = ConditionOperator.IsOrUnder, Values = ["hardware"] }, value));
        Assert.Null(other);
        Assert.Equal(TicketCategoryConditionSource.InputName, descriptor.InputName);
        Assert.Equal(["hardware", "laptop"], descriptor.Options.Select(option => option.Value));
        Assert.Equal(["hardware"], descriptor.Ancestors["laptop"]);
    }

    [Fact]
    public async Task OnTheCustomersForm_TheFieldsCustomersDontFillIn_AreHidden()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        var visibility = new TicketCustomerFieldsVisibility(new HttpContextAccessor { HttpContext = httpContext });
        var type = RequestType();

        // Act
        var outside = await visibility.GetHiddenFieldsAsync(new ContentItem(), type);
        TicketCustomerEditor.Begin(httpContext);
        var inside = await visibility.GetHiddenFieldsAsync(new ContentItem(), type);

        // Assert: a required internal field can't refuse a customer's request it never showed them.
        Assert.Empty(outside);
        Assert.Equal(["Request.RootCause"], inside);
    }

    private static ContentTypeDefinition RequestType()
        => new("Request", "Request",
        [
            new ContentTypePartDefinition(TicketsConstants.PartName, new ContentPartDefinition(TicketsConstants.PartName), []),
            new ContentTypePartDefinition("Request", new ContentPartDefinition("Request",
            [
                new ContentPartFieldDefinition(new ContentFieldDefinition("TextField"), "Model", new JsonObject
                {
                    [nameof(TicketFieldCustomerSettings)] = JsonSerializer.SerializeToNode(new TicketFieldCustomerSettings { CustomerEditable = true }),
                }),
                new ContentPartFieldDefinition(new ContentFieldDefinition("TextField"), "RootCause", []),
            ], []), []),
        ], []);

    private static ContentItem Taxonomy(params JsonObject[] terms)
    {
        var taxonomy = new ContentItem { ContentItemId = "categories", ContentType = "Taxonomy" };
        ((JsonObject)taxonomy.Content)["TaxonomyPart"] = new JsonObject { ["Terms"] = new JsonArray(terms) };

        return taxonomy;
    }

    private static JsonObject Term(string id, string name, TicketCategoryPart part, params JsonObject[] children)
    {
        var term = new JsonObject { ["ContentItemId"] = id, ["DisplayText"] = name };

        if (part is not null)
        {
            term[TicketsConstants.CategoryPartName] = JsonSerializer.SerializeToNode(part);
        }

        if (children.Length > 0)
        {
            term["Terms"] = new JsonArray(children);
        }

        return term;
    }

    private sealed class PassThroughStringLocalizer : IStringLocalizer<TicketCategoryConditionSource>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
