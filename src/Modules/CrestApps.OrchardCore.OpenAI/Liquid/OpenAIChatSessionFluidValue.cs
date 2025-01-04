using CrestApps.OrchardCore.OpenAI.Models;
using Fluid;
using Fluid.Values;

namespace CrestApps.OrchardCore.OpenAI.Liquid;

internal sealed class OpenAIChatSessionFluidValue : FluidValue
{
    private readonly OpenAIChatSession _session;

    public OpenAIChatSessionFluidValue()
    {
    }

    public OpenAIChatSessionFluidValue(OpenAIChatSession session)
    {
        _session = session;
    }

    public override FluidValues Type => FluidValues.Object;

    public override ValueTask<FluidValue> GetValueAsync(string name, TemplateContext context)
    {
        if (_session == null)
        {
            return ValueTask.FromResult<FluidValue>(NilValue.Instance);
        }

        FluidValue result = name switch
        {
            nameof(OpenAIChatSession.Title) => StringValue.Create(_session.Title),
            nameof(OpenAIChatSession.SessionId) => StringValue.Create(_session.SessionId),
            nameof(OpenAIChatSession.UserId) => StringValue.Create(_session.UserId),
            nameof(OpenAIChatSession.ClientId) => StringValue.Create(_session.ClientId),
            nameof(OpenAIChatSession.CreatedUtc) => new DateTimeValue(_session.CreatedUtc),
            nameof(OpenAIChatSession.Prompts) => new ArrayValue(_session.Prompts.Select(x => new ObjectValue(x)).ToList()),
            _ => NilValue.Instance
        };

        return ValueTask.FromResult(result);
    }

    public override bool Equals(FluidValue other)
    {
        if (other is null)
        {
            return false;
        }

        return ToStringValue() == other.ToStringValue();
    }

    public override bool ToBooleanValue() => true;

    public override decimal ToNumberValue() => 0;

    public override object ToObjectValue() => _session;

    public override string ToStringValue() => _session.Title;
}
