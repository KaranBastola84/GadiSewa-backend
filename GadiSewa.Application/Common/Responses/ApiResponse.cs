namespace GadiSewa.Application.Common.Responses;

public sealed class ApiResponse<T>
{
    public T? Result { get; init; }

    public bool IsSuccess { get; init; }

    public int StatusCode { get; init; }

    public IReadOnlyList<string> ErrorMessage { get; init; } = Array.Empty<string>();

    public static ApiResponse<T> Success(T? result, int statusCode = 200)
    {
        return new ApiResponse<T>
        {
            Result = result,
            IsSuccess = true,
            StatusCode = statusCode,
            ErrorMessage = Array.Empty<string>()
        };
    }

    public static ApiResponse<T> Failure(string errorMessage, int statusCode)
    {
        return new ApiResponse<T>
        {
            Result = default,
            IsSuccess = false,
            StatusCode = statusCode,
            ErrorMessage = new[] { errorMessage }
        };
    }

    public static ApiResponse<T> Failure(IEnumerable<string> errorMessages, int statusCode)
    {
        return new ApiResponse<T>
        {
            Result = default,
            IsSuccess = false,
            StatusCode = statusCode,
            ErrorMessage = errorMessages.ToArray()
        };
    }
}