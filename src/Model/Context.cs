using System.ComponentModel.DataAnnotations;
using dotenv.net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

public class BotContext : DbContext
{
    public DbSet<User> Users { get; set; }
    public DbSet<Transaction> Transactions { get; set; }

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
        if (user == null)
        {
            user = new User(userId);
            await AddAsync(user);
            await SaveChangesAsync();
        }
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