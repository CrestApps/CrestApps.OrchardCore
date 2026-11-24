using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Learning.Certifications.ViewModels;

public class CompetenciesPartViewModel
{
    public string[] SkillIds { get; set; } = [];

    public int Proficiency { get; set; }

    public IList<SelectListItem> Skills { get; set; } = [];
}

public class CompetenciesDisplayViewModel
{
    public IReadOnlyList<string> Names { get; set; } = [];

    public int Proficiency { get; set; }
}

public class MyCompetenciesViewModel
{
    public IReadOnlyList<MyCompetencyFramework> Frameworks { get; set; } = [];
}

public class MyCompetencyFramework
{
    public string Name { get; set; }

    public IReadOnlyList<MyCompetency> Competencies { get; set; } = [];
}

public class MyCompetency
{
    public string Name { get; set; }

    public string Description { get; set; }

    public int Proficiency { get; set; }

    public DateOnly? Since { get; set; }

    public DateOnly? Until { get; set; }
}
