using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace RaceDay.Api.Swagger;

/// <summary>
/// Adds a "Role required" line to every operation's description, derived from its [Authorize] attributes,
/// and documents the 401/403 responses that protected endpoints can return.
/// This keeps the Swagger UI in step with the "Role Required" column of the endpoint plan.
/// </summary>
public class AuthorizationOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var endpointMetadata = context.ApiDescription.ActionDescriptor.EndpointMetadata;
        var allowAnonymous = endpointMetadata.OfType<IAllowAnonymous>().Any();
        var authorize = endpointMetadata.OfType<IAuthorizeData>().ToList();

        string roleText;
        if (allowAnonymous || authorize.Count == 0)
        {
            roleText = "None (public)";
        }
        else
        {
            var roles = authorize
                .SelectMany(a => (a.Roles ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Distinct()
                .ToList();
            roleText = roles.Count == 0 ? "Any (logged in)" : string.Join(" or ", roles);

            operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Not logged in (no valid session)." });
            if (roles.Count > 0)
                operation.Responses.TryAdd("403", new OpenApiResponse { Description = $"Logged in, but not as {roleText}." });

            operation.Security = new List<OpenApiSecurityRequirement>
            {
                new()
                {
                    [new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "SessionCookie" }
                    }] = Array.Empty<string>()
                }
            };
        }

        var line = $"**Role required:** {roleText}";
        operation.Description = string.IsNullOrWhiteSpace(operation.Description)
            ? line
            : $"{line}\n\n{operation.Description}";
    }
}
