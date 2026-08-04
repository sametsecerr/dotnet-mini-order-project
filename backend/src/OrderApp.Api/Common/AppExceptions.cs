namespace OrderApp.Api.Common;

/// <summary>
/// İş kuralı ihlali. Servis katmanı bu tipi fırlatır, controller HTTP'ye çevirmez;
/// çeviriyi <see cref="ExceptionHandlingMiddleware"/> yapar.
/// </summary>
public class BusinessRuleException : Exception
{
    public BusinessRuleException(string message, IReadOnlyList<string>? reasons = null)
        : base(message)
    {
        Reasons = reasons ?? Array.Empty<string>();
    }

    /// <summary>Kullanıcıya gösterilecek satır bazlı sebepler (ör. hangi üründe stok yetersiz).</summary>
    public IReadOnlyList<string> Reasons { get; }
}

/// <summary>İstenen kayıt veritabanında yok.</summary>
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message)
    {
    }
}
