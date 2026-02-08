using System.Collections.Generic;

namespace UserManagementApi
{
    public class UserService
    {
        private readonly Dictionary<int, User> _users = new();
        public IEnumerable<User> GetAll() => _users.Values;
        public User? GetById(int id) => _users.TryGetValue(id, out var user) ? user : null;
        public void Add(User user)
        {
            int newId = _users.Count == 0 ? 1 : _users.Keys.Max() + 1;
            user.Id = newId;
            _users.Add(newId, user);
        }
        public bool Update(int id, User updatedUser)
        {
            if (_users.TryGetValue(id, out var user))
            {
                user.Username = updatedUser.Username;
                user.Email = updatedUser.Email;
                return true;
            }
            return false;
        }

        public bool Delete(int id)
        {
            return _users.Remove(id);
        }
    }
}