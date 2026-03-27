# Orchard Core placement examples

## Example 1: Card-based editor layout

Use cards when the editor should stay in a single `Content` zone but related fields need to be visually grouped:

```json
{
  "AIProfileGeneralFields_Edit": [
    {
      "place": "Content:1%General;1"
    }
  ],
  "AIProfileDeployment_Edit": [
    {
      "place": "Content:2%Deployments;1"
    }
  ],
  "AIProfileInteractionFields_Edit": [
    {
      "place": "Content:3%Interactions;2"
    }
  ],
  "AIProfileInstructionFields_Edit": [
    {
      "place": "Content:4%Instructions;3"
    }
  ]
}
```

## Example 2: Tabs plus cards

```json
{
  "MySummary_Edit": [
    {
      "place": "Content:1#General;1%Overview;1"
    }
  ],
  "MyToolSettings_Edit": [
    {
      "place": "Content:5#Capabilities;8%Tools;3"
    }
  ],
  "MyAgentSettings_Edit": [
    {
      "place": "Content:6#Capabilities;8%Agents;2"
    }
  ]
}
```

## Example 3: Cards with columns

```json
{
  "SeoBasic_Edit": [
    {
      "place": "Content:1%SEO;1|Left;1"
    }
  ],
  "SeoAdvanced_Edit": [
    {
      "place": "Content:1%SEO;1|Right;2"
    }
  ]
}
```

## Example 4: Fluent placement in a display driver

```csharp
public sealed class MySettingsDisplayDriver : SiteDisplayDriver<MySettings>
{
    public override IDisplayResult Edit(ISite site, MySettings settings, BuildEditorContext context)
    {
        return Initialize<MySettingsViewModel>("MySettings_Edit", model =>
        {
            model.Enabled = settings.Enabled;
        })
        .Location(c => c
            .Zone("Content", "4")
            .Tab("Capabilities", "8")
            .Card("Tools", "3")
            .Column("Right", "2"));
    }
}
```

## Example 5: Dynamic placement provider

```csharp
public sealed class MyPlacementProvider : IShapePlacementProvider
{
    public Task<IPlacementInfoResolver> BuildPlacementInfoResolverAsync(IBuildShapeContext context)
    {
        return Task.FromResult<IPlacementInfoResolver>(new Resolver());
    }

    private sealed class Resolver : IPlacementInfoResolver
    {
        public PlacementInfo ResolvePlacement(ShapePlacementContext context)
        {
            if (context.ShapeType == "MyShape")
            {
                return new PlacementInfo
                {
                    Location = "Content:2#General;1%Details;1",
                };
            }

            return null;
        }
    }
}
```
