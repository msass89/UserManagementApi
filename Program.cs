using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System.Text;

/* Secret key and issuer for JWT (store securely in production)
Instead of hardcoding these values, consider using a secure configuration source 
such as environment variables, configuration files or a secret manager. */
string jwtKey = "super_secret_jwt_key_12345_67890_abcde";
string jwtIssuer = "UserManagementApi";

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddSingleton<UserManagementApi.UserService>();

var app = builder.Build();

// Middleware to catch unhandled exceptions and return JSON error responses
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Unhandled exception: {ex.Message}");
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";

        // Return a generic error message in JSON format to avoid exposing sensitive details about the exception to the client.
        var errorJson = System.Text.Json.JsonSerializer.Serialize(new { error = "Internal server error." });
        await context.Response.WriteAsync(errorJson);
    }
});

/* Middleware to authenticate requests using a simple token validation mechanism.
This middleware checks for the presence of an Authorization header in the incoming HTTP request,
and validates the token against a predefined list of valid tokens. If the token is missing or invalid,
the middleware responds with a 401 Unauthorized status code and a JSON error message. */
/* This Example expects token in Authorization header as "Bearer <token>" 
Bearer token are used for simple token-based authentication with 
OAuth 2.0 and other token-based authentication schemes. */
app.Use(async (context, next) =>
{
    // Allow anonymous access to /login endpoint
    if (context.Request.Path.Equals("/login", StringComparison.OrdinalIgnoreCase))
    {
        await next();
        return;
    }

    var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer "))
    {
        context.Response.StatusCode = 401;
        context.Response.ContentType = "application/json";
        var errorJson = System.Text.Json.JsonSerializer.Serialize(new { error = "Unauthorized: Missing or invalid token." });
        await context.Response.WriteAsync(errorJson);
        return;
    }

    var token = authHeader.Substring("Bearer ".Length).Trim();
    var tokenHandler = new JwtSecurityTokenHandler();
    var key = Encoding.UTF8.GetBytes(jwtKey);
    try
    {
        tokenHandler.ValidateToken(token, new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtIssuer,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        }, out SecurityToken validatedToken);
    }
    catch
    {
        context.Response.StatusCode = 401;
        context.Response.ContentType = "application/json";
        var errorJson = System.Text.Json.JsonSerializer.Serialize(new { error = "Unauthorized: Invalid or expired token." });
        await context.Response.WriteAsync(errorJson);
        return;
    }

    await next();
});

// Middleware to log HTTP requests and responses
app.Use(async (context, next) =>
{
    // Log Request details such as the HTTP method and the request path to the console for debugging and monitoring purposes.
    var request = context.Request;
    Console.WriteLine($"HTTP Request: {request.Method} {request.Path}");

    /*Copy original response body stream.
    The using statement ensures that the memory stream is properly disposed of after use, 
    preventing memory leaks and ensuring efficient resource management. 
    By wrapping the MemoryStream in a using block, we guarantee that it will be cleaned up even 
    if an exception occurs, which is crucial for maintaining application performance and stability.*/
    var originalBodyStream = context.Response.Body;
    using var responseBody = new MemoryStream(); 

    // Redirect the response body to the memory stream for logging
    context.Response.Body = responseBody;

    await next();

    // Log Response 
    context.Response.Body.Seek(0, SeekOrigin.Begin);
    var responseText = await new StreamReader(context.Response.Body).ReadToEndAsync();
    context.Response.Body.Seek(0, SeekOrigin.Begin);
    Console.WriteLine($"HTTP Response: {context.Response.StatusCode} {responseText}");

    /* Copy the contents of the new memory stream (which contains the response) to the original stream.
    This step is crucial because the response body has been temporarily redirected to a memory stream for logging purposes.    By copying the contents back to the original stream, we ensure that the response is correctly sent
    to the client while still allowing us to log the response content. */
    await responseBody.CopyToAsync(originalBodyStream);
    context.Response.Body = originalBodyStream;
});

app.UseHttpsRedirection();


// Get all users
app.MapGet("/user", async (UserManagementApi.UserService userService) =>
    Results.Ok(await userService.GetAll())
);

// Get user by id
app.MapGet("/user/{id}", async (int id, UserManagementApi.UserService userService) =>
{
    var user = await userService.GetById(id);
    if (user == null)
        return Results.NotFound();
    return Results.Ok(user);
});

// Create a new user
app.MapPost("/user", async (UserManagementApi.User user, UserManagementApi.UserService userService) =>
{
    //validate the user data before trying to add the user to the collection
    if (!IsValidUser(user, userService, out var error))
        return Results.BadRequest(new { error });

    // try to add the user
    var success = await userService.Add(user);
    if (!success)
        return Results.BadRequest("Failed to create user.");
    return Results.Created($"/user/{user.Id}", user);
});

// Update an existing user
app.MapPut("/user/{id}", async (int id, UserManagementApi.User updatedUser, UserManagementApi.UserService userService) =>
{
    //check if the user exists before validating the updated user data to avoid unnecessary validation if the user doesn't exist
    var existingUser = await userService.GetById(id);
    if (existingUser == null)
        return Results.NotFound();

    //validate the updated user data
    if (!IsValidUser(updatedUser, userService, out var error))
        return Results.BadRequest(new { error });

    // try to update the user
    var updated = await userService.Update(id, updatedUser);
    if (!updated)
        return Results.NotFound();

    return Results.Ok(new { message = "Succesfully updated user", id });
});

// Delete a user
app.MapDelete("/user/{id}", async (int id, UserManagementApi.UserService userService) =>
{
    // try to delete the user
    var deleted = await userService.Delete(id);
    if (!deleted)
        return Results.NotFound();
    return Results.Ok(new { message = "Succesfully deleted user", id });
});


// JWT login endpoint (accepts JSON body)
app.MapPost("/login", (LoginRequest login) =>
{
    Console.WriteLine($"Generating JWT token for user: {login.username} and password: {login.password}");
    if (login.username == "admin" && login.password == "password")
    {
        Console.WriteLine("Login successful");
        // Generate JWT token for authenticated user
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(jwtKey);
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            /* The Subject property of the SecurityTokenDescriptor is set to a new ClaimsIdentity 
            that contains a single claim representing the username of the authenticated user. */
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Name, login.username)
            }),

            // Set token expiration time (e.g., 1 hour)
            Expires = DateTime.UtcNow.AddHours(1),

            /* Issuer identifies the principal that issued the token, and Audience identifies 
            the recipients that the token is intended for. */
            Issuer = jwtIssuer,
            Audience = jwtIssuer,

            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        string jwt = tokenHandler.WriteToken(token);
        return Results.Ok(new { message = "Login successful", token = jwt });
    }
    return Results.Unauthorized();
});

app.Run();

// Helper method to validate user data before creating or updating a user
bool IsValidUser(UserManagementApi.User user, UserManagementApi.UserService userService, out string error)
{
    // valide if username is not empty, between 3 and 30 characters, and only contains letters and numbers
    error = string.Empty;
    if (string.IsNullOrWhiteSpace(user.Username) || user.Username.Length < 3 || user.Username.Length > 30 || !System.Text.RegularExpressions.Regex.IsMatch(user.Username, @"^[a-zA-Z0-9]+$"))
    {
        error = "Username is required, should be between 3 and 30 characters and only contain letters and numbers.";
        return false;
    }

    // Validate email is not empty, less than 254 characters, and in a valid format
    if (string.IsNullOrWhiteSpace(user.Email) || user.Email.Length > 254) 
    {
        error = "Email is required and should be less than 254 characters.";
        return false;
    }
    try
    {
        // Validate email format
        var addr = new System.Net.Mail.MailAddress(user.Email);
        if (addr.Address != user.Email)
        {
            error = "Invalid email format.";
            return false;
        }
    }
    catch
    {
        error = "Invalid email format.";
        return false;
    }

    // Validate if the username is already in use by another user
    if (userService.GetAll().Result.Any(u => u.Username.Equals(user.Username, StringComparison.OrdinalIgnoreCase) && u.Id != user.Id))
    {
        error = "Username is already in use by another user.";
        return false;
    }

    // Validate if the email is already in use by another user
    if (userService.GetAll().Result.Any(u => u.Email.Equals(user.Email, StringComparison.OrdinalIgnoreCase) && u.Id != user.Id))
    {
        error = "Email is already in use by another user.";
        return false;
    }
    return true;
}

// defines a data transfer object (DTO) for login request
public record LoginRequest(string username, string password);

