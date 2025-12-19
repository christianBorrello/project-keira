namespace _Scripts.Enemies
{
    /// <summary>
    /// Enumeration of all enemy states.
    /// </summary>
    public enum EnemyState
    {
        None = 0,

        // Passive states
        Idle,
        Patrol,

        // Awareness states
        Alert,          // Noticed something
        Investigate,    // Moving to investigate

        // Combat states
        Chase,          // Pursuing target
        Attack,         // Performing attack
        Combo,          // Multi-hit attack sequence

        // Defensive states
        Block,          // Blocking stance
        Dodge,          // Evasive action
        CircleStrafe,   // Circling around target

        // Recovery states
        Stagger,        // Hit stagger
        Recover,        // Post-attack/stagger recovery
        Retreat,        // Backing off

        // Final state
        Death
    }
}
