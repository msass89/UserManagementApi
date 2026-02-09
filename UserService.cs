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
    }
}