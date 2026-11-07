namespace RaceDay.Web.Services;

/// <summary>The RaceDay API could not be reached (not running, wrong address or timed out).</summary>
public class ApiUnavailableException : Exception
{
    public ApiUnavailableException(Exception inner) : base("The RaceDay API could not be reached.", inner) { }
}

/// <summary>The API no longer recognises the user's session (it expired or the API was restarted).</summary>
public class ApiSessionExpiredException : Exception
{
    public ApiSessionExpiredException() : base("The API session has expired.") { }
}
