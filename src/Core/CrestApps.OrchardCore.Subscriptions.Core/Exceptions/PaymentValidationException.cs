namespace CrestApps.OrchardCore.Subscriptions.Core.Exceptions;

public class PaymentValidationException : Exception
{
    public PaymentValidationException(string message)
        : base(message) { }
}
