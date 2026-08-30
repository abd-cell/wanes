using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Wanes.Shareds.Models;

namespace Wanes.Shareds.Attributes;

/// <summary>
/// Short-circuits invalid model state into a failed <see cref="BaseResponse"/>
/// (HTTP 200 + ValidationError), collecting the messages into <c>Errors</c>.
/// Registered globally so every action validates its inputs uniformly.
/// </summary>
public sealed class ValidateModelAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (context.ModelState.IsValid) return;

        var errors = context.ModelState.Values
            .SelectMany(v => v.Errors)
            .Select(e => e.ErrorMessage)
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .ToList();

        var response = BaseResponse.Fail(ErrorCode.ValidationError,
            errors.FirstOrDefault() ?? "Validation failed.");
        response.Errors = errors;

        context.Result = new OkObjectResult(response);
    }
}
