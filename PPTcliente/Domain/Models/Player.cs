using System.Collections.Generic;
using PPTcliente.Domain.Enums;

namespace PPTcliente.Domain.Models;

public class Player
{
    public string Id { get; set; } // O ID real (futuramente do Google)
    public string ConnectionId { get; set; } // O ID da sessão do SignalR

    public int Score { get; set; } = 0;
    public List<CardType> Deck { get; set; } = new();
    public List<CardType> Hand { get; set; } = new();

    // Estado transitório do turno atual
    public CardType? SelectedCard { get; set; } // A carta real jogada virada para baixo
    public CardType? AnnouncedCard { get; set; } // A carta que o jogador DIZ ter jogado
    public bool HasAccusedBluff { get; set; } = false; // Se chamou o blefe do oponente neste turno

    public void ResetTurnState()
    {
        SelectedCard = null;
        AnnouncedCard = null;
        HasAccusedBluff = false;
    }
}
