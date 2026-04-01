using System.Net.WebSockets;

namespace WebSocketServer.Models
{
    public class User
    {
        public string ConnectionId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public WebSocket Socket { get; set; } = null!;
        public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;
        public HashSet<string> JoinedRooms { get; set; } = new(StringComparer.OrdinalIgnoreCase) { "general" };
        public bool IsTyping { get; set; }
        public DateTime? LastTypingAt { get; set; }
    }
}
