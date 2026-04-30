using Godot;
using System;
using System.Text.Json;
using PPTservidor.Domain;
using PPTservidor.Domain.Enums;
using PPTservidor.Domain.Models;

public partial class MatchUi : Control
{
    [Export] public Godot.Collections.Array<Texture2D> CardTextures { get; set; } = new();

    private NetworkManager _networkManager;
    private string _matchId;
    private string _myPlayerId;
    private MatchPhase _currentPhase;

    // Variaveis temporarias de jogada
    private CardType? _pendingAnnouncement = null;
    private bool _pendingAccusation = false;
    
    // Referencias da UI
    [Export] private Label _playerName1, _playerName2, _scorePlayer1, _scorePlayer2, _quatityCardDeck, _labelLog;
    [Export] private TextureButton _btnPedra, _btnPapel, _btnTesoura; 
    [Export] private Button _btnAccuser, _btnAnnuncement, _btnFinilized;
    [Export] private Control _panelCardAccused;
    [Export] private TextureRect _CardSelectedTextureArea, _OpponentSelectedTextureArea;
    [Export] private Label _notification;

    // Memória para saber quantos pontos a pessoa tinha no round passado
    private int _myPreviousScore = 0;
    private int _opponentPreviousScore = 0;

    public override void _Ready()
    {
        _networkManager = GetNode<NetworkManager>("/root/NetworkManager");
        _networkManager.MatchStateUpdated += OnMatchStateUpdated;

        // Prepara as jogadas (Salva na memoria mas nao envia)
        _btnPedra.Pressed += () => PrepareAnnouncement(CardType.Rock);
        _btnPapel.Pressed += () => PrepareAnnouncement(CardType.Paper);
        _btnTesoura.Pressed += () => PrepareAnnouncement(CardType.Scissors);
        _btnAccuser.Pressed += PrepareAccusation;

        // O botao Finalizar e quem realmente envia a jogada para o servidor
        _btnFinilized.Pressed += EndTurn;

        // Opcional: botao para reabrir o painel caso o jogador queira trocar a carta antes de finalizar
        _btnAnnuncement.Pressed += () => _panelCardAccused.Visible = true; 
    }

    public void Setup(string matchId, string playerId, string initialMatchJson)
    {
        _matchId = matchId;
        _myPlayerId = playerId;
        var match = JsonSerializer.Deserialize<MatchState>(initialMatchJson);
        UpdateUI(match);
    }

    // --- METODOS DE PREPARACAO ---
    private void PrepareAnnouncement(CardType card)
    {
        _pendingAnnouncement = card;
        _labelLog.Text = $"Voce separou {card}. Clique em Confirmar.";
        _panelCardAccused.Visible = false; // Fecha o painel apos selecionar para limpar a tela
        
        // Textos curtos
        _btnFinilized.Text = "Confirmar";
    }

    private void PrepareAccusation()
    {
        _pendingAccusation = !_pendingAccusation;

        if (_pendingAccusation)
        {
            _labelLog.Text = "Acusar blefe! Clique em Confirmar.";
            _btnFinilized.Text = "Confirmar";
            _btnAccuser.Text = "Cancelar";
        }
        else
        {
            _labelLog.Text = "Acusacao cancelada. Clique em Aceitar.";
            _btnFinilized.Text = "Aceitar";
            _btnAccuser.Text = "Acusar";
        }
    }

    // --- ENVIO AO SERVIDOR ---
    private void EndTurn()
    {
        _btnFinilized.Disabled = true; // Evita duplo clique imediato

        if (_currentPhase == MatchPhase.AnnouncementPhase)
        {
            _networkManager.SubmitPlay(_matchId, _myPlayerId, _pendingAnnouncement);
            
            _labelLog.Text = _pendingAnnouncement.HasValue 
                ? $"Jogada enviada ({_pendingAnnouncement.Value}). Aguardando..." 
                : "Passou a vez. Aguardando...";
                
            _pendingAnnouncement = null; // Limpa para a proxima rodada
        }
        else if (_currentPhase == MatchPhase.AccusationPhase)
        {
            _networkManager.SubmitAccusation(_matchId, _myPlayerId, _pendingAccusation);
            
            _labelLog.Text = _pendingAccusation 
                ? "Acusacao enviada. Aguardando..." 
                : "Jogada aceita. Aguardando...";
                
            _pendingAccusation = false; // Limpa para a proxima rodada
            _btnAccuser.Disabled = false; // Restaura o botao para a proxima rodada
        }
    }

    // --- DESENHO DA TELA (O CORACAO DO LOOP) ---
    private async void UpdateUI(MatchState match)
    {
        var me = match.GetPlayer(_myPlayerId);
        var opponent = match.GetOpponent(_myPlayerId);

        // 1. DETECTA FIM DE ROUND E NOTIFICACAO
        // Se estavamos na Acusacao e o servidor mandou Anuncio, significa que o round virou!
        bool isNewRound = _currentPhase == MatchPhase.AccusationPhase && match.CurrentPhase == MatchPhase.AnnouncementPhase;

        int myPointsWon = me.Score - _myPreviousScore;
        int oppPointsWon = opponent != null ? opponent.Score - _opponentPreviousScore : 0;

        // Atualiza a memoria de pontos para a proxima checagem
        _myPreviousScore = me.Score;
        if (opponent != null) _opponentPreviousScore = opponent.Score;

        if (isNewRound)
        {
            _notification.Visible = true;
            if (myPointsWon > oppPointsWon) _notification.Text = $"Voce venceu o round! (+{myPointsWon} pts)";
            else if (oppPointsWon > myPointsWon) _notification.Text = $"Oponente venceu o round! (+{oppPointsWon} pts)";
            else _notification.Text = "Empate no Round!";

            // REVELAÇÃO: Mostra a carta que o oponente JOGOU no round que acabou!
            if (opponent != null && opponent.LastPlayedCard.HasValue)
            {
                if (opponent.LastPlayedCard.Value == CardType.Rock) _OpponentSelectedTextureArea.Texture = CardTextures[0];
                else if (opponent.LastPlayedCard.Value == CardType.Paper) _OpponentSelectedTextureArea.Texture = CardTextures[1];
                else _OpponentSelectedTextureArea.Texture = CardTextures[2];
            }

            // Esconde os botoes para ninguem clicar em nada enquanto le a notificacao
            _panelCardAccused.Visible = false;
            _btnFinilized.Visible = false;
            _btnAccuser.Visible = false;
            _labelLog.Text = "Calculando resultados...";

            // FIM DA REVELAÇÃO: Esconde a notificação e "vira" a carta do oponente para baixo novamente
            await ToSignal(GetTree().CreateTimer(3.0f), SceneTreeTimer.SignalName.Timeout);
            
            _notification.Visible = false; // Esconde a notificacao e o jogo continua!

        }

        _currentPhase = match.CurrentPhase; // Atualiza a fase atual

        // 2. ATUALIZA STATUS FIXOS (Score, Nomes, Decks)
        _scorePlayer1.Text = me.Score.ToString();
        _playerName1.Text = me.Id;
        _quatityCardDeck.Text = me.Deck.Count.ToString();

        if (opponent != null)
        {
            _scorePlayer2.Text = opponent.Score.ToString();
            _playerName2.Text = opponent.Id;
            _quatityCardDeck.Text = opponent.Deck.Count.ToString();
            _OpponentSelectedTextureArea.Texture = CardTextures[3];
        }

        // Mostra a carta real que o servidor deu para o jogador
        if (me.SelectedCard.HasValue)
        {
            if (me.SelectedCard.Value == CardType.Rock) _CardSelectedTextureArea.Texture = CardTextures[0];
            else if (me.SelectedCard.Value == CardType.Paper) _CardSelectedTextureArea.Texture = CardTextures[1];
            else _CardSelectedTextureArea.Texture = CardTextures[2];
        }

        // 3. RESETA A UI DINAMICA
        _panelCardAccused.Visible = false;
        _btnAccuser.Visible = false;
        _btnFinilized.Visible = false;
        _btnFinilized.Disabled = false; 

        // 4. DESENHA APENAS A FASE ATUAL
        switch (match.CurrentPhase)
        {
            case MatchPhase.AnnouncementPhase:
                if (!me.HasSubmittedAnnouncement)
                {
                    _btnFinilized.Visible = true; 
                    _btnFinilized.Text = "Passar";
                    _labelLog.Text = $"Sua carta: {me.SelectedCard}. Escolha o anuncio ou clique em Passar.";
                }
                else
                {
                    _labelLog.Text = "Decisao enviada. Aguardando oponente...";
                }
                break;
            
            case MatchPhase.AccusationPhase:
                if (!me.HasSubmittedAccusation)
                {
                    _btnFinilized.Visible = true;

                    if (opponent != null && opponent.AnnouncedCard.HasValue)
                    {
                        _btnAccuser.Visible = true; 
                        _btnAccuser.Text = "Acusar";
                        _btnFinilized.Text = "Aceitar";
                        _labelLog.Text = $"Oponente diz ter: {opponent.AnnouncedCard.Value}! Acusar ou Aceitar?";
                    }
                    else
                    {
                        _btnFinilized.Text = "Continuar";
                        _labelLog.Text = "Oponente ficou quieto. Clique em Continuar.";
                    }
                }
                else
                {
                    _labelLog.Text = "Decisao enviada. Aguardando servidor...";
                }
                break;

            case MatchPhase.GameOver:
                _panelCardAccused.Visible = false;
                _btnAccuser.Visible = false;
                _btnFinilized.Visible = false;
                
                // Exibe a notificacao grande de Fim de Jogo
                _notification.Visible = true;
                _notification.Text = me.Score >= opponent?.Score ? "VITORIA!" : "DERROTA!";
                _labelLog.Text = "Retornando ao Lobby em 5 segundos...";

                // Congela por 5 segundos e joga para a tela inicial
                await ToSignal(GetTree().CreateTimer(5.0f), SceneTreeTimer.SignalName.Timeout);
                
                GetTree().ChangeSceneToFile("res://PPTcliente/Scenes/UI/lobby.tscn"); 
                break;
        }
    }

    private void OnMatchStateUpdated(string json)
    {
        var match = JsonSerializer.Deserialize<MatchState>(json);
        UpdateUI(match);
    }
}