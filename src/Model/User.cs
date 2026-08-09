namespace ZerverBot.Model;

public class User(ulong id, uint points)
{
    public ulong Id { get; init; } = id;
    public uint Points { get; set; } = points;
}