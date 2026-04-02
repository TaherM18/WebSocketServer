using WebSocketServer.Dtos;

namespace WebSocketServer.Models
{
    public class ChatRoom
    {
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public HashSet<string> MemberConnectionIds { get; set; } = new();
        public List<TextMessage> MessageHistory { get; set; } = new();
        public int MaxHistorySize { get; set; } = 50;
        public bool IsPrivate { get; set; } = false;

        public void AddMessage(TextMessage message)
        {
            MessageHistory.Add(message);
            if (MessageHistory.Count > MaxHistorySize)
            {
                MessageHistory.RemoveAt(0);
            }
        }
    }
}
