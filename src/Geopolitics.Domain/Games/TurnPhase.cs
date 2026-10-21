namespace Geopolitics.Domain.Games;

/// <summary>Where the current player is within their turn.</summary>
public enum TurnPhase
{
    /// <summary>Must place all reinforcements (round 2 onward) before doing anything else.</summary>
    Reinforce,

    /// <summary>May attack once, transfer soldiers, or end the turn.</summary>
    Action,

    /// <summary>Rolling dice in an attack; transfers are not allowed.</summary>
    AttackInProgress,

    /// <summary>Just conquered a country; may move extra soldiers into it.</summary>
    PostConquest,
}

public enum GameStatus
{
    InProgress,

    /// <summary>The human player conquered all ten countries.</summary>
    Won,

    /// <summary>The human player lost their last country.</summary>
    Lost,
}
