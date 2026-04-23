namespace PPTcliente.Domain.Enums;

public enum MatchPhase
{
    WaitingForPlayers, // Aguarda a conexão de dois jogadores
    DrawPhase,         // Fase de Compra de cartas do deck para a mão
    AnnouncementPhase, // Escolha da carta oculta e o anúncio (verdadeiro ou blefe)
    AccusationPhase,   // Momento em que o adversário decide se clica em "É Blefe!"
    RevealPhase,       // As cartas são viradas
    ResolutionPhase,   // Pontuação é calculada e aplica-se penalização se faltarem cartas
    GameOver           // Alguém atingiu 5 pontos
}
