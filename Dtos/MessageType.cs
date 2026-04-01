namespace WebSocketServer.Dtos
{
    public enum MessageType
    {
        // Basic messaging
        Text,
        
        // User presence
        Join,
        Leave,
        UserList,
        
        // Typing indicators
        Typing,
        TypingStop,
        
        // Receipts
        Delivered,
        Read,
        
        // Room operations
        RoomJoin,
        RoomLeave,
        RoomList,
        RoomUsers,
        RoomMessage,
        
        // History
        History,
        
        // System
        SetUsername,
        Error,
        Welcome
    }
}
