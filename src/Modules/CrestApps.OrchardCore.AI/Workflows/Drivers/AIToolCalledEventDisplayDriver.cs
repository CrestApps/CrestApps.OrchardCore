using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.AI.Workflows.Models;
using CrestApps.OrchardCore.AI.Workflows.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Liquid;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Workflows.Display;

namespace CrestApps.OrchardCore.AI.Workflows.Drivers;

/// <summary>
/// Shows the AI Tool Called event, which has nothing to edit.
/// </summary>
public sealed class AIToolCalledEventDisplayDriver : ActivityDisplayDriver<AIToolCalledEvent>
{
}
