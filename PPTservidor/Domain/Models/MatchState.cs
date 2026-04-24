using System;
using PPTservidor.Domain.Enums;

namespace PPTservidor.Domain.Models;

public class MatchState
{
    public string MatchId { get; private set; } = Guid.NewGuid().ToString();
    public Player Player1 { get; set; }
    public Player Player2 { get; set; }
    
    public MatchPhase CurrentPhase { get; set; } = MatchPhase.WaitingForPlayers;

    public bool IsFull => Player1 != null && Player2 != null;

    // Métodos de validação para impedir batotas de clientes adulterados
    public bool CanPlayerAct(string playerId)
    {
        return (Player1?.Id == playerId || Player2?.Id == playerId);
    }

    public Player GetPlayer(string playerId)
    {
        if (Player1?.Id == playerId) return Player1;
        if (Player2?.Id == playerId) return Player2;
        return null;
    }

    public Player GetOpponent(string playerId)
    {
        if (Player1?.Id == playerId) return Player2;
        if (Player2?.Id == playerId) return Player1;
        return null;
    }
}

