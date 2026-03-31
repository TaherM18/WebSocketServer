using System.Net.WebSockets;
using System.Text;

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

                await ReceiveMessageAsync(webSocket, async (result, bytes) =>
                {
                    switch (result.MessageType)
                    {
                        case WebSocketMessageType.Text:
                            Console.WriteLine($"Received text message: {Encoding.UTF8.GetString(bytes, 0, result.Count)}");
                            break;
                        case WebSocketMessageType.Binary:
                            break;
                        case WebSocketMessageType.Close:
                            Console.WriteLine("Received close message");

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
    }
}