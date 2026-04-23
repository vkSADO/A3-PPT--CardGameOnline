using System;
using PPTcliente.Domain.Enums;
namespace PPTcliente.Domain.Models;
public class Card
{
    public CardType Type { get; set; }

    public Card(CardType type)
    {
        Type = type;
    }

}
