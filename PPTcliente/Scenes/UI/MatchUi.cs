using Godot;
using System;
using System.Text.Json;
using PPTservidor.Domain;
using PPTservidor.Domain.Enums;
using PPTservidor.Domain.Models;
using PPTservidor.Application.Services;

public partial class MatchUi : Control
{
    private MatchLogic _logic = new MatchLogic();

    private Label _playerScore;
    private Label _enemyScore;
    private Label _result;

    public override void _Ready()
    {
        _playerScore = GetNode<Label>("Pontos");
        _enemyScore = GetNode<Label>("EnemyScore");
        _result = GetNode<Label>("Result");
    }

    private void OnRockPressed() => Play("Rock");
    private void OnPaperPressed() => Play("Paper");
    private void OnScissorsPressed() => Play("Scissors");

    private void Play(string choice)
    {
        string result = _logic.PlayRound(choice);

        _result.Text = result;
        _playerScore.Text = _logic.PlayerScore.ToString();
        _enemyScore.Text = _logic.EnemyScore.ToString();
    }
}
