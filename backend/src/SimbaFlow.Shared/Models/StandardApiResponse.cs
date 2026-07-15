namespace SimbaFlow.Shared.Models;

public class StandardApiResponse<T>
{
    public bool IsSuccess { get; set; }
    public T? Data { get; set; }
    public string? Message { get; set; }
    public object? Errors { get; set; }
    public int StatusCode { get; set; }

    public static StandardApiResponse<T> Ok(T data, string? message = null)
        => new() { IsSuccess = true, Data = data, StatusCode = 200, Message = message };

    public static StandardApiResponse<T> Created(T data, string? message = null)
        => new() { IsSuccess = true, Data = data, StatusCode = 201, Message = message };

    public static StandardApiResponse<T> Fail(string message, int statusCode = 400, object? errors = null)
        => new() { IsSuccess = false, Message = message, StatusCode = statusCode, Errors = errors };
}

public class StandardApiResponse
{
    public bool IsSuccess { get; set; }
    public string? Message { get; set; }
    public object? Errors { get; set; }
    public int StatusCode { get; set; }

    public static StandardApiResponse Ok(string? message = null)
        => new() { IsSuccess = true, StatusCode = 200, Message = message };

    public static StandardApiResponse Fail(string message, int statusCode = 400, object? errors = null)
        => new() { IsSuccess = false, Message = message, StatusCode = statusCode, Errors = errors };
}
