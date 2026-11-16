using System.Globalization;
using System.Net;
using System.Text;
using System.Xml.Linq;
using CrestApps.OrchardCore.Learning.Core.Archives;

namespace CrestApps.OrchardCore.Learning.Assessments.Core.Qti;

/// <summary>
/// Reads one assessment item, QTI 2.x or QTI 3.0, into a question. QTI 3.0 names its elements <c>qti-choice-interaction</c>
/// and its attributes <c>response-identifier</c> where QTI 2.x writes <c>choiceInteraction</c> and
/// <c>responseIdentifier</c>, so a QTI 3.0 item is first rewritten in the QTI 2.x names and both are read the same way.
/// </summary>
internal static class QtiItemParser
{
    private const string Namespace3 = "http://www.imsglobal.org/xsd/imsqtiasi_v3p0";
    private const string Namespace2 = "http://www.imsglobal.org/xsd/imsqti_v2p";

    private static readonly HashSet<string> _supportedInteractions = new(StringComparer.Ordinal)
    {
        "choiceInteraction",
        "textEntryInteraction",
        "extendedTextInteraction",
        "matchInteraction",
        "associateInteraction",
        "orderInteraction",
    };

    // The elements of an inline response processing this import understands: the rules the standard templates use,
    // and the tolerance of a numeric comparison.
    private static readonly HashSet<string> _knownRules = new(StringComparer.Ordinal)
    {
        "responseProcessing", "responseCondition", "responseIf", "responseElseIf", "responseElse", "setOutcomeValue",
        "match", "mapResponse", "equal", "variable", "correct", "baseValue", "sum", "isNull", "not", "and", "or",
        "gte", "lte", "gt", "lt", "multiple", "ordered", "exitResponse", "lookupOutcomeValue",
    };

    private static readonly HashSet<string> _htmlElements = new(StringComparer.Ordinal)
    {
        "p", "div", "span", "br", "b", "strong", "i", "em", "u", "sub", "sup", "small", "big", "code", "pre",
        "blockquote", "ul", "ol", "li", "dl", "dt", "dd", "table", "thead", "tbody", "tfoot", "tr", "th", "td",
        "caption", "col", "colgroup", "h1", "h2", "h3", "h4", "h5", "h6", "hr", "img", "a", "abbr", "acronym",
        "cite", "q", "kbd", "samp", "var", "dfn", "address", "figure", "figcaption", "tt", "bdo", "label",
        "section", "article", "aside", "header", "footer", "nav", "mark", "s", "del", "ins",
    };

    private static readonly HashSet<string> _voidElements = new(StringComparer.Ordinal) { "br", "hr", "img", "col" };

    private static readonly HashSet<string> _inlineTextElements = new(StringComparer.Ordinal)
    {
        "b", "strong", "i", "em", "u", "sub", "sup", "small", "big", "span", "code", "q", "abbr", "acronym", "cite",
        "kbd", "samp", "var", "dfn", "tt", "bdo", "mark", "s", "del", "ins", "br", "label",
    };

    private static readonly HashSet<string> _blockElements = new(StringComparer.Ordinal)
    {
        "p", "div", "ul", "ol", "dl", "table", "blockquote", "pre", "h1", "h2", "h3", "h4", "h5", "h6", "hr",
        "figure", "address", "section", "article", "aside", "header", "footer", "nav",
    };

    // QTI elements that never reach the prompt: conditional feedback shown by an engine, style sheets.
    private static readonly HashSet<string> _droppedElements = new(StringComparer.Ordinal)
    {
        "feedbackBlock", "feedbackInline", "stylesheet", "catalogInfo", "companionMaterialsInfo",
    };

    private static readonly HashSet<string> _imageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".webp",
    };

    /// <summary>
    /// Reads an item file.
    /// </summary>
    /// <param name="content">The bytes of the file.</param>
    /// <param name="path">The path of the file in the package.</param>
    /// <param name="archive">The package, to read the media from, or <see langword="null"/> for a file on its own.</param>
    /// <param name="limits">The limits.</param>
    public static QtiItem Parse(byte[] content, string path, PackageArchiveReader archive, QtiReadLimits limits)
    {
        var document = QtiReader.LoadXml(content, limits.MaxXmlCharacters);

        if (document?.Root is null)
        {
            return new QtiItem { Path = path, Refusal = QtiRefusals.InvalidXml };
        }

        var root = document.Root;
        string version;

        if (root.Name.LocalName == "qti-assessment-item" && root.Name.NamespaceName.StartsWith(Namespace3, StringComparison.Ordinal))
        {
            version = "3.0";
        }
        else if (root.Name.LocalName == "assessmentItem" && root.Name.NamespaceName.StartsWith(Namespace2, StringComparison.Ordinal))
        {
            version = root.Name.NamespaceName.Contains("v2p2", StringComparison.Ordinal) ? "2.2" : root.Name.NamespaceName.Contains("v2p0", StringComparison.Ordinal) ? "2.0" : "2.1";
        }
        else
        {
            return new QtiItem { Path = path, Refusal = QtiRefusals.NotAnItem };
        }

        var item = Normalize(root, root.Name.Namespace, version == "3.0");
        var context = new ItemContext(path, archive, limits);
        var result = new QtiItem
        {
            Identifier = Attribute(item, "identifier"),
            Title = Attribute(item, "title"),
            Path = path,
            Version = version,
        };

        result.Title = string.IsNullOrWhiteSpace(result.Title) ? result.Identifier : result.Title.Trim();

        try
        {
            result.Question = Read(item, context);
            result.Media = context.Files;
        }
        catch (RefusedException refused)
        {
            result.Refusal = refused.Refusal;
            result.RefusalDetail = refused.Detail;
            result.Question = null;
        }

        return result;
    }

    private static QtiQuestion Read(XElement item, ItemContext context)
    {
        if (string.Equals(Attribute(item, "adaptive"), "true", StringComparison.OrdinalIgnoreCase))
        {
            throw new RefusedException(QtiRefusals.Adaptive);
        }

        if (item.Descendants().Any(element => element.Name.LocalName is "templateDeclaration" or "templateProcessing" or "templateBlock" or "templateInline" or "templateVariable"))
        {
            throw new RefusedException(QtiRefusals.Template);
        }

        var body = item.Element("itemBody") ?? throw new RefusedException(QtiRefusals.NoInteraction);
        var interactions = body.Descendants().Where(element => element.Name.Namespace == XNamespace.None && element.Name.LocalName.EndsWith("Interaction", StringComparison.Ordinal)).ToList();

        if (interactions.FirstOrDefault(interaction => !_supportedInteractions.Contains(interaction.Name.LocalName)) is { } unsupported)
        {
            throw new RefusedException(QtiRefusals.UnsupportedInteraction, unsupported.Name.LocalName);
        }

        if (interactions.Count == 0)
        {
            throw new RefusedException(QtiRefusals.NoInteraction);
        }

        var declarations = item.Elements("responseDeclaration").Select(ResponseDeclaration.Read).Where(declaration => declaration.Identifier is not null)
            .GroupBy(declaration => declaration.Identifier, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var scoring = Scoring.Read(item.Element("responseProcessing"));
        var question = new QtiQuestion();

        // The rubric for scorers is grader guidance; a rubric for candidates is part of the prompt.
        foreach (var rubric in body.Descendants("rubricBlock").ToList())
        {
            var view = Attribute(rubric, "view") ?? string.Empty;

            if (view.Length == 0 || view.Contains("candidate", StringComparison.OrdinalIgnoreCase))
            {
                rubric.ReplaceWith(new XElement("div", rubric.Nodes()));

                continue;
            }

            if (view.Contains("scorer", StringComparison.OrdinalIgnoreCase))
            {
                var guidance = LooseText(rubric);
                question.GraderGuidance = string.IsNullOrEmpty(question.GraderGuidance) ? guidance : question.GraderGuidance + " " + guidance;
            }

            rubric.Remove();
        }

        var kinds = interactions.Select(interaction => interaction.Name.LocalName).Distinct(StringComparer.Ordinal).ToList();

        if (kinds.Count > 1 || (kinds[0] != "textEntryInteraction" && interactions.Count > 1))
        {
            throw new RefusedException(QtiRefusals.SeveralInteractions);
        }

        var modalFeedback = item.Elements("modalFeedback").ToList();

        switch (kinds[0])
        {
            case "choiceInteraction":
                ReadChoice(interactions[0], declarations, scoring, question, modalFeedback, context);
                break;

            case "textEntryInteraction":
                ReadTextEntries(body, interactions, declarations, scoring, question, context);
                break;

            case "extendedTextInteraction":
                question.QuestionType = AssessmentsConstants.QuestionTypes.Essay;
                question.Points = 1;
                ReplaceWithPrompt(interactions[0]);
                break;

            case "matchInteraction":
                ReadMatch(interactions[0], declarations, scoring, question);
                break;

            case "associateInteraction":
                ReadAssociate(interactions[0], declarations, scoring, question);
                break;

            case "orderInteraction":
                ReadOrder(interactions[0], declarations, scoring, question);
                break;
        }

        if (question.QuestionType != AssessmentsConstants.QuestionTypes.FillInBlanks)
        {
            question.Prompt = Html(body.Nodes(), context);
        }

        // Feedback not tied to a choice is general feedback when the item has one such block. Several are conditional
        // (right and wrong), and showing them all whatever the answer would mislead, so they are left out.
        var general = modalFeedback.Where(feedback => feedback.Parent is not null).ToList();

        if (general.Count == 1)
        {
            question.GeneralFeedback = NullIfEmpty(Html(general[0].Nodes(), context));
        }

        question.Points = Points(item, question.Points);
        question.Prompt = NullIfEmpty(question.Prompt);

        return question;
    }

    private static void ReadChoice(XElement interaction, Dictionary<string, ResponseDeclaration> declarations, Scoring scoring, QtiQuestion question, List<XElement> modalFeedback, ItemContext context)
    {
        var declaration = Declaration(interaction, declarations);
        scoring.Require(declaration);

        var choices = interaction.Elements("simpleChoice").ToList();
        var correct = declaration.Correct(scoring);

        foreach (var choice in choices)
        {
            var identifier = Attribute(choice, "identifier");
            var feedback = choice.Elements("feedbackInline").Select(LooseText).FirstOrDefault(text => text.Length > 0);
            choice.Elements("feedbackInline").Remove();

            var tied = modalFeedback.FirstOrDefault(element => element.Parent is not null && string.Equals(Attribute(element, "identifier"), identifier, StringComparison.Ordinal));

            if (tied is not null)
            {
                feedback ??= LooseText(tied);
                tied.Remove();
            }

            question.Options.Add(new QtiOption
            {
                Identifier = identifier,
                Text = PlainText(choice),
                Correct = correct.Contains(identifier),
                Feedback = NullIfEmpty(feedback),
            });
        }

        if (!question.Options.Any(option => option.Correct))
        {
            throw new RefusedException(QtiRefusals.NoCorrectResponse);
        }

        var maxChoices = int.TryParse(Attribute(interaction, "maxChoices"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var max) ? max : 1;
        var multiple = string.Equals(declaration.Cardinality, "multiple", StringComparison.Ordinal) || maxChoices != 1 || question.Options.Count(option => option.Correct) > 1;

        question.Shuffle = string.Equals(Attribute(interaction, "shuffle"), "true", StringComparison.OrdinalIgnoreCase);

        if (!multiple && question.Options.Count == 2 && question.Options.Select(option => option.Text.ToLowerInvariant()).Order(StringComparer.Ordinal).SequenceEqual(["false", "true"]))
        {
            question.QuestionType = AssessmentsConstants.QuestionTypes.TrueFalse;
            question.TrueFalseAnswer = question.Options.Single(option => option.Correct).Text.Equals("true", StringComparison.OrdinalIgnoreCase);
            question.Options.Clear();
        }
        else
        {
            question.QuestionType = multiple ? AssessmentsConstants.QuestionTypes.MultipleChoice : AssessmentsConstants.QuestionTypes.SingleChoice;
            question.PartialCredit = multiple && scoring.Kind == ScoringKind.MapResponse;
        }

        question.Points = scoring.Kind == ScoringKind.MapResponse ? declaration.MappedTotal(correct) : 1;
        ReplaceWithPrompt(interaction);
    }

    private static void ReadTextEntries(XElement body, List<XElement> interactions, Dictionary<string, ResponseDeclaration> declarations, Scoring scoring, QtiQuestion question, ItemContext context)
    {
        var first = interactions[0];
        var firstDeclaration = Declaration(first, declarations);
        scoring.Require(firstDeclaration);

        var block = BodyChild(body, first);
        var standalone = interactions.Count == 1 && (block == first || block.Nodes().All(node => node == first || IsAncestorOf(node, first) && OnlyHolds(node, first) || node is XText text && string.IsNullOrWhiteSpace(text.Value)));

        if (interactions.Count == 1 && firstDeclaration.BaseType is "float" or "integer")
        {
            question.QuestionType = AssessmentsConstants.QuestionTypes.Numeric;
            var answer = firstDeclaration.CorrectValues.FirstOrDefault();

            if (answer is null || !decimal.TryParse(answer, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
            {
                throw new RefusedException(QtiRefusals.NoCorrectResponse);
            }

            question.NumericAnswer = number;
            question.Tolerance = scoring.Tolerance(number);
            question.Points = scoring.Kind == ScoringKind.MapResponse ? firstDeclaration.MappedMaximum() : 1;

            if (standalone)
            {
                block.Remove();
            }
            else
            {
                first.ReplaceWith(new XText("_____"));
            }

            return;
        }

        if (standalone)
        {
            question.QuestionType = AssessmentsConstants.QuestionTypes.ShortText;
            question.AcceptedAnswers = firstDeclaration.Accepted(scoring);

            if (question.AcceptedAnswers.Count == 0)
            {
                throw new RefusedException(QtiRefusals.NoCorrectResponse);
            }

            question.CaseSensitive = firstDeclaration.CaseSensitive(scoring);
            question.Points = scoring.Kind == ScoringKind.MapResponse ? firstDeclaration.MappedMaximum() : 1;
            block.Remove();

            return;
        }

        // Inline text entries are the blanks of a text: the blocks that hold them become the text, the others the prompt.
        question.QuestionType = AssessmentsConstants.QuestionTypes.FillInBlanks;
        question.CaseSensitive = true;
        decimal points = 0;

        var blocks = interactions.Select(interaction => BodyChild(body, interaction)).Distinct().ToList();
        var texts = new List<string>();

        foreach (var interaction in interactions)
        {
            var declaration = Declaration(interaction, declarations);
            var answers = declaration.Accepted(scoring);

            if (answers.Count == 0)
            {
                throw new RefusedException(QtiRefusals.NoCorrectResponse);
            }

            if (answers.FirstOrDefault(answer => answer.IndexOfAny(['|', '[', ']']) >= 0) is { } unsafeAnswer)
            {
                throw new RefusedException(QtiRefusals.UnsupportedAnswer, unsafeAnswer);
            }

            question.CaseSensitive &= declaration.CaseSensitive(scoring);
            points += scoring.Kind == ScoringKind.MapResponse && declaration.Mapping.Count > 0 ? declaration.MappedMaximum() : 1;
            interaction.ReplaceWith(new XText("[[" + string.Join('|', answers) + "]]"));
        }

        foreach (var textBlock in blocks)
        {
            texts.Add(PlainText(textBlock));
            textBlock.Remove();
        }

        question.BlankText = string.Join(' ', texts.Where(text => text.Length > 0));
        question.Points = points;
        question.Prompt = Html(body.Nodes(), context);
    }

    private static void ReadMatch(XElement interaction, Dictionary<string, ResponseDeclaration> declarations, Scoring scoring, QtiQuestion question)
    {
        var declaration = Declaration(interaction, declarations);
        scoring.Require(declaration);

        var sets = interaction.Elements("simpleMatchSet").ToList();

        if (sets.Count != 2)
        {
            throw new RefusedException(QtiRefusals.UnsupportedStructure, "match sets");
        }

        var sources = sets[0].Elements("simpleAssociableChoice").Select(choice => (Id: Attribute(choice, "identifier"), Text: PlainText(choice))).ToList();
        var targets = sets[1].Elements("simpleAssociableChoice").Select(choice => (Id: Attribute(choice, "identifier"), Text: PlainText(choice))).ToList();
        var pairs = declaration.Pairs(scoring);
        var used = new HashSet<string>(StringComparer.Ordinal);

        foreach (var source in sources)
        {
            var matches = pairs.Where(pair => pair.First == source.Id).ToList();

            if (matches.Count != 1 || targets.FirstOrDefault(target => target.Id == matches[0].Second) is not { Id: not null } target)
            {
                throw new RefusedException(QtiRefusals.UnsupportedStructure, "prompt matched to no answer or to several");
            }

            used.Add(target.Id);
            question.Pairs.Add((source.Text, target.Text));
        }

        if (targets.Any(target => !used.Contains(target.Id)))
        {
            throw new RefusedException(QtiRefusals.UnsupportedStructure, "answer that matches no prompt");
        }

        question.QuestionType = AssessmentsConstants.QuestionTypes.Matching;
        question.PartialCredit = scoring.Kind == ScoringKind.MapResponse;
        question.Points = scoring.Kind == ScoringKind.MapResponse ? declaration.MappedTotal(pairs.Select(pair => pair.First + " " + pair.Second).ToHashSet(StringComparer.Ordinal)) : 1;
        ReplaceWithPrompt(interaction);
    }

    private static void ReadAssociate(XElement interaction, Dictionary<string, ResponseDeclaration> declarations, Scoring scoring, QtiQuestion question)
    {
        var declaration = Declaration(interaction, declarations);
        scoring.Require(declaration);

        var choices = interaction.Elements("simpleAssociableChoice").ToDictionary(choice => Attribute(choice, "identifier") ?? string.Empty, PlainText, StringComparer.Ordinal);
        var pairs = declaration.Pairs(scoring);
        var used = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (first, second) in pairs)
        {
            if (!choices.TryGetValue(first, out var prompt) || !choices.TryGetValue(second, out var answer) || !used.Add(first) || !used.Add(second))
            {
                throw new RefusedException(QtiRefusals.UnsupportedStructure, "choice in no pair or in several");
            }

            question.Pairs.Add((prompt, answer));
        }

        if (question.Pairs.Count == 0 || choices.Keys.Any(key => !used.Contains(key)))
        {
            throw new RefusedException(QtiRefusals.UnsupportedStructure, "choice in no pair or in several");
        }

        question.QuestionType = AssessmentsConstants.QuestionTypes.Matching;
        question.PartialCredit = scoring.Kind == ScoringKind.MapResponse;
        question.Points = scoring.Kind == ScoringKind.MapResponse ? declaration.MappedTotal(pairs.Select(pair => pair.First + " " + pair.Second).ToHashSet(StringComparer.Ordinal)) : 1;
        ReplaceWithPrompt(interaction);
    }

    private static void ReadOrder(XElement interaction, Dictionary<string, ResponseDeclaration> declarations, Scoring scoring, QtiQuestion question)
    {
        var declaration = Declaration(interaction, declarations);
        scoring.Require(declaration);

        var choices = interaction.Elements("simpleChoice").ToDictionary(choice => Attribute(choice, "identifier") ?? string.Empty, PlainText, StringComparer.Ordinal);
        var order = declaration.CorrectValues;

        if (order.Count == 0)
        {
            throw new RefusedException(QtiRefusals.NoCorrectResponse);
        }

        if (order.Count != choices.Count || order.Distinct(StringComparer.Ordinal).Count() != order.Count || order.Any(identifier => !choices.ContainsKey(identifier)))
        {
            throw new RefusedException(QtiRefusals.UnsupportedStructure, "order of only some of the choices");
        }

        foreach (var identifier in order)
        {
            question.Options.Add(new QtiOption { Identifier = identifier, Text = choices[identifier], Correct = true });
        }

        question.QuestionType = AssessmentsConstants.QuestionTypes.Ordering;
        question.Points = 1;
        ReplaceWithPrompt(interaction);
    }

    private static ResponseDeclaration Declaration(XElement interaction, Dictionary<string, ResponseDeclaration> declarations)
        => declarations.TryGetValue(Attribute(interaction, "responseIdentifier") ?? string.Empty, out var declaration)
            ? declaration
            : throw new RefusedException(QtiRefusals.NoCorrectResponse);

    // An interaction's prompt stays where the interaction was.
    private static void ReplaceWithPrompt(XElement interaction)
    {
        var prompt = interaction.Element("prompt");

        if (prompt is null || !prompt.Nodes().Any(node => node is XElement || node is XText text && !string.IsNullOrWhiteSpace(text.Value)))
        {
            interaction.Remove();

            return;
        }

        var block = prompt.Elements().Any(element => _blockElements.Contains(element.Name.LocalName));
        interaction.ReplaceWith(new XElement(block ? "div" : "p", prompt.Nodes()));
    }

    private static decimal Points(XElement item, decimal computed)
    {
        foreach (var outcome in item.Elements("outcomeDeclaration"))
        {
            if (Attribute(outcome, "identifier") == "MAXSCORE" &&
                outcome.Element("defaultValue")?.Element("value")?.Value is { } value &&
                decimal.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var maximum) && maximum > 0)
            {
                return maximum;
            }
        }

        var score = item.Elements("outcomeDeclaration").FirstOrDefault(outcome => Attribute(outcome, "identifier") == "SCORE");

        if (score is not null && decimal.TryParse(Attribute(score, "normalMaximum"), NumberStyles.Float, CultureInfo.InvariantCulture, out var normal) && normal > 0)
        {
            return normal;
        }

        return computed > 0 ? computed : 1;
    }

    // The child of the item body that holds a node.
    private static XElement BodyChild(XElement body, XElement node)
    {
        var current = node;

        while (current.Parent is not null && current.Parent != body)
        {
            current = current.Parent;
        }

        return current;
    }

    private static bool IsAncestorOf(XNode node, XElement descendant)
        => node is XElement element && descendant.Ancestors().Contains(element);

    // Whether an element holds nothing but the descendant (and white space).
    private static bool OnlyHolds(XNode node, XElement descendant)
        => node is XElement element && element.DescendantNodes().All(child => child == descendant || IsAncestorOf(child, descendant) || descendant.Ancestors().Contains(child) || child is XText text && string.IsNullOrWhiteSpace(text.Value) || child.Ancestors().Contains(descendant));

    /// <summary>
    /// Writes nodes as HTML: the HTML elements an item may hold with their safe attributes, pictures of the package as
    /// media references, and a refusal for anything else.
    /// </summary>
    private static string Html(IEnumerable<XNode> nodes, ItemContext context)
    {
        var builder = new StringBuilder();

        foreach (var node in nodes)
        {
            WriteHtml(node, builder, context);
        }

        return builder.ToString().Trim();
    }

    private static void WriteHtml(XNode node, StringBuilder builder, ItemContext context)
    {
        switch (node)
        {
            case XText text:
                builder.Append(WebUtility.HtmlEncode(text.Value));
                break;

            case XElement element:
                if (element.Name.Namespace != XNamespace.None)
                {
                    throw new RefusedException(QtiRefusals.UnsupportedContent, element.Name.LocalName);
                }

                var name = element.Name.LocalName;

                if (_droppedElements.Contains(name))
                {
                    return;
                }

                if (!_htmlElements.Contains(name))
                {
                    throw new RefusedException(QtiRefusals.UnsupportedContent, name);
                }

                builder.Append('<').Append(name);

                foreach (var attribute in element.Attributes())
                {
                    var value = SafeAttribute(name, attribute.Name.LocalName, attribute.Value, context);

                    if (value is not null)
                    {
                        builder.Append(' ').Append(attribute.Name.LocalName).Append("=\"").Append(WebUtility.HtmlEncode(value)).Append('"');
                    }
                }

                if (_voidElements.Contains(name))
                {
                    builder.Append(" />");

                    return;
                }

                builder.Append('>');

                foreach (var child in element.Nodes())
                {
                    WriteHtml(child, builder, context);
                }

                builder.Append("</").Append(name).Append('>');
                break;
        }
    }

    private static string SafeAttribute(string element, string attribute, string value, ItemContext context)
        => (element, attribute) switch
        {
            ("img", "src") => context.MediaReference(value),
            ("img", "alt" or "width" or "height" or "title") => value,
            ("a", "href") => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" or "mailto" ? value : null,
            ("a" or "abbr" or "acronym", "title") => value,
            ("td" or "th", "colspan" or "rowspan" or "scope") => value,
            ("ol", "start" or "type") => value,
            (_, "lang") => value,
            _ => null,
        };

    /// <summary>
    /// The text of an element whose question type keeps plain text: inline formatting is dropped, and anything else,
    /// such as a picture, refuses the item.
    /// </summary>
    private static string PlainText(XElement element)
    {
        var builder = new StringBuilder();
        AppendText(element, builder);

        return Collapse(builder.ToString());
    }

    private static void AppendText(XElement element, StringBuilder builder)
    {
        foreach (var node in element.Nodes())
        {
            switch (node)
            {
                case XText text:
                    builder.Append(text.Value);
                    break;

                case XElement child when child.Name.Namespace != XNamespace.None:
                    throw new RefusedException(QtiRefusals.MarkupInText, child.Name.LocalName);

                case XElement child when _droppedElements.Contains(child.Name.LocalName):
                    break;

                case XElement child when child.Name.LocalName == "br":
                    builder.Append(' ');
                    break;

                case XElement child when _inlineTextElements.Contains(child.Name.LocalName) || _blockElements.Contains(child.Name.LocalName) && child.Name.LocalName is "p" or "div" or "blockquote":
                    builder.Append(' ');
                    AppendText(child, builder);
                    builder.Append(' ');
                    break;

                case XElement child:
                    throw new RefusedException(QtiRefusals.MarkupInText, child.Name.LocalName);
            }
        }
    }

    // The text of feedback or a rubric, whatever it holds.
    private static string LooseText(XElement element)
        => Collapse(string.Join(' ', element.DescendantNodes().OfType<XText>().Select(text => text.Value)));

    private static string Collapse(string text)
    {
        var builder = new StringBuilder(text.Length);
        var space = false;

        foreach (var character in text)
        {
            if (char.IsWhiteSpace(character))
            {
                space = builder.Length > 0;

                continue;
            }

            if (space)
            {
                builder.Append(' ');
                space = false;
            }

            builder.Append(character);
        }

        // Spaces added around inline elements must not stand before punctuation.
        return builder.ToString().Replace(" .", ".", StringComparison.Ordinal).Replace(" ,", ",", StringComparison.Ordinal).Trim();
    }

    private static string NullIfEmpty(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value;

    private static string Attribute(XElement element, string name)
        => (string)element.Attribute(name);

    /// <summary>
    /// Rewrites an item in names without a namespace: QTI 3.0 names (<c>qti-choice-interaction</c>,
    /// <c>response-identifier</c>) in their QTI 2.x form (<c>choiceInteraction</c>, <c>responseIdentifier</c>); HTML
    /// elements keep their names and attributes; elements of another namespace, such as MathML, keep theirs.
    /// </summary>
    private static XElement Normalize(XElement element, XNamespace itemNamespace, bool version3)
    {
        if (element.Name.Namespace != itemNamespace)
        {
            return new XElement(element.Name, element.Attributes().Where(attribute => !attribute.IsNamespaceDeclaration), element.Nodes().Select(node => node is XElement child ? Normalize(child, itemNamespace, version3) : CopyNode(node)));
        }

        var local = element.Name.LocalName;
        var qti = version3 && local.StartsWith("qti-", StringComparison.Ordinal);
        var name = qti ? Camel(local[4..]) : local;
        var normalized = new XElement(name);

        foreach (var attribute in element.Attributes())
        {
            if (attribute.IsNamespaceDeclaration || attribute.Name.Namespace != XNamespace.None)
            {
                continue;
            }

            normalized.SetAttributeValue(qti ? Camel(attribute.Name.LocalName) : attribute.Name.LocalName, attribute.Value);
        }

        foreach (var node in element.Nodes())
        {
            normalized.Add(node is XElement child ? Normalize(child, itemNamespace, version3) : CopyNode(node));
        }

        return normalized;
    }

    private static XText CopyNode(XNode node)
        => node switch
        {
            XCData data => new XText(data.Value),
            XText text => new XText(text.Value),
            _ => new XText(string.Empty),
        };

    private static string Camel(string kebab)
    {
        var builder = new StringBuilder(kebab.Length);
        var upper = false;

        foreach (var character in kebab)
        {
            if (character == '-')
            {
                upper = true;

                continue;
            }

            builder.Append(upper ? char.ToUpperInvariant(character) : character);
            upper = false;
        }

        return builder.ToString();
    }

    private sealed class ItemContext
    {
        private readonly string _folder;
        private readonly PackageArchiveReader _archive;
        private readonly QtiReadLimits _limits;
        private readonly List<QtiMediaFile> _media = [];

        public ItemContext(string path, PackageArchiveReader archive, QtiReadLimits limits)
        {
            var slash = path.LastIndexOf('/');
            _folder = slash < 0 ? string.Empty : path[..(slash + 1)];
            _archive = archive;
            _limits = limits;
        }

        public IReadOnlyList<QtiMediaFile> Files => _media;

        // Resolves a picture of the item to a file of the package and returns its media reference.
        public string MediaReference(string source)
        {
            if (string.IsNullOrWhiteSpace(source) || source.Contains(':', StringComparison.Ordinal) || source.StartsWith("//", StringComparison.Ordinal) || source.StartsWith('/'))
            {
                throw new RefusedException(QtiRefusals.UnsupportedMedia, source);
            }

            var cut = source.IndexOfAny(['?', '#']);
            var relative = Uri.UnescapeDataString(cut < 0 ? source : source[..cut]);

            if (!_imageExtensions.Contains(Path.GetExtension(relative)))
            {
                throw new RefusedException(QtiRefusals.UnsupportedMedia, source);
            }

            var path = Resolve(_folder + relative);

            if (path is null || _archive is null || !_archive.Contains(path))
            {
                throw new RefusedException(QtiRefusals.MissingMedia, source);
            }

            var known = _media.FirstOrDefault(file => string.Equals(file.Path, path, StringComparison.OrdinalIgnoreCase));

            if (known is not null)
            {
                return known.Reference;
            }

            if (!_archive.TryRead(path, out var content, out _))
            {
                throw new RefusedException(QtiRefusals.MediaTooLarge, source);
            }

            if (content.LongLength > _limits.MaxMediaBytes)
            {
                throw new RefusedException(QtiRefusals.MediaTooLarge, source);
            }

            var file = new QtiMediaFile { Path = path, Content = content };
            _media.Add(file);

            return file.Reference;
        }

        // Resolves the dot segments of a path inside the package; a path that climbs out of it has no file.
        private static string Resolve(string path)
        {
            var segments = new List<string>();

            foreach (var segment in path.Replace('\\', '/').Split('/'))
            {
                if (segment.Length == 0 || segment == ".")
                {
                    continue;
                }

                if (segment == "..")
                {
                    if (segments.Count == 0)
                    {
                        return null;
                    }

                    segments.RemoveAt(segments.Count - 1);

                    continue;
                }

                segments.Add(segment);
            }

            return PackagePath.Normalize(string.Join('/', segments));
        }
    }

    private enum ScoringKind
    {
        None,
        MatchCorrect,
        MapResponse,
    }

    private sealed class Scoring
    {
        private decimal? _absoluteTolerance;
        private decimal? _relativeTolerance;

        public ScoringKind Kind { get; private set; }

        public static Scoring Read(XElement processing)
        {
            var scoring = new Scoring();

            if (processing is null)
            {
                return scoring;
            }

            var template = Attribute(processing, "template");

            if (!string.IsNullOrWhiteSpace(template))
            {
                var name = Path.GetFileNameWithoutExtension(template.TrimEnd('/'));

                scoring.Kind = name switch
                {
                    "match_correct" => ScoringKind.MatchCorrect,
                    "map_response" => ScoringKind.MapResponse,
                    _ => throw new RefusedException(QtiRefusals.CustomScoring, name),
                };

                return scoring;
            }

            if (!processing.Elements().Any())
            {
                return scoring;
            }

            if (processing.DescendantsAndSelf().FirstOrDefault(element => !_knownRules.Contains(element.Name.LocalName) || element.Name.Namespace != XNamespace.None) is { } unknown)
            {
                throw new RefusedException(QtiRefusals.CustomScoring, unknown.Name.LocalName);
            }

            scoring.Kind = processing.Descendants("mapResponse").Any() ? ScoringKind.MapResponse : ScoringKind.MatchCorrect;

            if (processing.Descendants("equal").FirstOrDefault(equal => Attribute(equal, "toleranceMode") is "absolute" or "relative") is { } comparison)
            {
                var bounds = (Attribute(comparison, "tolerance") ?? string.Empty)
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Select(value => decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var bound) ? bound : (decimal?)null)
                    .Where(bound => bound is >= 0)
                    .Select(bound => bound.Value)
                    .ToList();

                if (bounds.Count > 0)
                {
                    // The question type keeps one tolerance on both sides; the narrower bound is kept.
                    if (Attribute(comparison, "toleranceMode") == "absolute")
                    {
                        scoring._absoluteTolerance = bounds.Min();
                    }
                    else
                    {
                        scoring._relativeTolerance = bounds.Min();
                    }
                }
            }

            return scoring;
        }

        public decimal Tolerance(decimal answer)
            => _absoluteTolerance ?? (_relativeTolerance is { } percent ? Math.Abs(answer) * percent / 100 : 0);

        // Without a response processing an engine scores nothing; an item with a correct response is read as if it
        // used the match_correct template, the way authoring tools that leave it out mean it.
        public void Require(ResponseDeclaration declaration)
        {
            if (Kind == ScoringKind.None)
            {
                Kind = ScoringKind.MatchCorrect;
            }
        }
    }

    private sealed class ResponseDeclaration
    {
        public string Identifier { get; private init; }

        public string Cardinality { get; private init; }

        public string BaseType { get; private init; }

        public List<string> CorrectValues { get; private init; } = [];

        public List<(string Key, decimal Value, bool CaseSensitive)> Mapping { get; private init; } = [];

        public decimal? UpperBound { get; private init; }

        public static ResponseDeclaration Read(XElement element)
        {
            var mapping = element.Element("mapping");

            return new ResponseDeclaration
            {
                Identifier = Attribute(element, "identifier"),
                Cardinality = Attribute(element, "cardinality"),
                BaseType = Attribute(element, "baseType"),
                CorrectValues = element.Element("correctResponse")?.Elements("value").Select(value => value.Value.Trim()).Where(value => value.Length > 0).ToList() ?? [],
                Mapping = mapping?.Elements("mapEntry")
                    .Select(entry => (
                        Key: (Attribute(entry, "mapKey") ?? string.Empty).Trim(),
                        Value: decimal.TryParse(Attribute(entry, "mappedValue"), NumberStyles.Float, CultureInfo.InvariantCulture, out var mapped) ? mapped : 0,
                        CaseSensitive: !string.Equals(Attribute(entry, "caseSensitive"), "false", StringComparison.OrdinalIgnoreCase)))
                    .Where(entry => entry.Key.Length > 0)
                    .ToList() ?? [],
                UpperBound = mapping is not null && decimal.TryParse(Attribute(mapping, "upperBound"), NumberStyles.Float, CultureInfo.InvariantCulture, out var upper) ? upper : null,
            };
        }

        // The right identifiers of a choice: the correct response, or the mapped keys worth points.
        public HashSet<string> Correct(Scoring scoring)
        {
            var correct = CorrectValues.ToHashSet(StringComparer.Ordinal);

            if (correct.Count == 0 && scoring.Kind == ScoringKind.MapResponse)
            {
                correct.UnionWith(Mapping.Where(entry => entry.Value > 0).Select(entry => entry.Key));
            }

            return correct;
        }

        // The accepted answers of a text entry: the correct response first, then the mapped answers worth points.
        public List<string> Accepted(Scoring scoring)
        {
            var accepted = new List<string>(CorrectValues);

            if (scoring.Kind == ScoringKind.MapResponse)
            {
                accepted.AddRange(Mapping.Where(entry => entry.Value > 0).Select(entry => entry.Key));
            }

            return accepted.Distinct(StringComparer.Ordinal).ToList();
        }

        public bool CaseSensitive(Scoring scoring)
            => scoring.Kind != ScoringKind.MapResponse || Mapping.All(entry => entry.CaseSensitive);

        public List<(string First, string Second)> Pairs(Scoring scoring)
        {
            var values = CorrectValues.Count > 0 || scoring.Kind != ScoringKind.MapResponse
                ? CorrectValues
                : Mapping.Where(entry => entry.Value > 0).Select(entry => entry.Key).ToList();

            return values
                .Select(value => value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                .Where(parts => parts.Length == 2)
                .Select(parts => (parts[0], parts[1]))
                .ToList();
        }

        // The points of a mapped response with every right answer given, within the upper bound.
        public decimal MappedTotal(HashSet<string> right)
        {
            var total = Mapping.Where(entry => entry.Value > 0 && (right.Count == 0 || right.Contains(entry.Key))).Sum(entry => entry.Value);

            return UpperBound is { } upper && upper > 0 ? Math.Min(total, upper) : total;
        }

        // The points of the best single mapped answer, within the upper bound.
        public decimal MappedMaximum()
        {
            var maximum = Mapping.Count == 0 ? 1 : Mapping.Max(entry => entry.Value);

            return UpperBound is { } upper && upper > 0 ? Math.Min(maximum, upper) : maximum;
        }
    }

    private sealed class RefusedException : Exception
    {
        public RefusedException(string refusal, string detail = null)
            : base(refusal)
        {
            Refusal = refusal;
            Detail = detail;
        }

        public string Refusal { get; }

        public string Detail { get; }
    }
}
