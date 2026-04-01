using System.Collections.Concurrent;
using WebSocketServer.Dtos;

namespace WebSocketServer.Services
{
    public class MessageHistoryService
    {
        private readonly ConcurrentDictionary<string, List<TextMessage>> _roomMessages = new(StringComparer.OrdinalIgnoreCase);
        private readonly int _maxMessagesPerRoom;
        private readonly object _lock = new();

        public MessageHistoryService(int maxMessagesPerRoom = 100)
        {
            _maxMessagesPerRoom = maxMessagesPerRoom;
        }

        public void AddMessage(string roomName, TextMessage message)
        {
            var messages = _roomMessages.GetOrAdd(roomName.ToLowerInvariant(), _ => new List<TextMessage>());
            
            lock (_lock)
            {
                messages.Add(message);
                if (messages.Count > _maxMessagesPerRoom)
                {
                    messages.RemoveAt(0);
                }
            }
        }

        public List<TextMessage> GetRoomHistory(string roomName, int count = 50)
        {
            if (_roomMessages.TryGetValue(roomName, out var messages))
            {
                lock (_lock)
                {
                    return messages.TakeLast(count).ToList();
                }
            }
            return new List<TextMessage>();
        }

        public void ClearRoomHistory(string roomName)
        {
            if (_roomMessages.TryGetValue(roomName, out var messages))
            {
                lock (_lock)
                {
                    messages.Clear();
                }
            }
        }
    }
}
