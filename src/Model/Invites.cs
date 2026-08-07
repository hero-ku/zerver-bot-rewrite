using System.ComponentModel.DataAnnotations;

namespace ZerverBot.Model;

public class Invite(ulong userId, string inviteId)
{
    [Key]
    public ulong UserId { get; set; } = userId;
    public string InviteId { get; set; } = inviteId;
}