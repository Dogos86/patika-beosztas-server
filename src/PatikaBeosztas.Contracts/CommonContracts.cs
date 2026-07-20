namespace PatikaBeosztas.Contracts;

public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record ApiValidationError(string Code, string Message, string? Field = null);

public sealed record CsrfTokenResponse(string RequestToken, string HeaderName);
