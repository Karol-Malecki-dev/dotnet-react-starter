using Microsoft.AspNetCore.Mvc.ModelBinding;
using Shared.Responses;

namespace API.Filters;

/// <summary>
/// Creates the standard API response for model-binding and validation failures.
/// </summary>
public static class ValidationResponseFactory
{
    /// <summary>
    /// Maps every model-state error to an <see cref="ErrorDetail"/> entry.
    /// </summary>
    public static ApiResponse<object> Create(ModelStateDictionary modelState)
    {
        var errors = modelState
            .Where(entry => entry.Value is { Errors.Count: > 0 })
            .SelectMany(entry => entry.Value!.Errors.Select(error => new ErrorDetail(
                string.IsNullOrWhiteSpace(error.ErrorMessage) ? "The value is invalid." : error.ErrorMessage,
                string.IsNullOrWhiteSpace(entry.Key) ? null : entry.Key,
                "Validation")))
            .ToList();

        return ApiResponse<object>.Error(400, "Validation failed", errors);
    }
}