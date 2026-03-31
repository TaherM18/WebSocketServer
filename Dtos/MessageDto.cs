namespace WebSocketServer.Dtos
{
    public record MessageDto
    (
        string from,
        string to,
        string message
    );
}