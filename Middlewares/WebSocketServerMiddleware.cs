using System.Net.WebSockets;
using System.Text;
using Newtonsoft.Json;
using WebSocketServer.Dtos;

namespace WebSocketServer.Middlewares
{
    public class WebSocketServerMiddleware: IMiddleware
    {
        private readonly WebSockerServerConnectionManager _wssConnManager;

        public WebSocketServerMiddleware(WebSockerServerConnectionManager wssConnManager)
        {
            _wssConnManager = wssConnManager;
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            if (context.WebSockets.IsWebSocketRequest)
            {
                WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();
                Console.WriteLine("WebSocket Connected!");

                string connID = _wssConnManager.AddSocket(webSocket);

                await SendConnIdAsync(webSocket, connID);

                await ReceiveMessageAsync(webSocket, async (result, bytes) =>
                {
                    switch (result.MessageType)
                    {
                        case WebSocketMessageType.Text:
                            string rawMessage = Encoding.UTF8.GetString(bytes, 0, result.Count);
                            Console.WriteLine($"Received text message: {rawMessage}");
                            await RouteJsonMessageAsync(rawMessage);
                            break;
                        case WebSocketMessageType.Binary:
                            break;
                        case WebSocketMessageType.Close:
                            Console.WriteLine("Received close message");
                            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Connection closed by client", CancellationToken.None);
                            foreach (var (connID, socket) in _wssConnManager.SocketDictionary)
                            {
                                if (socket.Equals(webSocket))
                                {
                                    if(_wssConnManager.SocketDictionary.Remove(connID, out WebSocket? removedSocket))
                                    {
                                        Console.WriteLine($"ConnId: {connID} removed");
                                    }
                                }
                            }
                            break;
                    }
                });
            }
            else
            {
                await next(context);
            }
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

        private async Task SendConnIdAsync(WebSocket socket, string connID)
        {
            var messageDto = new MessageDto("Server", "Client (You)", $"ConnID: {connID}");
            string jsonString = JsonConvert.SerializeObject(messageDto);
            byte[] buffer = Encoding.UTF8.GetBytes(jsonString);
            await socket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
        }

        private async Task RouteJsonMessageAsync(string message)
        {
            MessageDto? messageDto = JsonConvert.DeserializeObject<MessageDto>(message);
            if (messageDto is null)
            {
                Console.WriteLine("Invalid message format. Expected { From: string, To: string, Message: string }");
                return;
            }
            if (Guid.TryParse(messageDto.to, out Guid guidTo))
            {
                // To particular client
                Console.WriteLine($"To particular client");
                var (connID, socket) = _wssConnManager.SocketDictionary
                                        .Where(x => x.Key.Equals(messageDto.to)).FirstOrDefault();
                if (socket is not null && socket.State == WebSocketState.Open)
                {
                    byte[] buffer = Encoding.UTF8.GetBytes(message);
                    await socket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
                }
                else
                {
                    Console.WriteLine("Invalid ConnID or connection with socket is closed");
                }
            }
            else
            {
                // Broadcast all except the sender
                Console.WriteLine("Broadcast");
                foreach (var (connID, socket) in _wssConnManager.SocketDictionary)
                {
                    if (!connID.Equals(messageDto.from) && socket.State == WebSocketState.Open)
                    {
                        byte[] buffer = Encoding.UTF8.GetBytes(message);
                        await socket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                }
            }
        }
    }
}