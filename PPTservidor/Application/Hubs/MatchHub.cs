using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using PPTservidor.Domain.Enums;
using PPTservidor.Domain.Models;
using PPTservidor.Application.Services;

namespace PPTservidor.Application.Hubs;

public class MatchHub : Hub
{
    private readonly MatchService _matchService;
    private readonly ILogger<MatchHub> _logger;

    // Fila de espera simples em memória para o MVP
    private static Player _waitingPlayer;
    private static string _waitingConnectionId;
    private static readonly object _lock = new object();

    // Constantes de eventos SignalR
    private const string MatchStartedEvent = "MatchStarted";
    private const string WaitingForOpponentEvent = "WaitingForOpponent";
    private const string MatchStateUpdatedEvent = "MatchStateUpdated";
    private const string InvalidMoveEvent = "InvalidMove";
    private const string OpponentDisconnectedEvent = "OpponentDisconnected";

    public MatchHub(MatchService matchService, ILogger<MatchHub> logger)
    {
        _matchService = matchService;
        _logger = logger;
    }

    // 1. O cliente Godot chama este método quando o jogador clica em "Procurar Partida"
    public async Task FindMatch(string playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId))
        {
            _logger.LogWarning("FindMatch chamado com playerId inválido");
            await Clients.Caller.SendAsync(InvalidMoveEvent, "ID do jogador inválido.");
            return;
        }

        MatchState newMatch = null;

        lock (_lock)
        {
            if (_waitingPlayer == null)
            {
                // Não tem ninguém esperando, então este jogador entra na fila
                _waitingPlayer = new Player { Id = playerId, ConnectionId = Context.ConnectionId };
                _waitingConnectionId = Context.ConnectionId;
                _logger.LogInformation($"Jogador {playerId} entrou na fila de espera. ConnectionId: {Context.ConnectionId}");
                return;
            }
            else
            {
                // Tem alguém esperando! Forma-se a partida.
                var player2 = new Player { Id = playerId, ConnectionId = Context.ConnectionId };
                newMatch = _matchService.StartNewMatch(_waitingPlayer, player2);
                _logger.LogInformation($"Partida iniciada: {newMatch.MatchId} entre {_waitingPlayer.Id} e {playerId}");
                _waitingPlayer = null;
                _waitingConnectionId = null;
            }
        }

        // Se o código chegou aqui sem entrar no "return" de cima, é porque foi o Player 2.
        if (newMatch != null)
        {
            try
            {
                // Colocamos as duas conexões no mesmo "Grupo" nomeado com o ID da partida
                await Groups.AddToGroupAsync(newMatch.Player1.ConnectionId, newMatch.MatchId);
                await Groups.AddToGroupAsync(newMatch.Player2.ConnectionId, newMatch.MatchId);

                // Dispara o evento "MatchStarted" para OS DOIS jogadores
                await Clients.Group(newMatch.MatchId).SendAsync(MatchStartedEvent, newMatch);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Erro ao iniciar partida {newMatch.MatchId}: {ex.Message}");
                await Clients.Caller.SendAsync(InvalidMoveEvent, "Erro ao iniciar partida.");
            }
        }
        else
        {
            // Avisa apenas o jogador que acabou de entrar que ele está na fila
            await Clients.Caller.SendAsync(WaitingForOpponentEvent);
        }
    }

    // 2. O cliente Godot chama este método quando o jogador escolhe a carta e o blefe
    public async Task SubmitPlay(string matchId, string playerId, CardType realCard, CardType announcedCard)
    {
        // Validações iniciais
        if (string.IsNullOrWhiteSpace(matchId) || string.IsNullOrWhiteSpace(playerId))
        {
            _logger.LogWarning($"SubmitPlay com dados inválidos. MatchId: {matchId}, PlayerId: {playerId}");
            await Clients.Caller.SendAsync(InvalidMoveEvent, "Dados inválidos.");
            return;
        }

        var match = _matchService.GetMatch(matchId);
        if (match == null)
        {
            _logger.LogWarning($"Partida não encontrada: {matchId}");
            await Clients.Caller.SendAsync(InvalidMoveEvent, "Partida não encontrada.");
            return;
        }

        // Validação de autorização: verifica se o jogador pertence à partida
        if (!match.CanPlayerAct(playerId))
        {
            _logger.LogWarning($"Jogador {playerId} tentou agir em partida {matchId} sem autorização");
            await Clients.Caller.SendAsync(InvalidMoveEvent, "Você não está nesta partida.");
            return;
        }

        bool success = _matchService.SubmitPlay(matchId, playerId, realCard, announcedCard);

        if (success)
        {
            var updatedMatch = _matchService.GetMatch(matchId);
            _logger.LogInformation($"Jogada aceita em {matchId} por {playerId}. Fase atual: {updatedMatch.CurrentPhase}");
            
            // Notifica a sala inteira que o estado mudou
            await Clients.Group(matchId).SendAsync(MatchStateUpdatedEvent, updatedMatch);
        }
        else
        {
            _logger.LogWarning($"Jogada rejeitada em {matchId} por {playerId}");
            await Clients.Caller.SendAsync(InvalidMoveEvent, "Jogada inválida neste momento.");
        }
    }

    // 3. O cliente Godot chama este método no momento da acusação
    public async Task SubmitAccusation(string matchId, string playerId, bool accusesOpponent)
    {
        // Validações iniciais
        if (string.IsNullOrWhiteSpace(matchId) || string.IsNullOrWhiteSpace(playerId))
        {
            _logger.LogWarning($"SubmitAccusation com dados inválidos. MatchId: {matchId}, PlayerId: {playerId}");
            await Clients.Caller.SendAsync(InvalidMoveEvent, "Dados inválidos.");
            return;
        }

        var match = _matchService.GetMatch(matchId);
        if (match == null)
        {
            _logger.LogWarning($"Partida não encontrada: {matchId}");
            await Clients.Caller.SendAsync(InvalidMoveEvent, "Partida não encontrada.");
            return;
        }

        // Validação de autorização
        if (!match.CanPlayerAct(playerId))
        {
            _logger.LogWarning($"Jogador {playerId} tentou acusar em partida {matchId} sem autorização");
            await Clients.Caller.SendAsync(InvalidMoveEvent, "Você não está nesta partida.");
            return;
        }

        bool success = _matchService.SubmitAccusation(matchId, playerId, accusesOpponent);

        if (success)
        {
            var updatedMatch = _matchService.GetMatch(matchId);
            if (updatedMatch == null)
            {
                _logger.LogError($"Partida desapareceu após acusação: {matchId}");
                await Clients.Caller.SendAsync(InvalidMoveEvent, "Erro ao processar acusação.");
                return;
            }

            _logger.LogInformation($"Acusação processada em {matchId} por {playerId}. Acusou blefe: {accusesOpponent}. Nova fase: {updatedMatch.CurrentPhase}");

            // Se a fase foi para RevealPhase, resolve o turno
            if (updatedMatch.CurrentPhase == MatchPhase.RevealPhase)
            {
                _matchService.ResolveRound(matchId);
                updatedMatch = _matchService.GetMatch(matchId); // Recarrega após resolver
                _logger.LogInformation($"Turno resolvido em {matchId}. Nova fase: {updatedMatch.CurrentPhase}");
            }

            // Envia o estado atualizado para todos os jogadores
            await Clients.Group(matchId).SendAsync(MatchStateUpdatedEvent, updatedMatch);
        }
        else
        {
            _logger.LogWarning($"Acusação rejeitada em {matchId} por {playerId}");
            await Clients.Caller.SendAsync(InvalidMoveEvent, "Acusação inválida neste momento.");
        }
    }

    // 4. Limpeza se alguém fechar o jogo inesperadamente
    public override async Task OnDisconnectedAsync(Exception exception)
    {
        _logger.LogInformation($"Cliente desconectado: {Context.ConnectionId}. Erro: {exception?.Message}");

        // Limpa da fila de espera se estava lá
        lock (_lock)
        {
            if (_waitingConnectionId == Context.ConnectionId)
            {
                _logger.LogInformation($"Removendo jogador da fila: {_waitingPlayer?.Id}");
                _waitingPlayer = null;
                _waitingConnectionId = null;
            }
        }

        // TODO futuro: Lógica para dar a vitória ao oponente se alguém desconectar no meio da partida
        // Isso requer um sistema para rastrear quais conexões estão em quais partidas

        await base.OnDisconnectedAsync(exception);
    }
}

