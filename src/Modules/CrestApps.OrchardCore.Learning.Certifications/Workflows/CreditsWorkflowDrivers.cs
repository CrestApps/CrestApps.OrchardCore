using OrchardCore.Workflows.Display;
using OrchardCore.Workflows.ViewModels;

namespace CrestApps.OrchardCore.Learning.Certifications.Workflows;

public class AwardEducationCreditsTaskViewModel
{
    public string CreditType { get; set; }

    public string UserId { get; set; }

    public string Amount { get; set; }

    public string SourceId { get; set; }
}

public sealed class EducationCreditsAwardedEventDisplayDriver : ActivityDisplayDriver<EducationCreditsAwardedEvent>
{
}

public sealed class ExternalLearningSubmittedEventDisplayDriver : ActivityDisplayDriver<ExternalLearningSubmittedEvent>
{
}

public sealed class AwardEducationCreditsTaskDisplayDriver : ActivityDisplayDriver<AwardEducationCreditsTask, AwardEducationCreditsTaskViewModel>
{
    protected override void EditActivity(AwardEducationCreditsTask activity, AwardEducationCreditsTaskViewModel model)
    {
        model.CreditType = activity.CreditType;
        model.UserId = activity.UserId;
        model.Amount = activity.Amount;
        model.SourceId = activity.SourceId;
    }

    protected override void UpdateActivity(AwardEducationCreditsTaskViewModel model, AwardEducationCreditsTask activity)
    {
        activity.CreditType = model.CreditType?.Trim();
        activity.UserId = model.UserId?.Trim();
        activity.Amount = model.Amount?.Trim();
        activity.SourceId = model.SourceId?.Trim();
    }
}
