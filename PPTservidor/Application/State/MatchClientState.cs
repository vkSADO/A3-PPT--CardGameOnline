using PPTservidor.Domain.Models;

namespace PPTservidor.Application.State;

public class MatchClientState
{

    public PlayerState LocalPlayer {get; set;}
    public PlayerState Opponent { get; set; }

    public int RoundNumber {get; set;}
    public bool RoundResolver {get; set;}
    
}

