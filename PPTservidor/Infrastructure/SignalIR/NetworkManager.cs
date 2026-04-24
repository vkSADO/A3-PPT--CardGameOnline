using Godot;
using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Text.Json;
using System.Threading.Tasks;
using PPTservidor.Domain.Models;
using PPTservidor.Domain.Enums;

public partial class NetworkManager : Node
{
    private HubConnection _connection;

    // Sinais do Godot que a sua UI vai escutar
    [Signal] public delegate void WaitingForOpponentEventHandler();
    [Signal] public delegate void MatchStartedEventHandler(string matchStateJson);
    [Signal] public delegate void MatchStateUpdatedEventHandler(string matchStateJson);
    [Signal] public delegate void InvalidMoveEventHandler(string errorMessage);

    public override void _Ready()
    {
        // Substitua pela porta correta que o ASP.NET abriu no seu PC
        string serverUrl = "http://localhost:5000/matchhub"; 

        _connection = new HubConnectionBuilder()
            .WithUrl(serverUrl)
            .WithAutomaticReconnect()
            .Build();

        RegisterServerEvents();
        
        // Inicia a conexão de forma assíncrona
        _ = ConnectToServerAsync();
    }

    private void RegisterServerEvents()
    {
        // Quando o servidor avisar que estamos na fila
        _connection.On("WaitingForOpponent", () =>
        {
            // Usamos CallDeferred para garantir que a UI do Godot atualize na thread principal
            CallDeferred(MethodName.EmitSignal, SignalName.WaitingForOpponent);
        });

        // Quando a partida começar ou o estado atualizar (recebemos o objeto)
        _connection.On<MatchState>("MatchStarted", (matchState) =>
        {
            // Para evitar problemas de referência de memória no Godot, 
            // serializamos para JSON antes de passar para a UI, ou passamos os dados diretamente.
            string json = JsonSerializer.Serialize(matchState);
            CallDeferred(MethodName.EmitSignal, SignalName.MatchStarted, json);
        });

        _connection.On<MatchState>("MatchStateUpdated", (matchState) =>
        {
            string json = JsonSerializer.Serialize(matchState);
            CallDeferred(MethodName.EmitSignal, SignalName.MatchStateUpdated, json);
        });

        _connection.On<string>("InvalidMove", (message) =>
        {
            CallDeferred(MethodName.EmitSignal, SignalName.InvalidMove, message);
        });
    }

    private async Task ConnectToServerAsync()
    {
        try
        {
            await _connection.StartAsync();
            GD.Print("Conectado ao servidor SignalR!");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Erro ao conectar: {ex.Message}");
        }
    }

    // ==========================================
    // Métodos que a UI vai chamar para enviar comandos
    // ==========================================

    public async void FindMatch(string myPlayerId)
    {
        if (_connection.State == HubConnectionState.Connected)
        {
            await _connection.InvokeAsync("FindMatch", myPlayerId);
        }
    }

    public async void SubmitPlay(string matchId, string playerId, CardType realCard, CardType announcedCard)
    {
        if (_connection.State == HubConnectionState.Connected)
        {
            await _connection.InvokeAsync("SubmitPlay", matchId, playerId, realCard, announcedCard);
        }
    }

    public async void SubmitAccusation(string matchId, string playerId, bool accusesOpponent)
    {
        if (_connection.State == HubConnectionState.Connected)
        {
            await _connection.InvokeAsync("SubmitAccusation", matchId, playerId, accusesOpponent);
        }
    }

    public override void _ExitTree()
    {
        // Limpeza quando o jogo fechar
        _ = _connection?.DisposeAsync();
    }
}
