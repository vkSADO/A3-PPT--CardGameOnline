using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using PPTservidor.Domain.Enums;
using PPTservidor.Domain.Models;

namespace PPTservidor.Application.Services;

public class MatchService
{
    // Utilizamos ConcurrentDictionary porque o SignalR é multithreaded.
    // Várias requisições podem tentar aceder às partidas em simultâneo.
    private readonly ConcurrentDictionary<string, MatchState> _activeMatches = new();

    // 1. Inicialização da Partida
    public MatchState StartNewMatch(Player p1, Player p2)
    {
        p1.InitializeDeck();
        p2.InitializeDeck();

        var match = new MatchState { Player1 = p1, Player2 = p2 };
        
        StartRound(match); // Saca as cartas e inicia o turno

        _activeMatches.TryAdd(match.MatchId, match);
        return match;
    }

    // Saque de carta do deck
    private void StartRound(MatchState match)
    {
        DrawCardForPlayer(match.Player1);
        DrawCardForPlayer(match.Player2);
        
        match.CurrentPhase = MatchPhase.AnnouncementPhase;
    }

    private void DrawCardForPlayer(Player player)
    {
        if(player.Deck.Count > 0 )
        {
            int index = Random.Shared.Next(player.Deck.Count);
            player.SelectedCard = player.Deck[index];
            player.Deck.RemoveAt(index);
        }
        else
        {
            player.SelectedCard = null;
        }
    }

    public MatchState GetMatch(string matchId)
    {
        _activeMatches.TryGetValue(matchId, out var match);
        return match;
    }

    // 2. Fase de Anúncio: O jogador escolhe a carta real e a carta que diz ter
    public bool SubmitPlay(string matchId, string playerId, CardType? announcedCard)
    {
        var match = GetMatch(matchId);
        if (match == null || match.CurrentPhase != MatchPhase.AnnouncementPhase) return false;

        var player = match.GetPlayer(playerId);
        if (player == null || player.HasSubmittedAnnouncement) return false;

        player.AnnouncedCard = announcedCard;
        player.HasSubmittedAnnouncement = true;

        // Se ambos finalizaram a fase de anúncio, vai para a acusação
        if (match.Player1.HasSubmittedAnnouncement && match.Player2.HasSubmittedAnnouncement)
        {
            match.CurrentPhase = MatchPhase.AccusationPhase;
        }

        return true;
    }

    // 3. Fase de Acusação: O jogador decide se clica no botão "É Blefe!"
    public bool SubmitAccusation(string matchId, string playerId, bool accusesOpponent)
    {
        var match = GetMatch(matchId);
        if (match == null || match.CurrentPhase != MatchPhase.AccusationPhase)
            return false;

        var player = match.GetPlayer(playerId);
        if (player == null)
            return false;

        player.HasAccusedBluff = accusesOpponent;
        player.HasSubmittedAccusation = true;

        // Só avança se ambos decidiram se acusam ou não
        if (match.Player1.HasSubmittedAccusation && match.Player2.HasSubmittedAccusation)
        {
            match.CurrentPhase = MatchPhase.RevealPhase;
        }

        return true;
    }

    // 4. Resolução: Revelar cartas e calcular pontos
    public void ResolveRound(string matchId)
    {
        var match = GetMatch(matchId);
        if(match == null || match.CurrentPhase != MatchPhase.RevealPhase) return; 

        var p1 = match.Player1;
        var p2 = match.Player2;

        // Se algum jogador não tem carta, pula a resolução normal
        if (!p1.SelectedCard.HasValue || !p2.SelectedCard.HasValue)
        {
            // Já penalizou no DrawCardForPlayer
            CheckWinCondition(match);
            return;
        }

        // 1. Identifica quem blefou 
        // (Só é blefe se ele anunciou alguma coisa E a carta anunciada for diferente da carta real)
        bool p1Bluffed = p1.AnnouncedCard.HasValue && p1.SelectedCard != p1.AnnouncedCard.Value;
        bool p2Bluffed = p2.AnnouncedCard.HasValue && p2.SelectedCard != p2.AnnouncedCard.Value;

        // 2. Identifica o vencedor normal (Sem anuncio)
        bool p1Wins  = Beats(p1.SelectedCard.Value, p2.SelectedCard.Value);
        bool p2Wins  = Beats(p2.SelectedCard.Value, p1.SelectedCard.Value);
        bool isTie = !p1Wins && !p2Wins; // empate

        // 3. Aplicando os pontos de Acusação
        ProcessScoring(p1, p2, p1Bluffed); // P1 acusa P2
        ProcessScoring(p2, p1, p2Bluffed); // P2 acusa P1

        // 4. Aplicarndo os pontos e derrota + modificadores
        if(!isTie)
        {
            var winner = p1Wins ? p1:p2;
            var loser = p1Wins ? p2:p1;

            bool winnerBluffed = p1Wins ? p1Bluffed:p2Bluffed;
            bool loserBluffed = p1Wins ? p2Bluffed:p1Bluffed;


            // Bonus de vitoria
            if (winnerBluffed && !loser.HasAccusedBluff)
            {
                winner.Score += 2; // Vitoria com blefe não descoberto
            }
            else
            {
                winner.Score += 1; // Vitoria sem blefe (ou blefe descoberto)
            }

            // Penalidade de derrota
            if(loserBluffed)
            {
                loser.Score -= 1; // Derrota com blefe
            }
        }

        // 5. Verifica condição de vitória (5 pontos OU fim do baralho)
        if (p1.Score >= 5 || p2.Score >= 5 || p1.Deck.Count == 0 || p2.Deck.Count == 0)
        {
            match.CurrentPhase = MatchPhase.GameOver;
        }
        else
        {
            // SALVA A CARTA ANTES DE DESCARTAR PARA O CLIENTE PODER VER!
            p1.LastPlayedCard = p1.SelectedCard;
            p2.LastPlayedCard = p2.SelectedCard;

            // 1- Limpa o que foi anunciado
            p1.ResetTurnState();
            p2.ResetTurnState();

            // 2- Compra novas cartas do deck para o próximo turno
            DrawCardForPlayer(p1);
            DrawCardForPlayer(p2);

            // 3- Volta a partida pra fase de anúncio
            match.CurrentPhase = MatchPhase.AnnouncementPhase;
        }
    }

    private void ProcessScoring(Player accuser, Player accused, bool accusedBluffed)
    {
        if(!accuser.HasAccusedBluff) return;

        if(accusedBluffed)
        {
            accuser.Score += 1; // Acusação correta
        }
        else
        {
            accuser.Score -= 2; // Acusação errada
        }
    }

    private void CheckWinCondition(MatchState match)
    {
        // Verificar condição de vitória (ex: 5 pontos)
        if (match.Player1.Score >= 5 || match.Player2.Score >= 5)
        {
            match.CurrentPhase = MatchPhase.GameOver;
        }
        else
        {
            // Limpar o estado do turno e voltar à fase de anúncio para a próxima ronda
            match.Player1.ResetTurnState();
            match.Player2.ResetTurnState();
            StartRound(match); // Saca as cartas para a próxima ronda
            match.CurrentPhase = MatchPhase.AnnouncementPhase;
        }
    }

    public MatchState HandlePlayerDisconnect(string connectionId)
    {
        // Procura se o jogador estava em alguma partida ativa
        var matchPair = _activeMatches.FirstOrDefault(m => 
            m.Value.Player1?.ConnectionId == connectionId || 
            m.Value.Player2?.ConnectionId == connectionId);

        var match = matchPair.Value;
        
        if (match != null && match.CurrentPhase != MatchPhase.GameOver)
        {
            // Descobre quem desconectou e quem ficou
            var disconnectedPlayer = match.Player1.ConnectionId == connectionId ? match.Player1 : match.Player2;
            var remainingPlayer = match.Player1.ConnectionId == connectionId ? match.Player2 : match.Player1;

            if (remainingPlayer != null)
            {
                // Dá a vitória máxima ao jogador que ficou (W.O.)
                remainingPlayer.Score = 5;
                disconnectedPlayer.Score = 0;
            }

            match.CurrentPhase = MatchPhase.GameOver;
            return match; // Retorna a partida para avisarmos o jogador que ficou
        }

        return null;
    }

    // Regra auxiliar clássica
    private bool Beats(CardType card1, CardType card2)
    {
        if (card1 == card2) return false;
        if (card1 == CardType.Rock && card2 == CardType.Scissors) return true;
        if (card1 == CardType.Paper && card2 == CardType.Rock) return true;
        if (card1 == CardType.Scissors && card2 == CardType.Paper) return true;
        return false;
    }

    private void InitializePlayerCards(Player player)
    {
        var deck = new List<CardType>();
        var random = new Random();

        // Cria um baralho equilibrado de 15 cartas (5 de cada tipo) para o MVP
        for (int i = 0; i < 5; i++)
        {
            deck.Add(CardType.Rock);
            deck.Add(CardType.Paper);
            deck.Add(CardType.Scissors);
        }

        // Embaralha o baralho e distribui as cartas
        var shuffledDeck = deck.OrderBy(x => random.Next()).ToList();
        
        player.Deck = shuffledDeck.Skip(5).ToList();
    }
}

