using Microsoft.AspNetCore.Diagnostics;

namespace Deepfake_analysis.Services
{
    /// <summary>
    /// Ошибка, о которой сервис сообщает клиенту: код ответа HTTP и понятный текст.
    /// Сервис только бросает её, а в ответ её превращает <see cref="ServiceExceptionHandler"/> —
    /// так контроллерам не нужно разбирать ошибки самим.
    /// </summary>
    public class ServiceException(int statusCode, string message) : Exception(message)
    {
        public int StatusCode { get; } = statusCode;
    }

    /// <summary>404: запрошенной записи нет.</summary>
    public class NotFoundException(string message) : ServiceException(StatusCodes.Status404NotFound, message);

    /// <summary>400: в запросе неверные данные.</summary>
    public class BadRequestException(string message) : ServiceException(StatusCodes.Status400BadRequest, message);

    /// <summary>409: запрос правильный, но противоречит текущему состоянию данных.</summary>
    public class ConflictException(string message) : ServiceException(StatusCodes.Status409Conflict, message);

    /// <summary>503: сервер сейчас не может выполнить запрос (например, нет файла модели).</summary>
    public class ServiceUnavailableException(string message) : ServiceException(StatusCodes.Status503ServiceUnavailable, message);

    /// <summary>Превращает <see cref="ServiceException"/> в ответ в стандартном формате ProblemDetails.</summary>
    public sealed class ServiceExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
    {
        public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            if (exception is not ServiceException serviceException)
                return false;   // остальные ошибки — настоящие сбои, их обработает ASP.NET (ответ 500)

            httpContext.Response.StatusCode = serviceException.StatusCode;
            return await problemDetails.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                Exception = exception,
                ProblemDetails = { Status = serviceException.StatusCode, Detail = serviceException.Message }
            });
        }
    }
}
