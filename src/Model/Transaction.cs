using System.ComponentModel.DataAnnotations;
using Discord;

namespace ZerverBot.Model;

public class Transaction(ulong interactionId, ulong senderId, ulong recipientId, uint amount)
{
    [Key]
    public ulong InteractionId { get; init; } = interactionId;
    public DateTimeOffset Timestamp { get; init; } = SnowflakeUtils.FromSnowflake(interactionId);
    public ulong SenderId { get; init; } = senderId;
    public ulong RecipientId { get; init; } = recipientId;
    public uint Amount { get; init; } = amount;
}