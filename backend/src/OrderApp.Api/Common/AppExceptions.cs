namespace OrderApp.Api.Common;

public class BusinessRuleException : Exception
{
    public BusinessRuleException(string message, IReadOnlyList<string>? reasons = null)
        : base(message)
    {
        Reasons = reasons ?? Array.Empty<string>();
    }

    public IReadOnlyList<string> Reasons { get; }
}

public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message)
    {
    }
}
