using System.Collections.Concurrent;
using WebSocketServer.Models;

namespace WebSocketServer.Services
{
    public class ChatRoomService
    {
        private readonly ConcurrentDictionary<string, ChatRoom> _rooms = new(StringComparer.OrdinalIgnoreCase);

        public ChatRoomService()
        {
            // Create default "general" room
            _rooms.TryAdd("general", new ChatRoom { Name = "general", IsPrivate = false });
        }

        /// <summary>
        /// Checks if a room with the given name exists
        /// </summary>
        public bool RoomExists(string roomName)
        {
            return _rooms.ContainsKey(roomName);
        }

        /// <summary>
        /// Creates a new room. Returns null if room already exists.
        /// </summary>
        public ChatRoom? CreateRoom(string roomName, string creatorConnectionId, bool isPrivate = false)
        {
            var room = new ChatRoom 
            { 
                Name = roomName.ToLowerInvariant(),
                IsPrivate = isPrivate
            };
            
            if (_rooms.TryAdd(roomName.ToLowerInvariant(), room))
            {
                room.MemberConnectionIds.Add(creatorConnectionId);
                return room;
            }
            return null; // Room already exists
        }

        public ChatRoom GetOrCreateRoom(string roomName)
        {
            return _rooms.GetOrAdd(roomName.ToLowerInvariant(), name => new ChatRoom { Name = name });
        }

        public ChatRoom? GetRoom(string roomName)
        {
            _rooms.TryGetValue(roomName, out var room);
            return room;
        }

        /// <summary>
        /// Creates a consistent private room ID from two connection IDs.
        /// The IDs are sorted to ensure the same room ID regardless of who initiates.
        /// </summary>
        public static string GetPrivateRoomId(string connectionId1, string connectionId2)
        {
            var ids = new[] { connectionId1, connectionId2 };
            Array.Sort(ids, StringComparer.Ordinal);
            return $"{ids[0]}_{ids[1]}";
        }

        /// <summary>
        /// Checks if a room ID is a DM room (contains two connection IDs separated by _)
        /// </summary>
        public static bool IsDmRoom(string roomName)
        {
            if (string.IsNullOrEmpty(roomName)) return false;
            var parts = roomName.Split('_');
            return parts.Length == 2 && Guid.TryParse(parts[0], out _) && Guid.TryParse(parts[1], out _);
        }

        /// <summary>
        /// Gets the other user's connection ID from a DM room ID
        /// </summary>
        public static string? GetOtherUserInDmRoom(string roomName, string currentConnectionId)
        {
            if (!IsDmRoom(roomName)) return null;
            var parts = roomName.Split('_');
            return parts[0] == currentConnectionId ? parts[1] : parts[0];
        }

        public ChatRoom GetOrCreateDmRoom(string connectionId1, string connectionId2)
        {
            var roomId = GetPrivateRoomId(connectionId1, connectionId2);
            return _rooms.GetOrAdd(roomId, name => new ChatRoom 
            { 
                Name = name, 
                IsPrivate = true 
            });
        }

        public bool? JoinRoom(string roomName, string connectionId)
        {
            if (_rooms.TryGetValue(roomName, out var room))
            {
                return room.MemberConnectionIds.Add(connectionId);
            }
            return null; // Room doesn't exist
        }

        public bool LeaveRoom(string roomName, string connectionId)
        {
            if (_rooms.TryGetValue(roomName, out var room))
            {
                return room.MemberConnectionIds.Remove(connectionId);
            }
            return false;
        }

        public void LeaveAllRooms(string connectionId)
        {
            foreach (var room in _rooms.Values)
            {
                room.MemberConnectionIds.Remove(connectionId);
            }
        }

        public IEnumerable<string> GetRoomMembers(string roomName)
        {
            if (_rooms.TryGetValue(roomName, out var room))
            {
                return room.MemberConnectionIds.ToList();
            }
            return Enumerable.Empty<string>();
        }

        public IEnumerable<ChatRoom> GetAllRooms()
        {
            return _rooms.Values.ToList();
        }

        /// <summary>
        /// Gets all rooms the user is a member of (excluding DM rooms)
        /// </summary>
        public IEnumerable<ChatRoom> GetUserRooms(string connectionId)
        {
            return _rooms.Values
                .Where(r => !IsDmRoom(r.Name) && r.MemberConnectionIds.Contains(connectionId))
                .ToList();
        }

        /// <summary>
        /// Gets all DM rooms for a user
        /// </summary>
        public IEnumerable<ChatRoom> GetUserDmRooms(string connectionId)
        {
            return _rooms.Values
                .Where(r => IsDmRoom(r.Name) && r.MemberConnectionIds.Contains(connectionId))
                .ToList();
        }
    }
}
