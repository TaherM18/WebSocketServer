using System.Collections.Concurrent;
using System.Net.WebSockets;
using WebSocketServer.Dtos;
using WebSocketServer.Models;

namespace WebSocketServer.Middlewares
{
    public class WebSockerServerConnectionManager
    {
        private readonly ConcurrentDictionary<string, User> _users = new();
        private int _usernameCounter = 0;

        public ConcurrentDictionary<string, User> Users => _users;

        public User AddUser(WebSocket webSocket)
        {
            var existingUser = _users.Values.FirstOrDefault(u => u.Socket.Equals(webSocket));
            if (existingUser != null)
            {
                Console.WriteLine($"Connection already exists: {existingUser.ConnectionId}");
                return existingUser;
            }

            string connectionId = Guid.NewGuid().ToString();
            var username = $"User{Interlocked.Increment(ref _usernameCounter)}";
            
            var user = new User
            {
                ConnectionId = connectionId,
                Username = username,
                Socket = webSocket,
                ConnectedAt = DateTime.UtcNow
            };

            _users.TryAdd(connectionId, user);
            Console.WriteLine($"User connected: {username} ({connectionId})");
            return user;
        }

        public bool RemoveUser(string connectionId)
        {
            if (_users.TryRemove(connectionId, out var user))
            {
                Console.WriteLine($"User disconnected: {user.Username} ({connectionId})");
                return true;
            }
            return false;
        }

        public User? GetUser(string connectionId)
        {
            _users.TryGetValue(connectionId, out var user);
            return user;
        }

        public User? GetUserBySocket(WebSocket socket)
        {
            return _users.Values.FirstOrDefault(u => u.Socket.Equals(socket));
        }

        public bool SetUsername(string connectionId, string newUsername)
        {
            if (string.IsNullOrWhiteSpace(newUsername) || newUsername.Length > 20)
                return false;

            // Check if username is taken
            if (_users.Values.Any(u => u.Username.Equals(newUsername, StringComparison.OrdinalIgnoreCase) 
                                       && u.ConnectionId != connectionId))
                return false;

            if (_users.TryGetValue(connectionId, out var user))
            {
                var oldUsername = user.Username;
                user.Username = newUsername;
                Console.WriteLine($"Username changed: {oldUsername} -> {newUsername}");
                return true;
            }
            return false;
        }

        public List<UserInfo> GetOnlineUsers()
        {
            return _users.Values.Select(u => new UserInfo
            {
                ConnectionId = u.ConnectionId,
                Username = u.Username,
                JoinedAt = u.ConnectedAt,
                Rooms = u.JoinedRooms.ToList()
            }).ToList();
        }

        public List<User> GetUsersInRoom(string roomName)
        {
            return _users.Values
                .Where(u => u.JoinedRooms.Contains(roomName))
                .ToList();
        }

        public void SetTyping(string connectionId, bool isTyping)
        {
            if (_users.TryGetValue(connectionId, out var user))
            {
                user.IsTyping = isTyping;
                user.LastTypingAt = isTyping ? DateTime.UtcNow : null;
            }
        }

        // Legacy property for backward compatibility
        public ConcurrentDictionary<string, WebSocket> SocketDictionary => 
            new ConcurrentDictionary<string, WebSocket>(_users.ToDictionary(u => u.Key, u => u.Value.Socket));
    }
}