namespace WebSocketServer.Dtos
{
    public class BaseMessage
    {
        public MessageType Type { get; set; }
        public string? From { get; set; }
        public string? To { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string? MessageId { get; set; }
    }

    public class TextMessage : BaseMessage
    {
        public string Content { get; set; } = string.Empty;
        public string? RoomName { get; set; }

        public TextMessage()
        {
            Type = MessageType.Text;
            MessageId = Guid.NewGuid().ToString();
        }
    }

    public class TypingMessage : BaseMessage
    {
        public string? RoomName { get; set; }
        public bool IsTyping { get; set; }

        public TypingMessage()
        {
            Type = MessageType.Typing;
        }
    }

    public class UserPresenceMessage : BaseMessage
    {
        public string? Username { get; set; }
        public string? ConnectionId { get; set; }

        public UserPresenceMessage(MessageType type)
        {
            Type = type;
        }
    }

    public class UserListMessage : BaseMessage
    {
        public List<UserInfo> Users { get; set; } = new();

        public UserListMessage()
        {
            Type = MessageType.UserList;
        }
    }

    public class UserInfo
    {
        public string ConnectionId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public DateTime JoinedAt { get; set; }
        public List<string> Rooms { get; set; } = new();
    }

    public class ReceiptMessage : BaseMessage
    {
        public string OriginalMessageId { get; set; } = string.Empty;

        public ReceiptMessage(MessageType type)
        {
            Type = type;
        }
    }

    public class RoomMessage : BaseMessage
    {
        public string RoomName { get; set; } = string.Empty;
        public List<string>? Users { get; set; }

        public RoomMessage(MessageType type)
        {
            Type = type;
        }
    }

    public class RoomListMessage : BaseMessage
    {
        public List<RoomInfo> Rooms { get; set; } = new();

        public RoomListMessage()
        {
            Type = MessageType.RoomList;
        }
    }

    public class RoomInfo
    {
        public string Name { get; set; } = string.Empty;
        public int UserCount { get; set; }
    }

    public class HistoryMessage : BaseMessage
    {
        public string RoomName { get; set; } = string.Empty;
        public List<TextMessage> Messages { get; set; } = new();

        public HistoryMessage()
        {
            Type = MessageType.History;
        }
    }

    public class SetUsernameMessage : BaseMessage
    {
        public string Username { get; set; } = string.Empty;

        public SetUsernameMessage()
        {
            Type = MessageType.SetUsername;
        }
    }

    public class WelcomeMessage : BaseMessage
    {
        public string ConnectionId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;

        public WelcomeMessage()
        {
            Type = MessageType.Welcome;
        }
    }

    public class ErrorMessage : BaseMessage
    {
        public string Error { get; set; } = string.Empty;

        public ErrorMessage()
        {
            Type = MessageType.Error;
        }
    }
}
