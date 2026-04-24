using Godot;
using System;

public partial class Lobby : Control
{
    private NetworkManager _networkManager;
    private Label _statusLabel;
    private Button _findMatchButton;

    public override void _Ready()
    {
        // 1. Pega a referência do Autoload de rede
        _networkManager = GetNode<NetworkManager>("/root/NetworkManager");
        
        // 2. Pega as referências dos nós da UI
        _statusLabel = GetNode<Label>("Container/VBoxContainer/ServerStatusMessage");
        _findMatchButton = GetNode<Button>("Container/VBoxContainer/FindMatchButton");

        // 3. Conecta o clique do botão
        _findMatchButton.Pressed += OnFindMatchPressed;

        // 4. Conecta os sinais que vêm do servidor (via NetworkManager)
        _networkManager.WaitingForOpponent += OnWaitingForOpponent;
        _networkManager.MatchStarted += OnMatchStarted;
    }

    private void OnFindMatchPressed()
    {
        // Como ainda não temos o login do Google pronto, 
        // vamos gerar um ID falso (Guid) para simular um jogador único neste teste.
        string myFakeId = Guid.NewGuid().ToString(); 
        
        _statusLabel.Text = "Conectando ao servidor...";
        _findMatchButton.Disabled = true; // Impede que o jogador clique várias vezes
        
        _networkManager.FindMatch(myFakeId);
    }

    private void OnWaitingForOpponent()
    {
        _statusLabel.Text = "Aguardando oponente entrar na fila...";
    }

    private void OnMatchStarted(string matchStateJson)
    {
        _statusLabel.Text = "Partida Encontrada! O jogo vai começar.";
        
        // Imprime o JSON no console do Godot para vermos os dados que o servidor mandou
        GD.Print("Estado da partida recebido: " + matchStateJson);
        
        // TODO: Futuramente, aqui faremos a troca de cena para a "Mesa" do jogo
    }
    
    public override void _ExitTree()
    {
        // Boa prática: desconectar sinais ao destruir a tela
        if (_networkManager != null)
        {
            _networkManager.WaitingForOpponent -= OnWaitingForOpponent;
            _networkManager.MatchStarted -= OnMatchStarted;
        }
    }
}
