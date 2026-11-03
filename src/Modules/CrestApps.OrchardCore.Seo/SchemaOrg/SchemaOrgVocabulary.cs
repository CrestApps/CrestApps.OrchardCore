using static CrestApps.OrchardCore.Seo.SchemaOrg.SchemaOrgPropertyLevel;
using static CrestApps.OrchardCore.Seo.SchemaOrg.SchemaOrgValueKind;

namespace CrestApps.OrchardCore.Seo.SchemaOrg;

/// <summary>
/// The schema.org types and properties the Schema.org part's editor offers: the types search engines show rich results
/// for, and the common types sites describe. Each type lists the properties it adds to its parent's, marked required or
/// recommended as Google's guides do. Any other schema.org type or property can still be typed in.
/// </summary>
internal static class SchemaOrgVocabulary
{
    private const string Articles = "Articles";
    private const string Pages = "Pages";
    private const string Commerce = "Products and services";
    private const string Events = "Events";
    private const string People = "People and organizations";
    private const string Places = "Places";
    private const string Learning = "Recipes, how-tos and courses";
    private const string Media = "Media";
    private const string Other = "Other";

    private const string GoogleGuides = "https://developers.google.com/search/docs/appearance/structured-data/";

    private static readonly SchemaOrgOption[] _availability =
    [
        new("https://schema.org/InStock", "In stock"),
        new("https://schema.org/OutOfStock", "Out of stock"),
        new("https://schema.org/PreOrder", "Pre-order"),
        new("https://schema.org/BackOrder", "Back order"),
        new("https://schema.org/LimitedAvailability", "Limited availability"),
        new("https://schema.org/OnlineOnly", "Online only"),
        new("https://schema.org/InStoreOnly", "In store only"),
        new("https://schema.org/SoldOut", "Sold out"),
        new("https://schema.org/Discontinued", "Discontinued"),
    ];

    private static readonly SchemaOrgOption[] _condition =
    [
        new("https://schema.org/NewCondition", "New"),
        new("https://schema.org/UsedCondition", "Used"),
        new("https://schema.org/RefurbishedCondition", "Refurbished"),
        new("https://schema.org/DamagedCondition", "Damaged"),
    ];

    private static readonly SchemaOrgOption[] _eventStatus =
    [
        new("https://schema.org/EventScheduled", "Scheduled"),
        new("https://schema.org/EventRescheduled", "Rescheduled"),
        new("https://schema.org/EventPostponed", "Postponed"),
        new("https://schema.org/EventCancelled", "Cancelled"),
        new("https://schema.org/EventMovedOnline", "Moved online"),
    ];

    private static readonly SchemaOrgOption[] _attendanceMode =
    [
        new("https://schema.org/OfflineEventAttendanceMode", "In person"),
        new("https://schema.org/OnlineEventAttendanceMode", "Online"),
        new("https://schema.org/MixedEventAttendanceMode", "In person and online"),
    ];

    private static readonly SchemaOrgOption[] _employmentType =
    [
        new("FULL_TIME", "Full time"),
        new("PART_TIME", "Part time"),
        new("CONTRACTOR", "Contractor"),
        new("TEMPORARY", "Temporary"),
        new("INTERN", "Intern"),
        new("VOLUNTEER", "Volunteer"),
        new("PER_DIEM", "Per diem"),
        new("OTHER", "Other"),
    ];

    private static readonly SchemaOrgOption[] _bookFormat =
    [
        new("https://schema.org/Hardcover", "Hardcover"),
        new("https://schema.org/Paperback", "Paperback"),
        new("https://schema.org/EBook", "E-book"),
        new("https://schema.org/AudiobookFormat", "Audiobook"),
        new("https://schema.org/GraphicNovel", "Graphic novel"),
    ];

    /// <summary>
    /// Gets the types, base types first.
    /// </summary>
    public static IReadOnlyList<SchemaOrgType> Types { get; } =
    [
        new()
        {
            Name = "Thing",
            Description = "The most generic type of item.",
            Properties =
            [
                P("name", Text, Recommended, "The name of the item.", "Item:DisplayText"),
                P("description", LongText, Recommended, "A short description of the item.", "Item:Summary"),
                P("url", Url, Optional, "The URL of the item's page.", "Item:Url"),
                P("image", Image, Recommended, "An image of the item."),
                P("alternateName", Text, Optional, "Another name the item is known by."),
                P("identifier", Text, Optional, "An identifier of the item, such as an ISBN or a product code."),
                P("sameAs", Url, Optional, "The URL of a page that identifies the item, such as its Wikipedia page or a profile."),
            ],
        },
        new()
        {
            Name = "CreativeWork",
            Parent = "Thing",
            Description = "A creative work, such as an article, a book or a video.",
            Properties =
            [
                P("headline", Text, Recommended, "The title of the work. Keep it under 110 characters.", "Item:DisplayText"),
                O("author", "Person", Recommended, "Who wrote or made the work.", "Item:Author"),
                O("publisher", "Organization", Optional, "Who publishes the work.", "Site:Publisher"),
                P("datePublished", SchemaOrgValueKind.DateTime, Recommended, "When the work was first published.", "Item:PublishedUtc"),
                P("dateModified", SchemaOrgValueKind.DateTime, Recommended, "When the work was last changed.", "Item:ModifiedUtc"),
                P("dateCreated", SchemaOrgValueKind.DateTime, Optional, "When the work was created.", "Item:CreatedUtc"),
                P("keywords", TextList, Optional, "Keywords or tags that describe the work."),
                P("inLanguage", Text, Optional, "The language of the work, as an IETF tag such as en-US.", "Item:Language"),
                P("thumbnailUrl", Image, Optional, "A thumbnail image of the work."),
                P("text", LongText, Optional, "The text of the work.", "Item:Body"),
                O("about", "Thing", Optional, "What the work is about."),
                P("genre", Text, Optional, "The genre of the work."),
                P("license", Url, Optional, "The URL of the license the work is published under."),
                O("copyrightHolder", "Organization", Optional, "Who holds the copyright.", "Site:Publisher"),
                P("copyrightYear", Number, Optional, "The year the copyright was first claimed."),
                P("isAccessibleForFree", SchemaOrgValueKind.Boolean, Optional, "Whether the work can be read without paying."),
            ],
        },
        new()
        {
            Name = "Article",
            Parent = "CreativeWork",
            Group = Articles,
            Description = "A news, sports, how-to or blog article.",
            GuideUrl = GoogleGuides + "article",
            Properties =
            [
                P("articleBody", LongText, Optional, "The full text of the article.", "Item:Body"),
                P("articleSection", Text, Optional, "The section of the publication the article belongs to, such as Sports."),
                P("wordCount", Number, Optional, "The number of words in the article."),
            ],
        },
        T("BlogPosting", "Article", Articles, "A post on a blog.", GoogleGuides + "article"),
        T("NewsArticle", "Article", Articles, "A news article.", GoogleGuides + "article"),
        new()
        {
            Name = "TechArticle",
            Parent = "Article",
            Group = Articles,
            Description = "A technical article, such as a how-to or a reference.",
            GuideUrl = GoogleGuides + "article",
            Properties =
            [
                P("proficiencyLevel", Text, Optional, "The skill the reader needs: Beginner or Expert."),
                P("dependencies", Text, Optional, "What the reader needs to follow the article."),
            ],
        },
        new()
        {
            Name = "WebPage",
            Parent = "CreativeWork",
            Group = Pages,
            Description = "A web page. Use it for pages that are not articles, products or events.",
            Properties =
            [
                P("lastReviewed", Date, Optional, "When the page's content was last reviewed."),
                P("primaryImageOfPage", Image, Optional, "The main image of the page."),
                P("significantLink", Url, Optional, "One of the page's most important links."),
            ],
        },
        T("AboutPage", "WebPage", Pages, "A page about the site or the organization."),
        T("ContactPage", "WebPage", Pages, "A page with contact details."),
        T("CollectionPage", "WebPage", Pages, "A page that lists other pages, such as a category page."),
        T("ItemPage", "WebPage", Pages, "A page about one item."),
        T("ProfilePage", "WebPage", Pages, "A page about a person or an organization.", GoogleGuides + "profile-page"),
        new()
        {
            Name = "FAQPage",
            Parent = "WebPage",
            Group = Pages,
            Description = "A page of frequently asked questions and their answers.",
            GuideUrl = GoogleGuides + "faqpage",
            Properties =
            [
                new()
                {
                    Name = "mainEntity",
                    Kind = SchemaOrgValueKind.Json,
                    Level = Required,
                    Description = "The questions, each a Question with an acceptedAnswer. Build them with Liquid from the item's questions.",
                    LiquidExample = "[{% for q in ContentItem.Content.BagPart.ContentItems %}{\"@type\":\"Question\",\"name\":{{ q.DisplayText | json }},\"acceptedAnswer\":{\"@type\":\"Answer\",\"text\":{{ q.Content.HtmlBodyPart.Html | strip_html | json }}}}{% unless forloop.last %},{% endunless %}{% endfor %}]",
                },
            ],
        },
        new()
        {
            Name = "Product",
            Parent = "Thing",
            Group = Commerce,
            Description = "A product for sale. Search engines need a price, a review or a rating to show it as a product.",
            GuideUrl = GoogleGuides + "product",
            Properties =
            [
                P("name", Text, Required, "The name of the product.", "Item:DisplayText"),
                P("image", Image, Recommended, "Images of the product."),
                O("brand", "Brand", Recommended, "The brand of the product."),
                P("sku", Text, Recommended, "The product's SKU."),
                P("gtin", Text, Optional, "The product's GTIN, such as its UPC or EAN barcode."),
                P("mpn", Text, Optional, "The manufacturer's part number."),
                P("model", Text, Optional, "The model of the product."),
                P("color", Text, Optional, "The color of the product."),
                P("material", Text, Optional, "What the product is made of."),
                P("category", Text, Optional, "The product's category, such as Clothing > Shoes."),
                O("offers", "Offer", Recommended, "The offer to sell the product: set its price, currency and availability below."),
                P("offers.price", Number, Recommended, "The price, as a number."),
                P("offers.priceCurrency", Text, Recommended, "The currency of the price, as a three-letter ISO 4217 code such as USD."),
                E("offers.availability", Recommended, "Whether the product is in stock.", _availability),
                E("offers.itemCondition", Optional, "The condition of the product.", _condition),
                P("offers.url", Url, Optional, "The URL where the product can be bought.", "Item:Url"),
                P("offers.priceValidUntil", Date, Optional, "The last day the price is valid."),
                O("aggregateRating", "AggregateRating", Optional, "The product's average rating: set its value and count below."),
                P("aggregateRating.ratingValue", Number, Optional, "The average rating."),
                P("aggregateRating.reviewCount", Number, Optional, "The number of reviews."),
            ],
        },
        new()
        {
            Name = "Service",
            Parent = "Thing",
            Group = Commerce,
            Description = "A service offered by an organization or a person.",
            Properties =
            [
                P("serviceType", Text, Recommended, "The kind of service, such as Plumbing."),
                O("provider", "Organization", Recommended, "Who provides the service.", "Site:Publisher"),
                P("areaServed", Text, Optional, "Where the service is offered."),
                O("offers", "Offer", Optional, "The offer: set its price and currency below."),
                P("offers.price", Number, Optional, "The price, as a number."),
                P("offers.priceCurrency", Text, Optional, "The currency of the price, such as USD."),
            ],
        },
        new()
        {
            Name = "SoftwareApplication",
            Parent = "CreativeWork",
            Group = Commerce,
            Description = "An application, such as a mobile app or a game.",
            GuideUrl = GoogleGuides + "software-app",
            Properties =
            [
                P("name", Text, Required, "The name of the application.", "Item:DisplayText"),
                P("applicationCategory", Text, Recommended, "The category, such as GameApplication or BusinessApplication."),
                P("operatingSystem", Text, Recommended, "The operating systems it runs on, such as Windows 11, Android."),
                P("softwareVersion", Text, Optional, "The version."),
                P("downloadUrl", Url, Optional, "Where it can be downloaded."),
                O("offers", "Offer", Required, "The offer: set its price below, 0 when it is free."),
                P("offers.price", Number, Required, "The price, as a number. Use 0 when it is free."),
                P("offers.priceCurrency", Text, Recommended, "The currency of the price, such as USD."),
            ],
        },
        new()
        {
            Name = "Event",
            Parent = "Thing",
            Group = Events,
            Description = "An event, such as a concert, a lecture or a festival.",
            GuideUrl = GoogleGuides + "event",
            Properties =
            [
                P("name", Text, Required, "The name of the event.", "Item:DisplayText"),
                P("startDate", SchemaOrgValueKind.DateTime, Required, "When the event starts."),
                P("endDate", SchemaOrgValueKind.DateTime, Recommended, "When the event ends."),
                O("location", "Place", Required, "Where the event takes place: set its name and address below."),
                P("location.name", Text, Recommended, "The name of the venue."),
                P("location.address", Text, Required, "The address of the venue."),
                E("eventStatus", Recommended, "Whether the event is going ahead.", _eventStatus),
                E("eventAttendanceMode", Optional, "Whether people attend in person, online or both.", _attendanceMode),
                O("organizer", "Organization", Recommended, "Who organizes the event.", "Site:Publisher"),
                O("performer", "Person", Recommended, "Who performs at the event."),
                O("offers", "Offer", Recommended, "The tickets: set their price and availability below."),
                P("offers.price", Number, Recommended, "The ticket price, as a number."),
                P("offers.priceCurrency", Text, Recommended, "The currency of the price, such as USD."),
                E("offers.availability", Recommended, "Whether tickets are available.", _availability),
                P("offers.url", Url, Recommended, "Where tickets can be bought."),
                P("offers.validFrom", SchemaOrgValueKind.DateTime, Recommended, "When tickets go on sale."),
            ],
        },
        new()
        {
            Name = "Organization",
            Parent = "Thing",
            Group = People,
            Description = "An organization, such as a company, a school or a club.",
            GuideUrl = GoogleGuides + "organization",
            Properties =
            [
                P("logo", Image, Recommended, "The organization's logo."),
                P("legalName", Text, Optional, "The registered name of the organization."),
                P("email", Text, Optional, "The organization's email address."),
                P("telephone", Text, Optional, "The organization's phone number, with its country code."),
                P("address", Text, Optional, "The organization's postal address."),
                P("foundingDate", Date, Optional, "When the organization was founded."),
                O("founder", "Person", Optional, "Who founded the organization."),
            ],
        },
        new()
        {
            Name = "LocalBusiness",
            Parent = "Organization",
            Group = People,
            Description = "A business with a place customers visit, such as a shop or a dentist.",
            GuideUrl = GoogleGuides + "local-business",
            Properties =
            [
                P("name", Text, Required, "The name of the business.", "Item:DisplayText"),
                P("address", Text, Required, "The address of the business."),
                P("telephone", Text, Recommended, "The phone number, with its country code."),
                P("priceRange", Text, Recommended, "The price range, such as $$ or 10-50 USD."),
                P("openingHours", Text, Recommended, "The opening hours, such as Mo-Fr 09:00-17:00."),
                O("geo", "GeoCoordinates", Recommended, "Where the business is: set its latitude and longitude below."),
                P("geo.latitude", Number, Recommended, "The latitude."),
                P("geo.longitude", Number, Recommended, "The longitude."),
            ],
        },
        new()
        {
            Name = "Restaurant",
            Parent = "LocalBusiness",
            Group = People,
            Description = "A restaurant.",
            GuideUrl = GoogleGuides + "local-business",
            Properties =
            [
                P("servesCuisine", Text, Recommended, "The cuisine, such as Italian."),
                P("menu", Url, Recommended, "The URL of the menu."),
                P("acceptsReservations", SchemaOrgValueKind.Boolean, Optional, "Whether the restaurant takes reservations."),
            ],
        },
        T("Store", "LocalBusiness", People, "A shop.", GoogleGuides + "local-business"),
        new()
        {
            Name = "Person",
            Parent = "Thing",
            Group = People,
            Description = "A person, alive, dead or fictional.",
            Properties =
            [
                P("givenName", Text, Optional, "The person's first name."),
                P("familyName", Text, Optional, "The person's last name."),
                P("jobTitle", Text, Optional, "The person's job title."),
                O("worksFor", "Organization", Optional, "Where the person works."),
                P("email", Text, Optional, "The person's email address."),
                P("telephone", Text, Optional, "The person's phone number."),
                P("birthDate", Date, Optional, "When the person was born."),
                O("alumniOf", "Organization", Optional, "A school the person went to."),
            ],
        },
        new()
        {
            Name = "Place",
            Parent = "Thing",
            Group = Places,
            Description = "A place, such as a venue, a landmark or a city.",
            Properties =
            [
                P("address", Text, Recommended, "The address of the place."),
                P("telephone", Text, Optional, "The phone number of the place."),
                O("geo", "GeoCoordinates", Optional, "Where the place is: set its latitude and longitude below."),
                P("geo.latitude", Number, Optional, "The latitude."),
                P("geo.longitude", Number, Optional, "The longitude."),
                P("hasMap", Url, Optional, "The URL of a map of the place."),
            ],
        },
        new()
        {
            Name = "Recipe",
            Parent = "CreativeWork",
            Group = Learning,
            Description = "A recipe for a dish or a drink.",
            GuideUrl = GoogleGuides + "recipe",
            Properties =
            [
                P("name", Text, Required, "The name of the dish.", "Item:DisplayText"),
                P("image", Image, Required, "Images of the finished dish."),
                P("recipeIngredient", TextList, Recommended, "The ingredients, one per line."),
                P("recipeInstructions", LongText, Recommended, "The steps to make the dish."),
                P("recipeYield", Text, Recommended, "How much it makes, such as 4 servings."),
                P("prepTime", Duration, Recommended, "How long the preparation takes, such as PT20M."),
                P("cookTime", Duration, Recommended, "How long the cooking takes, such as PT1H."),
                P("totalTime", Duration, Recommended, "How long it takes in total."),
                P("recipeCategory", Text, Recommended, "The kind of dish, such as Dessert."),
                P("recipeCuisine", Text, Recommended, "The cuisine, such as French."),
                O("nutrition", "NutritionInformation", Optional, "Nutrition facts: set the calories below."),
                P("nutrition.calories", Text, Optional, "The calories per serving, such as 270 calories."),
            ],
        },
        new()
        {
            Name = "HowTo",
            Parent = "CreativeWork",
            Group = Learning,
            Description = "Instructions to do something.",
            Properties =
            [
                P("step", LongText, Recommended, "The steps.", "Item:Body"),
                P("totalTime", Duration, Optional, "How long it takes, such as PT30M."),
                P("supply", TextList, Optional, "What is used up, one per line."),
                P("tool", TextList, Optional, "The tools needed, one per line."),
            ],
        },
        new()
        {
            Name = "Course",
            Parent = "CreativeWork",
            Group = Learning,
            Description = "A course of study.",
            GuideUrl = GoogleGuides + "course",
            Properties =
            [
                P("name", Text, Required, "The title of the course.", "Item:DisplayText"),
                P("description", LongText, Required, "What the course covers.", "Item:Summary"),
                O("provider", "Organization", Recommended, "Who offers the course.", "Site:Publisher"),
                P("courseCode", Text, Optional, "The course's code."),
            ],
        },
        new()
        {
            Name = "VideoObject",
            Parent = "CreativeWork",
            Group = Media,
            Description = "A video.",
            GuideUrl = GoogleGuides + "video",
            Properties =
            [
                P("name", Text, Required, "The title of the video.", "Item:DisplayText"),
                P("thumbnailUrl", Image, Required, "An image from the video."),
                P("uploadDate", SchemaOrgValueKind.DateTime, Required, "When the video was published.", "Item:PublishedUtc"),
                P("description", LongText, Recommended, "What the video shows.", "Item:Summary"),
                P("contentUrl", Url, Recommended, "The URL of the video file."),
                P("embedUrl", Url, Recommended, "The URL of the video's player, such as a YouTube embed."),
                P("duration", Duration, Recommended, "How long the video is, such as PT1M33S."),
            ],
        },
        new()
        {
            Name = "Book",
            Parent = "CreativeWork",
            Group = Media,
            Description = "A book.",
            GuideUrl = GoogleGuides + "book",
            Properties =
            [
                P("isbn", Text, Recommended, "The ISBN of the book."),
                E("bookFormat", Optional, "The format of the book.", _bookFormat),
                P("numberOfPages", Number, Optional, "The number of pages."),
                O("illustrator", "Person", Optional, "Who illustrated the book."),
            ],
        },
        new()
        {
            Name = "Movie",
            Parent = "CreativeWork",
            Group = Media,
            Description = "A movie.",
            GuideUrl = GoogleGuides + "movie",
            Properties =
            [
                O("director", "Person", Recommended, "Who directed the movie."),
                O("actor", "Person", Optional, "An actor in the movie."),
                P("duration", Duration, Optional, "How long the movie is, such as PT2H10M."),
            ],
        },
        new()
        {
            Name = "JobPosting",
            Parent = "Thing",
            Group = Other,
            Description = "A job opening.",
            GuideUrl = GoogleGuides + "job-posting",
            Properties =
            [
                P("title", Text, Required, "The job title.", "Item:DisplayText"),
                P("description", LongText, Required, "The full description of the job, as HTML or text.", "Item:Body"),
                P("datePosted", Date, Required, "When the job was posted.", "Item:PublishedUtc"),
                P("validThrough", SchemaOrgValueKind.DateTime, Recommended, "When the posting closes."),
                O("hiringOrganization", "Organization", Required, "Who is hiring.", "Site:Publisher"),
                O("jobLocation", "Place", Required, "Where the job is: set its address below."),
                P("jobLocation.address", Text, Required, "The address of the workplace."),
                E("employmentType", Recommended, "The kind of employment.", _employmentType),
                O("baseSalary", "MonetaryAmount", Recommended, "The pay: set its currency, amount and unit below."),
                P("baseSalary.currency", Text, Recommended, "The currency of the pay, such as USD."),
                O("baseSalary.value", "QuantitativeValue", Recommended, "The amount of the pay."),
                P("baseSalary.value.value", Number, Recommended, "The amount, as a number."),
                P("baseSalary.value.unitText", Text, Recommended, "What the amount is per: HOUR, DAY, WEEK, MONTH or YEAR."),
            ],
        },
        new()
        {
            Name = "Review",
            Parent = "CreativeWork",
            Group = Other,
            Description = "A review of an item, such as a product, a book or a restaurant.",
            GuideUrl = GoogleGuides + "review-snippet",
            Properties =
            [
                O("itemReviewed", "Thing", Required, "What is reviewed."),
                O("author", "Person", Required, "Who wrote the review.", "Item:Author"),
                O("reviewRating", "Rating", Required, "The rating: set its value below."),
                P("reviewRating.ratingValue", Number, Required, "The rating given."),
                P("reviewRating.bestRating", Number, Optional, "The highest rating possible, 5 when it is empty."),
                P("reviewBody", LongText, Optional, "The text of the review.", "Item:Body"),
            ],
        },
        new()
        {
            Name = "Dataset",
            Parent = "CreativeWork",
            Group = Other,
            Description = "A set of data, such as a table or a collection of files.",
            GuideUrl = GoogleGuides + "dataset",
            Properties =
            [
                P("name", Text, Required, "The name of the data set.", "Item:DisplayText"),
                P("description", LongText, Required, "What the data set holds.", "Item:Summary"),
                O("creator", "Organization", Recommended, "Who made the data set.", "Site:Publisher"),
                P("temporalCoverage", Text, Optional, "The period the data covers, such as 2020/2024."),
                P("spatialCoverage", Text, Optional, "The place the data covers."),
            ],
        },
    ];

    private static readonly Dictionary<string, SchemaOrgType> _typesByName = Types.ToDictionary(type => type.Name, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Finds a type by name.
    /// </summary>
    /// <param name="name">The type name.</param>
    /// <returns>The type, or <see langword="null"/> when the vocabulary does not list it.</returns>
    public static SchemaOrgType GetType(string name)
        => !string.IsNullOrWhiteSpace(name) && _typesByName.TryGetValue(name.Trim(), out var type) ? type : null;

    /// <summary>
    /// Gets every property of a type: its own and its parents', each once, the type's own declaration winning, and the
    /// required and recommended ones first. A type the vocabulary does not list gets the properties of <c>Thing</c>.
    /// </summary>
    /// <param name="typeName">The type name.</param>
    /// <returns>The properties.</returns>
    public static IReadOnlyList<SchemaOrgProperty> GetProperties(string typeName)
    {
        var type = GetType(typeName) ?? GetType("Thing");
        var properties = new Dictionary<string, SchemaOrgProperty>(StringComparer.Ordinal);
        var order = new List<string>();

        for (var current = type; current is not null; current = GetType(current.Parent))
        {
            foreach (var property in current.Properties)
            {
                if (properties.TryAdd(property.Name, property))
                {
                    order.Add(property.Name);
                }
            }
        }

        return order
            .Select(name => properties[name])
            .OrderByDescending(property => property.Level)
            .ToList();
    }

    /// <summary>
    /// Finds a property of a type.
    /// </summary>
    /// <param name="typeName">The type name.</param>
    /// <param name="propertyName">The property name, which may be dotted.</param>
    /// <returns>The property, or <see langword="null"/> when the vocabulary does not list it for the type.</returns>
    public static SchemaOrgProperty GetProperty(string typeName, string propertyName)
        => GetProperties(typeName).FirstOrDefault(property => string.Equals(property.Name, propertyName, StringComparison.Ordinal));

    private static SchemaOrgType T(string name, string parent, string group, string description, string guideUrl = null)
        => new() { Name = name, Parent = parent, Group = group, Description = description, GuideUrl = guideUrl };

    private static SchemaOrgProperty P(string name, SchemaOrgValueKind kind, SchemaOrgPropertyLevel level, string description, params string[] suggestions)
        => new() { Name = name, Kind = kind, Level = level, Description = description, Suggestions = suggestions };

    private static SchemaOrgProperty O(string name, string objectType, SchemaOrgPropertyLevel level, string description, params string[] suggestions)
        => new() { Name = name, Kind = SchemaOrgValueKind.Object, ObjectType = objectType, Level = level, Description = description, Suggestions = suggestions };

    private static SchemaOrgProperty E(string name, SchemaOrgPropertyLevel level, string description, SchemaOrgOption[] options)
        => new() { Name = name, Kind = Enumeration, Level = level, Description = description, Options = options };
}
