using System;
using System.Collections.Generic;
using System.Text;

namespace CrestApps.OrchardCore.OpenAI.Core.Models;

public sealed class OpenAIProfileElasticsearchMetadata
{
    public string IndexName { get; set; }

    public int? Strictness { get; set; }

    public int? TopNDocuments { get; set; }
}
