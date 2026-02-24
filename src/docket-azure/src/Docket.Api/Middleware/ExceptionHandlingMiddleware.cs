using Docket.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Docket.Api.Middleware;

/// <summary>
/// Catches domain exceptions thrown by services and maps them to
/// RFC 7807 Problem Details responses with Docket error codes.
///
/// This keeps endpoint handlers clean — they never catch exceptions,
/// they just let domain exceptions propagate here.
/// </summary>
public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (DocketException ex)
        {
            await HandleDocketExceptionAsync(context, ex);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception");
            await HandleUnexpectedExceptionAsync(context);
        }
    }

    private static async Task HandleDocketExceptionAsync(HttpContext context, DocketException ex)
    {
        var statusCode = ex switch
        {
            NotFoundException            => StatusCodes.Status404NotFound,
            ForbiddenException           => StatusCodes.Status403Forbidden,
            EmailExistsException         => StatusCodes.Status409Conflict,
            DuplicateParticipantException => StatusCodes.Status409Conflict,
            DuplicateAttendeeException   => StatusCodes.Status409Conflict,
            DuplicateLabelException      => StatusCodes.Status409Conflict,
            LabelAlreadyAppliedException => StatusCodes.Status409Conflict,
            _                            => StatusCodes.Status422UnprocessableEntity
        };

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = ex.ErrorCode,
            Detail = ex.Message
        };

        // Attach structured detail for errors that carry additional data
        if (ex is UnresolvedDraftsException draftsEx)
        {
            problem.Extensions["unresolvedMinutes"] = draftsEx.UnresolvedDrafts
                .Select(d => new { id = d.Id, scheduledFor = d.ScheduledFor })
                .ToArray();
        }

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(problem));
    }

    private static async Task HandleUnexpectedExceptionAsync(HttpContext context)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "INTERNAL_ERROR",
            Detail = "An unexpected error occurred."
        };

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(problem));
    }
}
