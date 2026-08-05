namespace ZerverBot.Model;

public class User(ulong id)
{
    public ulong Id { get; init; } = id;
    public uint Points { get; set; } = 0;
}