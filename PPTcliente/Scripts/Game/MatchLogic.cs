using System;

public class MatchLogic
{
    private Random _rng = new Random();

    public int PlayerScore { get; private set; }
    public int EnemyScore { get; private set; }

    public string PlayRound(string playerChoice)
    {
        string[] options = { "Rock", "Paper", "Scissors" };
        string enemyChoice = options[_rng.Next(0, 3)];

        if (playerChoice == enemyChoice)
            return $"Empate! Ambos jogaram {playerChoice}";

        bool playerWin =
            (playerChoice == "Rock" && enemyChoice == "Scissors") ||
            (playerChoice == "Paper" && enemyChoice == "Rock") ||
            (playerChoice == "Scissors" && enemyChoice == "Paper");

        if (playerWin)
        {
            PlayerScore++;
            return $"Você venceu! {playerChoice} vs {enemyChoice}";
        }
        else
        {
            EnemyScore++;
            return $"Você perdeu! {playerChoice} vs {enemyChoice}";
        }
    }
}
