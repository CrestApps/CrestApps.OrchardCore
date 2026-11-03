using CrestApps.OrchardCore.Seo.Models;

namespace CrestApps.OrchardCore.Seo.SchemaOrg;

/// <summary>
/// Suggests where each schema.org property of a type should take its value from, among the values of a content type.
/// It follows the same rules as the editor's script (<c>schema-org-editor.js</c>), so the AI tools and the editor
/// suggest the same mappings.
/// </summary>
internal static class SchemaOrgSuggestions
{
    /// <summary>
    /// The properties the site fills in for every item without a mapping, and where it takes them from.
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, string> AutomaticProperties = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["name"] = "Item:DisplayText",
        ["url"] = "Item:Url",
        ["description"] = "Item:Summary",
        ["dateCreated"] = "Item:CreatedUtc",
        ["datePublished"] = "Item:PublishedUtc",
        ["dateModified"] = "Item:ModifiedUtc",
        ["inLanguage"] = "Item:Language",
    };

    private static readonly Dictionary<SchemaOrgValueKind, SchemaOrgValueKind[]> _compatible = new()
    {
        [SchemaOrgValueKind.Text] = [SchemaOrgValueKind.Text, SchemaOrgValueKind.LongText, SchemaOrgValueKind.Number, SchemaOrgValueKind.Url, SchemaOrgValueKind.Object],
        [SchemaOrgValueKind.LongText] = [SchemaOrgValueKind.LongText, SchemaOrgValueKind.Text],
        [SchemaOrgValueKind.TextList] = [SchemaOrgValueKind.TextList, SchemaOrgValueKind.Text, SchemaOrgValueKind.LongText],
        [SchemaOrgValueKind.Url] = [SchemaOrgValueKind.Url, SchemaOrgValueKind.Image],
        [SchemaOrgValueKind.Image] = [SchemaOrgValueKind.Image, SchemaOrgValueKind.Url],
        [SchemaOrgValueKind.Date] = [SchemaOrgValueKind.Date, SchemaOrgValueKind.DateTime],
        [SchemaOrgValueKind.DateTime] = [SchemaOrgValueKind.DateTime, SchemaOrgValueKind.Date],
        [SchemaOrgValueKind.Duration] = [SchemaOrgValueKind.Text],
        [SchemaOrgValueKind.Number] = [SchemaOrgValueKind.Number, SchemaOrgValueKind.Text],
        [SchemaOrgValueKind.Boolean] = [SchemaOrgValueKind.Boolean],
        [SchemaOrgValueKind.Enumeration] = [SchemaOrgValueKind.Text],
        [SchemaOrgValueKind.Object] = [SchemaOrgValueKind.Object, SchemaOrgValueKind.Text],
        [SchemaOrgValueKind.Json] = [SchemaOrgValueKind.Text, SchemaOrgValueKind.LongText],
    };

    private static readonly Dictionary<string, string[]> _synonyms = new(StringComparer.Ordinal)
    {
        ["image"] = ["photo", "picture", "thumbnail", "cover", "banner", "hero"],
        ["thumbnailurl"] = ["thumbnail", "image", "photo", "poster"],
        ["description"] = ["summary", "teaser", "excerpt", "intro", "abstract"],
        ["keywords"] = ["tags", "categories", "topics"],
        ["author"] = ["writer", "by", "owner"],
        ["startdate"] = ["start", "begins", "from", "date"],
        ["enddate"] = ["end", "ends", "until"],
        ["price"] = ["cost", "amount"],
        ["address"] = ["location", "venue", "street"],
        ["telephone"] = ["phone", "tel", "mobile"],
        ["headline"] = ["title"],
        ["sku"] = ["code", "productcode"],
        ["articlebody"] = ["body", "content", "text"],
        ["performer"] = ["artist", "speaker", "band"],
        ["organizer"] = ["host"],
        ["recipeingredient"] = ["ingredients", "ingredient"],
        ["recipeinstructions"] = ["instructions", "method", "steps", "directions"],
        ["duration"] = ["length", "runtime"],
        ["embedurl"] = ["video", "youtube"],
        ["logo"] = ["logo"],
        ["brand"] = ["manufacturer", "make"],
    };

    /// <summary>
    /// Checks whether a value of one kind can fill a property of another.
    /// </summary>
    /// <param name="propertyKind">The kind of the property.</param>
    /// <param name="sourceKind">The kind of the value.</param>
    /// <returns><see langword="true"/> when it can.</returns>
    public static bool IsCompatible(SchemaOrgValueKind propertyKind, SchemaOrgValueKind sourceKind)
        => (_compatible.TryGetValue(propertyKind, out var kinds) ? kinds : [SchemaOrgValueKind.Text]).Contains(sourceKind);

    /// <summary>
    /// Scores how well a value of the content type fits a property; 0 when it should not be suggested.
    /// </summary>
    /// <param name="property">The property.</param>
    /// <param name="source">The value of the content type.</param>
    /// <returns>The score.</returns>
    public static int Score(SchemaOrgProperty property, SchemaOrgContentSource source)
    {
        if (source.Key.StartsWith(SchemaOrgContentSources.FieldPrefix, StringComparison.Ordinal))
        {
            if (!IsCompatible(property.Kind, source.Kind))
            {
                return 0;
            }

            var score = NameScore(property, source);

            // A field of the very kind a property needs, such as the only image of a recipe, fits even with another name.
            if (score == 0 && source.Kind == property.Kind && property.Kind is SchemaOrgValueKind.Image or SchemaOrgValueKind.TextList)
            {
                score = 15;
            }

            if (score > 0 && source.Kind == property.Kind)
            {
                score += 5;
            }

            return score;
        }

        var suggested = Array.IndexOf(property.Suggestions ?? [], source.Key);

        return suggested >= 0 ? 30 - suggested : 0;
    }

    /// <summary>
    /// Suggests where a property should take its value from: the best value of the content type, a Liquid example, or
    /// the editors.
    /// </summary>
    /// <param name="property">The property.</param>
    /// <param name="sources">The values of the content type.</param>
    /// <param name="score">The score of the suggestion; 0 when no value of the content type fits.</param>
    /// <returns>The mapping.</returns>
    public static SchemaOrgPropertyMapping Suggest(SchemaOrgProperty property, IEnumerable<SchemaOrgContentSource> sources, out int score)
    {
        SchemaOrgContentSource best = null;
        score = 0;

        foreach (var source in sources ?? [])
        {
            var sourceScore = Score(property, source);

            if (sourceScore > score)
            {
                best = source;
                score = sourceScore;
            }
        }

        if (best is not null)
        {
            return new SchemaOrgPropertyMapping { Name = property.Name, Source = SchemaOrgValueSource.Content, Value = best.Key };
        }

        if (!string.IsNullOrEmpty(property.LiquidExample))
        {
            return new SchemaOrgPropertyMapping { Name = property.Name, Source = SchemaOrgValueSource.Liquid, Value = property.LiquidExample };
        }

        return new SchemaOrgPropertyMapping { Name = property.Name, Source = SchemaOrgValueSource.Editor };
    }

    /// <summary>
    /// Suggests the mappings of a type that are not mapped yet: its required and recommended properties, and the
    /// optional ones a field no other mapping uses is clearly named for. Properties the site fills in are left out unless a field gives a
    /// better value, and an object is written through its dotted properties.
    /// </summary>
    /// <param name="typeName">The schema.org type.</param>
    /// <param name="sources">The values of the content type.</param>
    /// <param name="existing">The mappings already made.</param>
    /// <returns>The suggested mappings.</returns>
    public static List<SchemaOrgPropertyMapping> SuggestMappings(string typeName, IReadOnlyList<SchemaOrgContentSource> sources, IEnumerable<SchemaOrgPropertyMapping> existing = null)
    {
        var properties = SchemaOrgVocabulary.GetProperties(typeName);
        var mapped = new HashSet<string>((existing ?? []).Select(mapping => mapping.Name), StringComparer.Ordinal);
        var usedSources = new HashSet<string>(
            (existing ?? []).Where(mapping => mapping.Source == SchemaOrgValueSource.Content).Select(mapping => mapping.Value ?? string.Empty),
            StringComparer.Ordinal);
        var result = new List<SchemaOrgPropertyMapping>();

        foreach (var property in properties)
        {
            if (mapped.Contains(property.Name) || HasChildren(properties, property.Name))
            {
                continue;
            }

            var suggestion = Suggest(property, sources, out var score);

            if (AutomaticProperties.TryGetValue(property.Name, out var automatic) &&
                (suggestion.Source != SchemaOrgValueSource.Content || suggestion.Value == automatic))
            {
                continue;
            }

            // An optional property is only worth a mapping for a field of its own, not one another mapping already uses.
            if (property.Level == SchemaOrgPropertyLevel.Optional &&
                !(suggestion.Source == SchemaOrgValueSource.Content && score >= 40 && !usedSources.Contains(suggestion.Value)))
            {
                continue;
            }

            if (suggestion.Source == SchemaOrgValueSource.Content)
            {
                usedSources.Add(suggestion.Value);
            }

            result.Add(suggestion);
        }

        return result;
    }

    /// <summary>
    /// Lists the required properties of a type that no mapping gives and the site does not fill in.
    /// </summary>
    /// <param name="typeName">The schema.org type.</param>
    /// <param name="mappings">The mappings.</param>
    /// <returns>The names of the missing properties.</returns>
    public static List<string> MissingRequired(string typeName, IEnumerable<SchemaOrgPropertyMapping> mappings)
    {
        var mapped = (mappings ?? []).Select(mapping => mapping.Name).ToList();

        return SchemaOrgVocabulary.GetProperties(typeName)
            .Where(property => property.Level == SchemaOrgPropertyLevel.Required &&
                !AutomaticProperties.ContainsKey(property.Name) &&
                !mapped.Any(name => name == property.Name || name.StartsWith(property.Name + ".", StringComparison.Ordinal)))
            .Select(property => property.Name)
            .ToList();
    }

    private static bool HasChildren(IReadOnlyList<SchemaOrgProperty> properties, string name)
        => properties.Any(property => property.Name.StartsWith(name + ".", StringComparison.Ordinal));

    private static int NameScore(SchemaOrgProperty property, SchemaOrgContentSource source)
    {
        var field = Normalize(source.Key.Substring(source.Key.LastIndexOf('.') + 1));
        var name = Normalize(property.Name.Split('.')[^1]);

        if (field.Length == 0 || name.Length == 0)
        {
            return 0;
        }

        if (field == name)
        {
            return 50;
        }

        if (!_synonyms.TryGetValue(Normalize(property.Name), out var synonyms) && !_synonyms.TryGetValue(name, out synonyms))
        {
            synonyms = [];
        }

        if (synonyms.Contains(field))
        {
            return 40;
        }

        if (synonyms.Any(synonym => field.Contains(synonym, StringComparison.Ordinal)))
        {
            return 20;
        }

        if (name.Length > 3 && (field.Contains(name, StringComparison.Ordinal) || name.Contains(field, StringComparison.Ordinal)))
        {
            return 20;
        }

        return 0;
    }

    private static string Normalize(string text)
        => new((text ?? string.Empty).ToLowerInvariant().Where(char.IsAsciiLetterOrDigit).ToArray());
}
