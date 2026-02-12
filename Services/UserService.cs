using System.Collections.Concurrent;

namespace UserManagementApi
{
    public class UserService
    {
        // Use a thread-safe collection to store users
        private readonly ConcurrentDictionary<int, User> _users = new();
        public Task<IEnumerable<User>> GetAll() => Task.FromResult((IEnumerable<User>)_users.Values);
        public Task<User?> GetById(int id) => Task.FromResult(_users.TryGetValue(id, out var user) ? user : null);

        private int _nextId = 0; // To generate unique IDs for users

        public Task<bool> Add(User user)
        {
            // Generate a new unique ID for the user in a thread-safe manner
            int newId = Interlocked.Increment(ref _nextId);
            user.Id = newId;
            return Task.FromResult(_users.TryAdd(newId, user));
        }

        public Task<bool> Update(int id, User updatedUser)
        {
            if (_users.TryGetValue(id, out var user))
            {
                user.Username = updatedUser.Username;
                user.Email = updatedUser.Email;
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }

        public Task<bool> Delete(int id)
        {
            // TryRemove returns true if the user was successfully removed, false if the user was not found
            return Task.FromResult(_users.TryRemove(id, out _));
        }

        // Helper method to validate user data before creating or updating a user
        public static async Task<UserValidationResult> IsValidUser(UserManagementApi.User user, UserManagementApi.UserService userService)
        {
            // valide if username is not empty, between 3 and 30 characters, and only contains letters and numbers
            string error = string.Empty;
            if (string.IsNullOrWhiteSpace(user.Username) || user.Username.Length < 3 || user.Username.Length > 30 || !System.Text.RegularExpressions.Regex.IsMatch(user.Username, @"^[a-zA-Z0-9]+$"))
            {
                error = "Username is required, should be between 3 and 30 characters and only contain letters and numbers.";
                return new UserValidationResult(false, error);
            }

            // Validate email is not empty, less than 254 characters, and in a valid format
            if (string.IsNullOrWhiteSpace(user.Email) || user.Email.Length > 254) 
            {
                error = "Email is required and should be less than 254 characters.";
                return new UserValidationResult(false, error);
            }
            try
            {
                // Validate email format 
                var addr = new System.Net.Mail.MailAddress(user.Email);
                if (addr.Address != user.Email)
                {
                    error = "Invalid email format.";
                    return new UserValidationResult(false, error);
                }
            }
            catch
            {
                error = "Invalid email format.";
                return new UserValidationResult(false, error);
            }

            // Validate if the username is already in use by another user
            if ((await userService.GetAll()).Any(u => u.Username.Equals(user.Username, StringComparison.OrdinalIgnoreCase) && u.Id != user.Id))
            {
                error = "Username is already in use by another user.";
                return new UserValidationResult(false, error);
            }

            // Validate if the email is already in use by another user
            if ((await userService.GetAll()).Any(u => u.Email.Equals(user.Email, StringComparison.OrdinalIgnoreCase) && u.Id != user.Id))
            {
                error = "Email is already in use by another user.";
                return new UserValidationResult(false, error);
            }
            return new UserValidationResult(true, string.Empty);
        }
    }
}