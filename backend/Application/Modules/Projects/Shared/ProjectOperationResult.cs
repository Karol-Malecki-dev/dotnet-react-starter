namespace Application.Modules.Projects.Shared;

public enum ProjectOperationStatus
{
    Success,
    NotFound,
    ValidationError,
    Conflict,
    Forbidden
}

public sealed record ProjectOperationResult<T>(
    ProjectOperationStatus Status,
    T? Value = default,
    string Message = "Success",
    int CreatedStatusCode = 200)
{
    public bool IsSuccess => Status == ProjectOperationStatus.Success;

    public static ProjectOperationResult<T> Success(T value, string message = "Success", int statusCode = 200)
        => new(ProjectOperationStatus.Success, value, message, statusCode);

    public static ProjectOperationResult<T> Failure(ProjectOperationStatus status, string message)
        => new(status, default, message);
}
