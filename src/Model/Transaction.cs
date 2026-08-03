using System.ComponentModel.DataAnnotations;
using Discord;

public class Transaction(ulong interactionId, ulong senderId, ulong recipientId, uint amount)
{
    [Key]
    public ulong InteractionId { get; set; } = interactionId;
    public DateTimeOffset Timestamp { get; set; } = SnowflakeUtils.FromSnowflake(interactionId);
    public ulong SenderId { get; set; } = senderId;
    public ulong RecipientId { get; set; } = recipientId;
    public uint Amount { get; set; } = amount;
}