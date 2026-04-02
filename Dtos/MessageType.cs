namespace WebSocketServer.Dtos
{
    public enum MessageType
    {
        // Basic messaging
        Text = 0,
        
        // User presence
        Join = 1,
        Leave = 2,
        UserList = 3,
        
        // Typing indicators
        Typing = 4,
        TypingStop = 5,
        
        // Receipts
        Delivered = 6,
        Read = 7,
        
        // Room operations
        RoomJoin = 8,
        RoomLeave = 9,
        RoomList = 10,
        RoomUsers = 11,
        RoomMessage = 12,
        RoomCreate = 13,
        RoomExists = 14,
        
        // History
        History = 15,
        
        // System
        SetUsername = 16,
        Error = 17,
        Welcome = 18
    }
}
