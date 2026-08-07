using System.ComponentModel.DataAnnotations;

namespace ZerverBot.Model;

public class Invite()
{
    [Key]
    public ulong UserId { get; set; }
    public required string InviteId { get; set; }
}