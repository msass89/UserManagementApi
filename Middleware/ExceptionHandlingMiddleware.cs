using System.Text.Json;


// Middleware to handle exceptions globally and return standardized error responses
namespace UserManagementApi.Middleware
{
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;

        public ExceptionHandlingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unhandled exception: {ex.Message}");
                context.Response.StatusCode = 500;
                context.Response.ContentType = "application/json";
                var errorJson = JsonSerializer.Serialize(new { error = "Internal server error." });
                await context.Response.WriteAsync(errorJson);
            }
        }
    }
}