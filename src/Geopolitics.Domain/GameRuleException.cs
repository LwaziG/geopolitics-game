namespace Geopolitics.Domain;

/// <summary>
/// A move broke a game rule (wrong turn, wrong phase, not enough soldiers, ...).
/// The message is written for the player and is safe to show in the UI.
/// </summary>
public sealed class GameRuleException(string message) : Exception(message);
