using System.Net;

namespace RaceDay.Web.Services;

/// <summary>The outcome of a call to the RaceDay API, with the API's error message when it failed.</summary>
public class ApiResult
{
    private static readonly IReadOnlyDictionary<string, string[]> NoFieldErrors = new Dictionary<string, string[]>();

    public bool Succeeded { get; init; }
    public HttpStatusCode StatusCode { get; init; }

    /// <summary>A message that is safe to show to the user.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Validation errors returned by the API, keyed by field name (e.g. "EventName").</summary>
    public IReadOnlyDictionary<string, string[]> FieldErrors { get; init; } = NoFieldErrors;

    public bool IsNotFound => StatusCode == HttpStatusCode.NotFound;
    public bool IsForbidden => StatusCode == HttpStatusCode.Forbidden;
}

public class ApiResult<T> : ApiResult
{
    public T? Data { get; init; }
}
