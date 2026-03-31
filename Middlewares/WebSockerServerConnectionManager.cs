using System.Collections.Concurrent;
using System.Net.WebSockets;

namespace WebSocketServer.Middlewares
{
    public class WebSockerServerConnectionManager
    {
        public ConcurrentDictionary<string, WebSocket> SocketDictionary { get; private set; } = new ConcurrentDictionary<string, WebSocket>();

        public string AddSocket(WebSocket webSocket)
        {
            Dictionary<string, WebSocket> pairs = SocketDictionary.Where(x => x.Value.Equals(webSocket)).ToDictionary();
            if (pairs.Count() > 0)
            {
                Console.WriteLine($"Connection alredy exists");
                return pairs.First().Key;
            }

            string connectionID = Guid.NewGuid().ToString();
            SocketDictionary.TryAdd(connectionID, webSocket);

            Console.WriteLine($"Connection Added: {connectionID}");
            return connectionID;
        }
    }
}