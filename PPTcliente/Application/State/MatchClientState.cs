using PPTcliente.Domain.Models;

namespace PPTcliente.Application.State;

public class MatchClientState
{

    public PlayerState LocalPlayer {get; set;}
    public PlayerState Opponent { get; set; }

    public int RoundNumber {get; set;}
    public bool RoundResolver {get; set;}
    
}
