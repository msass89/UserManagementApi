var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddSingleton<UserManagementApi.UserService>();
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

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

app.Run();

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

