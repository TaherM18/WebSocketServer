using System.Net.WebSockets;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WebSocketServer.Dtos;
using WebSocketServer.Models;
using WebSocketServer.Services;

namespace WebSocketServer.Middlewares
{
    public class WebSocketServerMiddleware: IMiddleware
    {
        private readonly WebSockerServerConnectionManager _connectionManager;
        private readonly ChatRoomService _roomService;
        private readonly MessageHistoryService _historyService;

        public WebSocketServerMiddleware(
            WebSockerServerConnectionManager connectionManager,
            ChatRoomService roomService,
            MessageHistoryService historyService)
        {
            _connectionManager = connectionManager;
            _roomService = roomService;
            _historyService = historyService;
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            if (context.WebSockets.IsWebSocketRequest)
            {
                WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();
                Console.WriteLine("WebSocket Connected!");

                var user = _connectionManager.AddUser(webSocket);
                
                // Join default room
                _roomService.JoinRoom("general", user.ConnectionId);
                user.JoinedRooms.Add("general");

                // Send welcome message
                await SendWelcomeAsync(user);
                
                // Send online users list
                await SendUserListAsync(user);
                
                // Broadcast join notification
                await BroadcastUserJoinedAsync(user);
                
                // Send message history for general room
                await SendRoomHistoryAsync(user, "general");
                
                // Send initial room list (will include general)
                await SendRoomListAsync(user);

                await ReceiveMessageAsync(webSocket, async (result, bytes) =>
                {
                    switch (result.MessageType)
                    {
                        case WebSocketMessageType.Text:
                            string rawMessage = Encoding.UTF8.GetString(bytes, 0, result.Count);
                            Console.WriteLine($"Received: {rawMessage}");
                            await HandleMessageAsync(user, rawMessage);
                            break;
                        case WebSocketMessageType.Binary:
                            break;
                        case WebSocketMessageType.Close:
                            await HandleDisconnectAsync(user, webSocket);
                            break;
                    }
                });
            }
            else
            {
                await next(context);
            }
        }

        private async Task HandleMessageAsync(User user, string rawMessage)
        {
            try
            {
                var json = JObject.Parse(rawMessage);
                var messageType = json["Type"]?.ToObject<MessageType>() ?? json["type"]?.ToObject<MessageType>();

                switch (messageType)
                {
                    case MessageType.Text:
                        await HandleTextMessageAsync(user, json);
                        break;
                    case MessageType.SetUsername:
                        await HandleSetUsernameAsync(user, json);
                        break;
                    case MessageType.Typing:
                        await HandleTypingAsync(user, json);
                        break;
                    case MessageType.TypingStop:
                        await HandleTypingStopAsync(user, json);
                        break;
                    case MessageType.RoomCreate:
                        await HandleRoomCreateAsync(user, json);
                        break;
                    case MessageType.RoomJoin:
                        await HandleRoomJoinAsync(user, json);
                        break;
                    case MessageType.RoomLeave:
                        await HandleRoomLeaveAsync(user, json);
                        break;
                    case MessageType.RoomList:
                        await SendRoomListAsync(user);
                        break;
                    case MessageType.RoomExists:
                        await HandleRoomExistsAsync(user, json);
                        break;
                    case MessageType.UserList:
                        await SendUserListAsync(user);
                        break;
                    case MessageType.Read:
                        await HandleReadReceiptAsync(user, json);
                        break;
                    default:
                        // Try legacy message format for backward compatibility
                        await HandleLegacyMessageAsync(user, rawMessage);
                        break;
                }
            }
            catch (JsonException)
            {
                // Try legacy format
                await HandleLegacyMessageAsync(user, rawMessage);
            }
        }

        private async Task HandleTextMessageAsync(User user, JObject json)
        {
            var content = json["Content"]?.ToString() ?? json["content"]?.ToString() ?? "";
            var to = json["To"]?.ToString() ?? json["to"]?.ToString();
            var roomName = json["RoomName"]?.ToString() ?? json["roomName"]?.ToString() ?? "general";

            var textMessage = new TextMessage
            {
                From = user.ConnectionId,
                Content = content,
                RoomName = roomName,
                Timestamp = DateTime.UtcNow
            };

            // Store in history
            _historyService.AddMessage(roomName, textMessage);

            // Add sender's username for display
            var messageWithUsername = new
            {
                textMessage.Type,
                textMessage.MessageId,
                textMessage.From,
                FromUsername = user.Username,
                textMessage.To,
                textMessage.Content,
                textMessage.RoomName,
                textMessage.Timestamp
            };

            string jsonString = JsonConvert.SerializeObject(messageWithUsername);

            if (!string.IsNullOrEmpty(to) && Guid.TryParse(to, out _))
            {
                // Direct message
                var targetUser = _connectionManager.GetUser(to);
                if (targetUser != null && targetUser.Socket.State == WebSocketState.Open)
                {
                    await SendAsync(targetUser.Socket, jsonString);
                    // Send delivery receipt
                    await SendDeliveryReceiptAsync(user, textMessage.MessageId!);
                }
            }
            else
            {
                // Room message - send to all users in room except sender
                var roomUsers = _connectionManager.GetUsersInRoom(roomName);
                foreach (var roomUser in roomUsers.Where(u => u.ConnectionId != user.ConnectionId))
                {
                    if (roomUser.Socket.State == WebSocketState.Open)
                    {
                        await SendAsync(roomUser.Socket, jsonString);
                    }
                }
                // Send delivery receipt to sender
                await SendDeliveryReceiptAsync(user, textMessage.MessageId!);
            }

            // Clear typing indicator
            _connectionManager.SetTyping(user.ConnectionId, false);
        }

        private async Task HandleSetUsernameAsync(User user, JObject json)
        {
            var newUsername = json["Username"]?.ToString() ?? json["username"]?.ToString();
            
            if (string.IsNullOrEmpty(newUsername))
            {
                await SendErrorAsync(user, "Username cannot be empty");
                return;
            }

            var oldUsername = user.Username;
            if (_connectionManager.SetUsername(user.ConnectionId, newUsername))
            {
                // Send confirmation
                var welcome = new WelcomeMessage
                {
                    ConnectionId = user.ConnectionId,
                    Username = newUsername
                };
                await SendAsync(user.Socket, JsonConvert.SerializeObject(welcome));

                // Broadcast updated user list
                await BroadcastUserListAsync();

                // Notify rooms about username change
                foreach (var roomName in user.JoinedRooms)
                {
                    var notification = new
                    {
                        Type = MessageType.Text,
                        From = "System",
                        FromUsername = "System",
                        Content = $"{oldUsername} is now known as {newUsername}",
                        RoomName = roomName,
                        Timestamp = DateTime.UtcNow
                    };
                    await BroadcastToRoomAsync(roomName, JsonConvert.SerializeObject(notification), null);
                }
            }
            else
            {
                await SendErrorAsync(user, "Username is already taken or invalid (max 20 characters)");
            }
        }

        private async Task HandleTypingAsync(User user, JObject json)
        {
            var roomName = json["RoomName"]?.ToString() ?? json["roomName"]?.ToString() ?? "general";
            _connectionManager.SetTyping(user.ConnectionId, true);

            var typingMessage = new
            {
                Type = MessageType.Typing,
                From = user.ConnectionId,
                FromUsername = user.Username,
                RoomName = roomName,
                IsTyping = true,
                Timestamp = DateTime.UtcNow
            };

            await BroadcastToRoomAsync(roomName, JsonConvert.SerializeObject(typingMessage), user.ConnectionId);
        }

        private async Task HandleTypingStopAsync(User user, JObject json)
        {
            var roomName = json["RoomName"]?.ToString() ?? json["roomName"]?.ToString() ?? "general";
            _connectionManager.SetTyping(user.ConnectionId, false);

            var typingMessage = new
            {
                Type = MessageType.TypingStop,
                From = user.ConnectionId,
                FromUsername = user.Username,
                RoomName = roomName,
                IsTyping = false,
                Timestamp = DateTime.UtcNow
            };

            await BroadcastToRoomAsync(roomName, JsonConvert.SerializeObject(typingMessage), user.ConnectionId);
        }

        private async Task HandleRoomCreateAsync(User user, JObject json)
        {
            var roomName = json["RoomName"]?.ToString() ?? json["roomName"]?.ToString();
            
            if (string.IsNullOrEmpty(roomName))
            {
                await SendErrorAsync(user, "Room name is required");
                return;
            }

            // Validate room name
            if (roomName.Length > 50 || roomName.Contains('_'))
            {
                await SendErrorAsync(user, "Invalid room name. Max 50 characters, no underscores allowed.");
                return;
            }

            // Check if room already exists
            if (_roomService.RoomExists(roomName))
            {
                await SendErrorAsync(user, $"Room '{roomName}' already exists");
                return;
            }

            // Create the room with creator as first member
            var room = _roomService.CreateRoom(roomName, user.ConnectionId, isPrivate: true);
            if (room == null)
            {
                await SendErrorAsync(user, $"Failed to create room '{roomName}'");
                return;
            }

            user.JoinedRooms.Add(roomName.ToLowerInvariant());

            // Send confirmation
            var createConfirmation = new
            {
                Type = MessageType.RoomCreate,
                RoomName = room.Name,
                Success = true,
                Timestamp = DateTime.UtcNow
            };
            await SendAsync(user.Socket, JsonConvert.SerializeObject(createConfirmation));

            // Send updated room list
            await SendRoomListAsync(user);
        }

        private async Task HandleRoomExistsAsync(User user, JObject json)
        {
            var roomName = json["RoomName"]?.ToString() ?? json["roomName"]?.ToString();
            
            var exists = !string.IsNullOrEmpty(roomName) && _roomService.RoomExists(roomName);
            
            var response = new
            {
                Type = MessageType.RoomExists,
                RoomName = roomName,
                Exists = exists,
                Timestamp = DateTime.UtcNow
            };
            await SendAsync(user.Socket, JsonConvert.SerializeObject(response));
        }

        private async Task HandleRoomJoinAsync(User user, JObject json)
        {
            var roomName = json["RoomName"]?.ToString() ?? json["roomName"]?.ToString();
            var targetUserId = json["TargetUserId"]?.ToString() ?? json["targetUserId"]?.ToString();
            
            if (string.IsNullOrEmpty(roomName) && string.IsNullOrEmpty(targetUserId))
            {
                await SendErrorAsync(user, "Room name or target user is required");
                return;
            }

            bool isDm = false;
            
            // If targetUserId is provided, this is a DM chat request
            if (!string.IsNullOrEmpty(targetUserId))
            {
                roomName = ChatRoomService.GetPrivateRoomId(user.ConnectionId, targetUserId);
                var room = _roomService.GetOrCreateDmRoom(user.ConnectionId, targetUserId);
                isDm = true;
                
                // Also add the other user to the room if they're online
                var targetUser = _connectionManager.GetUser(targetUserId);
                if (targetUser != null)
                {
                    _roomService.JoinRoom(roomName, targetUserId);
                    targetUser.JoinedRooms.Add(roomName);
                }
            }
            else
            {
                // For regular rooms, check if it exists first
                if (!_roomService.RoomExists(roomName))
                {
                    await SendErrorAsync(user, $"Room '{roomName}' does not exist");
                    return;
                }
            }

            // Join the room
            bool? joinResult = _roomService.JoinRoom(roomName!, user.ConnectionId);
            if (joinResult == null)
            {
                // Room doesn't exist (shouldn't happen for DM rooms)
                await SendErrorAsync(user, $"Failed to join room '{roomName}'");
                return;
            }
            
            user.JoinedRooms.Add(roomName!);
            isDm = isDm || ChatRoomService.IsDmRoom(roomName!);
            
            // For DM rooms, include the other user's username
            string? otherUsername = null;
            if (isDm)
            {
                var otherUserId = ChatRoomService.GetOtherUserInDmRoom(roomName!, user.ConnectionId);
                if (otherUserId != null)
                {
                    var otherUser = _connectionManager.GetUser(otherUserId);
                    otherUsername = otherUser?.Username;
                }
            }

            // Notify user
            var joinConfirmation = new
            {
                Type = MessageType.RoomJoin,
                RoomName = roomName,
                IsDm = isDm,
                OtherUsername = otherUsername,
                Users = _connectionManager.GetUsersInRoom(roomName!).Select(u => u.Username).ToList(),
                Timestamp = DateTime.UtcNow
            };
            await SendAsync(user.Socket, JsonConvert.SerializeObject(joinConfirmation));

            // Send room history
            await SendRoomHistoryAsync(user, roomName!);

            // Only broadcast join notification for non-DM rooms
            if (!isDm)
            {
                var notification = new
                {
                    Type = MessageType.Text,
                    From = "System",
                    FromUsername = "System",
                    Content = $"{user.Username} joined the room",
                    RoomName = roomName,
                    Timestamp = DateTime.UtcNow
                };
                await BroadcastToRoomAsync(roomName!, JsonConvert.SerializeObject(notification), user.ConnectionId);
            }
            
            // Send updated room list
            await SendRoomListAsync(user);
        }

        private async Task HandleRoomLeaveAsync(User user, JObject json)
        {
            var roomName = json["RoomName"]?.ToString() ?? json["roomName"]?.ToString();
            
            if (string.IsNullOrEmpty(roomName) || roomName.Equals("general", StringComparison.OrdinalIgnoreCase))
            {
                await SendErrorAsync(user, "Cannot leave the general room");
                return;
            }

            var isDm = ChatRoomService.IsDmRoom(roomName);
            
            user.JoinedRooms.Remove(roomName);
            _roomService.LeaveRoom(roomName, user.ConnectionId);

            // Notify user
            var leaveConfirmation = new RoomMessage(MessageType.RoomLeave) { RoomName = roomName };
            await SendAsync(user.Socket, JsonConvert.SerializeObject(leaveConfirmation));

            // Only broadcast leave notification for non-DM rooms
            if (!isDm)
            {
                var notification = new
                {
                    Type = MessageType.Text,
                    From = "System",
                    FromUsername = "System",
                    Content = $"{user.Username} left the room",
                    RoomName = roomName,
                    Timestamp = DateTime.UtcNow
                };
                await BroadcastToRoomAsync(roomName, JsonConvert.SerializeObject(notification), null);
            }
            
            // Send updated room list
            await SendRoomListAsync(user);
        }

        private async Task HandleReadReceiptAsync(User user, JObject json)
        {
            var originalMessageId = json["OriginalMessageId"]?.ToString() ?? json["originalMessageId"]?.ToString();
            var to = json["To"]?.ToString() ?? json["to"]?.ToString();

            if (string.IsNullOrEmpty(originalMessageId) || string.IsNullOrEmpty(to))
                return;

            var targetUser = _connectionManager.GetUser(to);
            if (targetUser != null && targetUser.Socket.State == WebSocketState.Open)
            {
                var receipt = new
                {
                    Type = MessageType.Read,
                    OriginalMessageId = originalMessageId,
                    From = user.ConnectionId,
                    FromUsername = user.Username,
                    Timestamp = DateTime.UtcNow
                };
                await SendAsync(targetUser.Socket, JsonConvert.SerializeObject(receipt));
            }
        }

        private async Task HandleLegacyMessageAsync(User user, string rawMessage)
        {
            // Support legacy MessageDto format
            var legacyMessage = JsonConvert.DeserializeObject<MessageDto>(rawMessage);
            if (legacyMessage == null)
            {
                Console.WriteLine("Invalid message format");
                return;
            }

            var textMessage = new TextMessage
            {
                From = user.ConnectionId,
                To = legacyMessage.to,
                Content = legacyMessage.message,
                RoomName = "general",
                Timestamp = DateTime.UtcNow
            };

            var messageWithUsername = new
            {
                textMessage.Type,
                textMessage.MessageId,
                textMessage.From,
                FromUsername = user.Username,
                textMessage.To,
                textMessage.Content,
                textMessage.RoomName,
                textMessage.Timestamp
            };

            string jsonString = JsonConvert.SerializeObject(messageWithUsername);

            if (Guid.TryParse(legacyMessage.to, out _))
            {
                var targetUser = _connectionManager.GetUser(legacyMessage.to);
                if (targetUser != null && targetUser.Socket.State == WebSocketState.Open)
                {
                    await SendAsync(targetUser.Socket, jsonString);
                }
            }
            else
            {
                // Broadcast
                foreach (var connUser in _connectionManager.Users.Values)
                {
                    if (connUser.ConnectionId != user.ConnectionId && connUser.Socket.State == WebSocketState.Open)
                    {
                        await SendAsync(connUser.Socket, jsonString);
                    }
                }
            }
        }

        private async Task HandleDisconnectAsync(User user, WebSocket webSocket)
        {
            Console.WriteLine($"User disconnecting: {user.Username}");
            
            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Connection closed", CancellationToken.None);
            
            _roomService.LeaveAllRooms(user.ConnectionId);
            _connectionManager.RemoveUser(user.ConnectionId);

            // Broadcast user left
            await BroadcastUserLeftAsync(user);
        }

        private async Task SendWelcomeAsync(User user)
        {
            var welcome = new WelcomeMessage
            {
                ConnectionId = user.ConnectionId,
                Username = user.Username
            };
            await SendAsync(user.Socket, JsonConvert.SerializeObject(welcome));
        }

        private async Task SendUserListAsync(User user)
        {
            var userList = new UserListMessage
            {
                Users = _connectionManager.GetOnlineUsers()
            };
            await SendAsync(user.Socket, JsonConvert.SerializeObject(userList));
        }

        private async Task BroadcastUserListAsync()
        {
            var userList = new UserListMessage
            {
                Users = _connectionManager.GetOnlineUsers()
            };
            string json = JsonConvert.SerializeObject(userList);

            foreach (var user in _connectionManager.Users.Values)
            {
                if (user.Socket.State == WebSocketState.Open)
                {
                    await SendAsync(user.Socket, json);
                }
            }
        }

        private async Task BroadcastUserJoinedAsync(User joinedUser)
        {
            var notification = new UserPresenceMessage(MessageType.Join)
            {
                ConnectionId = joinedUser.ConnectionId,
                Username = joinedUser.Username
            };
            string json = JsonConvert.SerializeObject(notification);

            foreach (var user in _connectionManager.Users.Values)
            {
                if (user.ConnectionId != joinedUser.ConnectionId && user.Socket.State == WebSocketState.Open)
                {
                    await SendAsync(user.Socket, json);
                }
            }
        }

        private async Task BroadcastUserLeftAsync(User leftUser)
        {
            var notification = new UserPresenceMessage(MessageType.Leave)
            {
                ConnectionId = leftUser.ConnectionId,
                Username = leftUser.Username
            };
            string json = JsonConvert.SerializeObject(notification);

            foreach (var user in _connectionManager.Users.Values)
            {
                if (user.Socket.State == WebSocketState.Open)
                {
                    await SendAsync(user.Socket, json);
                }
            }

            // Also broadcast updated user list
            await BroadcastUserListAsync();
        }

        private async Task SendRoomHistoryAsync(User user, string roomName)
        {
            var messages = _historyService.GetRoomHistory(roomName);
            
            // Enrich messages with usernames
            var enrichedMessages = messages.Select(m =>
            {
                var fromUser = _connectionManager.GetUser(m.From ?? "");
                return new
                {
                    m.Type,
                    m.MessageId,
                    m.From,
                    FromUsername = fromUser?.Username ?? "Unknown",
                    m.To,
                    m.Content,
                    m.RoomName,
                    m.Timestamp
                };
            }).ToList();

            var historyMessage = new
            {
                Type = MessageType.History,
                RoomName = roomName,
                Messages = enrichedMessages,
                Timestamp = DateTime.UtcNow
            };

            await SendAsync(user.Socket, JsonConvert.SerializeObject(historyMessage));
        }

        private async Task SendRoomListAsync(User user)
        {
            // Only return rooms the user is a member of (excluding DM rooms)
            var rooms = _roomService.GetUserRooms(user.ConnectionId).Select(r => new RoomInfo
            {
                Name = r.Name,
                UserCount = r.MemberConnectionIds.Count
            }).ToList();

            var roomList = new RoomListMessage { Rooms = rooms };
            await SendAsync(user.Socket, JsonConvert.SerializeObject(roomList));
        }

        private async Task SendDeliveryReceiptAsync(User user, string messageId)
        {
            var receipt = new
            {
                Type = MessageType.Delivered,
                OriginalMessageId = messageId,
                Timestamp = DateTime.UtcNow
            };
            await SendAsync(user.Socket, JsonConvert.SerializeObject(receipt));
        }

        private async Task SendErrorAsync(User user, string error)
        {
            var errorMessage = new ErrorMessage { Error = error };
            await SendAsync(user.Socket, JsonConvert.SerializeObject(errorMessage));
        }

        private async Task BroadcastToRoomAsync(string roomName, string message, string? excludeConnectionId)
        {
            var roomUsers = _connectionManager.GetUsersInRoom(roomName);
            foreach (var user in roomUsers)
            {
                if (user.ConnectionId != excludeConnectionId && user.Socket.State == WebSocketState.Open)
                {
                    await SendAsync(user.Socket, message);
                }
            }
        }

        private async Task SendAsync(WebSocket socket, string message)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(message);
            await socket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
        }

        private async Task ReceiveMessageAsync(WebSocket socket, Action<WebSocketReceiveResult, byte[]> handleMessage)
        {
            var buffer = new byte[1024 * 4];

            while (socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(
                    buffer: new ArraySegment<byte>(buffer),
                    cancellationToken: CancellationToken.None
                );
                handleMessage(result, buffer);
            }
        }
    }
}