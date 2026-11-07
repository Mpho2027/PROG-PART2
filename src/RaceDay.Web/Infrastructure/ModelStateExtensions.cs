using Microsoft.AspNetCore.Mvc.ModelBinding;
using RaceDay.Web.Services;

namespace RaceDay.Web.Infrastructure;

public static class ModelStateExtensions
{
    /// <summary>
    /// Shows the API's validation errors next to the matching form fields. View model properties use the same
    /// names as the API's request fields, so "EventName" from the API lands on the EventName input.
    /// </summary>
    public static void AddApiErrors(this ModelStateDictionary modelState, ApiResult result, string? prefix = null)
    {
        if (result.FieldErrors.Count == 0)
        {
            modelState.AddModelError(string.Empty, result.ErrorMessage ?? "Something went wrong. Please try again.");
            return;
        }

        foreach (var (field, messages) in result.FieldErrors)
        {
            var key = string.IsNullOrEmpty(field) ? string.Empty : char.ToUpperInvariant(field[0]) + field[1..];
            if (prefix is not null && key.Length > 0) key = $"{prefix}.{key}";
            foreach (var message in messages) modelState.AddModelError(key, message);
        }
    }
}
