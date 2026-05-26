using System;
using System.Text.Json.Serialization;

namespace PPTservidor.Domain.Models;

public class MatchRecord
{
    [JsonPropertyName("id")] // Obrigatório para o CosmosDB
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    public string MatchId { get; set; }
    public string Player1Id { get; set; }
    public string Player2Id { get; set; }
    public int Player1Score { get; set; }
    public int Player2Score { get; set; }
    public string WinnerId { get; set; }
    public DateTime EndTime { get; set; } = DateTime.UtcNow;
}