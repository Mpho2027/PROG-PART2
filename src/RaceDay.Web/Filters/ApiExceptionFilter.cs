using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using RaceDay.Web.Services;

namespace RaceDay.Web.Filters;

/// <summary>
/// Turns API problems into friendly pages instead of raw errors:
/// the API being down shows the "service unavailable" page, and an expired API session logs the user out.
/// </summary>
public class ApiExceptionFilter : IExceptionFilter
{
    private readonly UserSession _session;
    private readonly ITempDataDictionaryFactory _tempData;

    public ApiExceptionFilter(UserSession session, ITempDataDictionaryFactory tempData)
    {
        _session = session;
        _tempData = tempData;
    }

    public void OnException(ExceptionContext context)
    {
        switch (context.Exception)
        {
            case ApiUnavailableException:
                context.Result = new ViewResult
                {
                    ViewName = "~/Views/Shared/ServiceUnavailable.cshtml",
                    StatusCode = StatusCodes.Status503ServiceUnavailable
                };
                context.ExceptionHandled = true;
                break;

            case ApiSessionExpiredException:
                _session.SignOut();
                _tempData.GetTempData(context.HttpContext)["Error"] = "Your session has expired. Please log in again.";
                var request = context.HttpContext.Request;
                var returnUrl = HttpMethods.IsGet(request.Method) ? $"{request.PathBase}{request.Path}{request.QueryString}" : null;
                context.Result = new RedirectToActionResult("Login", "Account", new { area = "", returnUrl });
                context.ExceptionHandled = true;
                break;
        }
    }
}
