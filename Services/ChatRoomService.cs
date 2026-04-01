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
            _rooms.TryAdd("general", new ChatRoom { Name = "general" });
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

        public bool JoinRoom(string roomName, string connectionId)
        {
            var room = GetOrCreateRoom(roomName);
            return room.MemberConnectionIds.Add(connectionId);
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

        public IEnumerable<string> GetUserRooms(string connectionId)
        {
            return _rooms.Where(r => r.Value.MemberConnectionIds.Contains(connectionId))
                         .Select(r => r.Key)
                         .ToList();
        }
    }
}
