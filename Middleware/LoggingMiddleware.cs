namespace UserManagementApi.Middleware
{
    public class LoggingMiddleware
    {
        private readonly RequestDelegate _next;

        public LoggingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
           // Log Request details such as the HTTP method and the request path to the console for debugging and monitoring purposes.
            var request = context.Request;
            Console.WriteLine($"HTTP Request: {request.Method} {request.Path} ");

            /*Copy original response body stream.
            The using statement ensures that the memory stream is properly disposed of after use, 
            preventing memory leaks and ensuring efficient resource management. 
            By wrapping the MemoryStream in a using block, we guarantee that it will be cleaned up even 
            if an exception occurs, which is crucial for maintaining application performance and stability.*/
            var originalBodyStream = context.Response.Body;
            using var responseBody = new MemoryStream(); 

            // Redirect the response body to the memory stream for logging
            context.Response.Body = responseBody;

            await _next(context);

            // Log Response 
            /*The first line is used to reset the position of the response body stream to the beginning (0) before reading its contents.
            This is necessary because after the response has been written to the memory stream, the stream's
            position is at the end. If we try to read from it without resetting the position, we would get an empty result.*/
            context.Response.Body.Seek(0, SeekOrigin.Begin);
            var responseText = await new StreamReader(context.Response.Body).ReadToEndAsync();
            Console.WriteLine($"HTTP Response: {context.Response.StatusCode} {responseText}");

            /* Copy the contents of the new memory stream (which contains the response) to the original stream.
            This step is crucial because the response body has been temporarily redirected to a memory stream for logging purposes.    
            By copying the contents back to the original stream, we ensure that the response is correctly sent
            to the client while still allowing us to log the response content. */
            context.Response.Body.Seek(0, SeekOrigin.Begin);
            await responseBody.CopyToAsync(originalBodyStream);
            context.Response.Body = originalBodyStream;
        }
    }
}