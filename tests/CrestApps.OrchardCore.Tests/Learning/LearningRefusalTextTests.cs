using System.Reflection;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Tests.Learning;

public sealed class LearningRefusalTextTests
{
    [Fact]
    public void EveryRefusalCode_HasTextForPeople_AndAnUnknownCodeIsShownAsIs()
    {
        // Arrange
        var text = new LearningRefusalText(new EchoLocalizer());
        var codes = typeof(LearningRefusals).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral)
            .Select(field => (string)field.GetRawConstantValue())
            .ToArray();

        // Act
        var texts = codes.ToDictionary(code => code, code => text.Get(code));

        // Assert
        Assert.NotEmpty(codes);
        Assert.All(texts, pair =>
        {
            Assert.NotNull(pair.Value);
            Assert.NotEqual(pair.Key, pair.Value.Value);
            Assert.Contains(' ', pair.Value.Value);
        });
        Assert.Equal("CustomRuleCode", text.Get("CustomRuleCode").Value);
        Assert.Null(text.Get(null));
    }

    private sealed class EchoLocalizer : IStringLocalizer<LearningRefusalText>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
