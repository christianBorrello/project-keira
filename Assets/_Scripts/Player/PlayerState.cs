namespace _Scripts.Player
{
    /// <summary>
    /// All possible player states for the FSM.
    /// Visible in Inspector for debugging.
    /// </summary>
    public enum PlayerState
    {
        None = 0,

        // Locomotion
        Idle = 10,
        Walk = 11,
        Run = 12,
        Sprint = 13,

        // Combat - Offensive
        LightAttack = 20,
        HeavyAttack = 21,

        // Combat - Defensive
        Block = 29,    // Passive block with initial parry window
        Parry = 30,
        Deflect = 31,  // Successful parry reaction
        Dodge = 32,

        // Reactions
        Stagger = 40,
        Death = 50
    }
}
