using System;
using PPTservidor.Domain.Enums;
namespace PPTservidor.Domain.Models;
public class Card
{
    public CardType Type { get; set; }

    public Card(CardType type)
    {
        Type = type;
    }

}

