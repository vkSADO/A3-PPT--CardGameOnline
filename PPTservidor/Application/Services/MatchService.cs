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
    public MatchState StartNewMatch(Player player1, Player player2)
    {
        var match = new MatchState
        {
            Player1 = player1,
            Player2 = player2,
            CurrentPhase = MatchPhase.AnnouncementPhase // Saltamos o DrawPhase para simplificar o MVP
        };

        InitializePlayerCards(player1);
        InitializePlayerCards(player2);

        _activeMatches.TryAdd(match.MatchId, match);
        return match;
    }

    public MatchState GetMatch(string matchId)
    {
        _activeMatches.TryGetValue(matchId, out var match);
        return match;
    }

    // 2. Fase de Anúncio: O jogador escolhe a carta real e a carta que diz ter
    public bool SubmitPlay(string matchId, string playerId, CardType realCard, CardType announcedCard)
    {
        var match = GetMatch(matchId);
        if (match == null || match.CurrentPhase != MatchPhase.AnnouncementPhase)
            return false;

        var player = match.GetPlayer(playerId);
        if (player == null || player.SelectedCard.HasValue)
            return false; // Jogador não existe ou já jogou neste turno

        // Valida se o jogador tem a carta na mão e a consome
        if (!player.Hand.Contains(realCard)) return false;
        player.Hand.Remove(realCard);

        player.SelectedCard = realCard;
        player.AnnouncedCard = announcedCard;

        // Se ambos os jogadores já escolheram as suas cartas, o servidor avança a fase
        if (match.Player1.SelectedCard != null && match.Player2.SelectedCard != null)
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

        // Só avança se ambos decidiram se acusam ou não
        var opponent = match.GetOpponent(playerId);
        // Aqui assumimos que no Front o jogador envia 'false' se clicar em "Passar"
        // Para o MVP, avançamos se este jogador agiu e o oponente agiu (ou estamos simplificando para 1 acionamento)
        match.CurrentPhase = MatchPhase.RevealPhase;

        return true;
    }

    // 4. Resolução: Revelar cartas e calcular pontos
    public void ResolveRound(string matchId)
    {
        var match = GetMatch(matchId);
        if(match == null || match.CurrentPhase != MatchPhase.RevealPhase) return; 

        var p1 = match.Player1;
        var p2 = match.Player2;

        // 1. Indentifica quem blefou (A carta real e dirente da anunciada??)
        bool p1Bluffed = p1.SelectedCard != p1.AnnouncedCard;
        bool p2Bluffed = p2.SelectedCard != p2.AnnouncedCard;

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

        // TODO: Adicionar lógica de remover a carta do Deck/Hand do jogador
        // Regra: "A carta usada é descartada... Se o deck acabar, o jogador perde 1 ponto por turno"

        // Verificar condição de vitória
        CheckWinCondition(match);

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
            match.CurrentPhase = MatchPhase.AnnouncementPhase;
        }
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
        
        player.Hand = shuffledDeck.Take(5).ToList();
        player.Deck = shuffledDeck.Skip(5).ToList();
    }
}

