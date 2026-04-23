using System.Collections.Generic;
using PPTcliente.Domain.Enums;

namespace PPTcliente.Domain.Models;

public class PlayerState
{
    public int Score { get; set; }
    public List<CardType> Deck { get; set; } = new();
    public List<CardType> Hand { get; set; } = new();
}
