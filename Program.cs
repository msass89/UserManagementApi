using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using UserManagementApi;
using UserManagementApi.Middleware;
using UserManagementApi.Options;

/* Secret key and issuer for JWT (store securely in production)
Instead of hardcoding these values, consider using a secure configuration source 
such as environment variables, configuration files or a secret manager. */
string jwtKey = "super_secret_jwt_key_12345_67890_abcde";
string jwtIssuer = "UserManagementApi";

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddSingleton<UserService>();
builder.Services.Configure<JwtOptions>(options =>
{
    options.Key = jwtKey;
    options.Issuer = jwtIssuer;
});

var app = builder.Build();

// Use custom exception handling middleware
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseMiddleware<JWTAuthentificationMiddleware>();

app.UseMiddleware<LoggingMiddleware>();

/* Middleware to enforce HTTPS redirection for all incoming requests to enhance security 
 by ensuring that all communication between the client and server is encrypted.*/
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
    var validationResult = await UserService.IsValidUser(user, userService);
    if (!validationResult.IsValid)
        return Results.BadRequest(new { error = validationResult.ErrorMessage });

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
    var validationResult = await UserService.IsValidUser(updatedUser, userService);
    if (!validationResult.IsValid)
        return Results.BadRequest(new { error = validationResult.ErrorMessage });

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

// defines a data transfer object (DTO) for login request.
public record LoginRequest(string username, string password);

public record UserValidationResult(bool IsValid, string ErrorMessage);

//public record JWTOptions(string Key, string Issuer);

