using System.Collections.Generic;
using System.Runtime.InteropServices;
using Godot;
using PPTservidor.Domain.Enums;

namespace PPTservidor.Domain.Models;

public class Player
{
    public string Id { get; set; } // O ID real (futuramente do Google)
    public string ConnectionId { get; set; } // O ID da sessão do SignalR

    public int Score { get; set; } = 0;
    public List<CardType> Deck { get; set; } = new();
    
    // Estado transitório do turno atual
    public CardType? SelectedCard { get; set; } // A carta real jogada virada para baixo
    public CardType? AnnouncedCard { get; set; } // A carta que o jogador DIZ ter jogado
    public CardType? LastPlayedCard { get; set; } // A carta que foi jogada no round anterior
    public bool HasSubmittedAnnouncement { get; set; } = false; // Se já finalizou a fase de anúncio
    public bool HasAccusedBluff { get; set; } = false; // Se chamou o blefe do oponente neste turno
    public bool HasSubmittedAccusation { get; set; } = false; // Se já submeteu decisão de acusação

    
    public void InitializeDeck()
    {
        Deck.Clear();
        for (int i = 0; i < 4; i++) Deck.Add(CardType.Rock);
        for (int i = 0; i < 3; i++) Deck.Add(CardType.Paper);
        for (int i = 0; i < 3; i++) Deck.Add(CardType.Scissors);
        
    }

    public void ResetTurnState()
    {
        SelectedCard = null;
        AnnouncedCard = null;
        HasSubmittedAnnouncement = false;
        HasAccusedBluff = false;
        HasSubmittedAccusation = false;
    }
}

