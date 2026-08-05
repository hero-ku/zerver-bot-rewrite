using dotenv.net;
using Microsoft.EntityFrameworkCore;
using ZerverBot.Model.Arena;

namespace ZerverBot.Model;

public class BotContext : DbContext
{
    public DbSet<User> Users { get; set; }
    public DbSet<Transaction> Transactions { get; set; }
    public DbSet<OstracismVote> OstracismVotes { get; set; }
    public DbSet<ElectionVote> ElectionVotes { get; set; }

    public static readonly ulong RESERVE_ACCOUNT_ID = 1;

    public async Task<uint> GetReservePointsAsync()
    {
        return await GetPointsAsync(RESERVE_ACCOUNT_ID);
    }

    public async Task<uint> AddReservePointsAsync(ulong interactionId, uint amount)
    {
        return await AddPointsAsync(interactionId, RESERVE_ACCOUNT_ID, amount);
    }

    public async Task<(uint, uint)> SendToReserveAsync(ulong interactionId, ulong senderId, uint amount)
    {
        return await TransferPointsAsync(interactionId, senderId, RESERVE_ACCOUNT_ID, amount);
    }

    public async Task<(uint, uint)> PayFromReserveAsync(ulong interactionId, ulong recipientId, uint amount)
    {
        return await TransferPointsAsync(interactionId, RESERVE_ACCOUNT_ID, recipientId, amount);
    }

    public async Task<uint> GetPointsAsync(ulong userId)
    {
        var user = await GetUserAsync(userId);
        return user.Points;
    }

    public async Task<uint> AddPointsAsync(ulong interactionId, ulong userId, uint amount)
    {
        var user = await UpdateUserAsync(userId, u =>
        {
            u.Points += amount;
        });
        Add(new Transaction(interactionId, 0, userId, amount));
        await SaveChangesAsync();

        return user.Points;
    }

    public async Task<uint> RemovePointsAsync(ulong interactionId, ulong userId, uint amount)
    {
        var user = await UpdateUserAsync(userId, u =>
        {
            u.Points -= amount;
        });
        Add(new Transaction(interactionId, userId, 0, amount));
        await SaveChangesAsync();

        return user.Points;
    }

    public async Task<(uint, uint)> TransferPointsAsync(ulong interactionId, ulong senderId, ulong recipientId, uint amount)
    {
        var sender = await UpdateUserAsync(senderId, u =>
        {
            u.Points -= amount;
        });
        var recipient = await UpdateUserAsync(recipientId, u =>
        {
            u.Points += amount;
        });
        Add(new Transaction(interactionId, senderId, recipientId, amount));
        await SaveChangesAsync();

        return (sender.Points, recipient.Points);
    }

    public async Task<User> GetUserAsync(ulong userId)
    {
        var user = await Users.Where(u => u.Id == userId).SingleOrDefaultAsync();
        if (user != null) return user;

        user = new User(userId);
        await AddAsync(user);
        await SaveChangesAsync();
        return user;
    }

    public async Task<User> UpdateUserAsync(ulong userId, Action<User> updater)
    {
        var user = await GetUserAsync(userId);
        updater(user);

        return user;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        base.OnConfiguring(options);

        DotEnv.Load();
        var uri = Environment.GetEnvironmentVariable("DATABASE_URI");
        options.UseNpgsql(uri);
    }
}