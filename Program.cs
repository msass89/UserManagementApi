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
app.MapGet("/user", (UserManagementApi.UserService userService) =>
{
    return Results.Ok(userService.GetAll());
});

// Get user by id
app.MapGet("/user/{id}", (int id, UserManagementApi.UserService userService) =>
{
    var user = userService.GetById(id);
    if (user == null)
        return Results.NotFound();
    return Results.Ok(user);
});

// Create a new user
app.MapPost("/user", (UserManagementApi.User user, UserManagementApi.UserService userService) =>
{
    userService.Add(user);
    return Results.Created($"/user/{user.Id}", user);
});

// Update an existing user
app.MapPut("/user/{id}", (int id, UserManagementApi.User updatedUser, UserManagementApi.UserService userService) =>
{
    var success = userService.Update(id, updatedUser);
    if (!success)
        return Results.NotFound();
    return Results.Ok("Succesfully updated: " + updatedUser.Username + ", Email: " + updatedUser.Email);
});

// Delete a user
app.MapDelete("/user/{id}", (int id, UserManagementApi.UserService userService) =>
{
    var success = userService.Delete(id);
    if (!success)
        return Results.NotFound();
    return Results.Ok("Succesfully deleted user with ID: " + id);
});

app.Run();

