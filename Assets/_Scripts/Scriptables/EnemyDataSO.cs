using _Scripts.Combat.Data;
using UnityEngine;

namespace _Scripts.Scriptables
{
    /// <summary>
    /// Enemy behavior type.
    /// </summary>
    public enum EnemyBehaviorType
    {
        Melee,      // Close-range attacker
        Ranged,     // Keeps distance, uses ranged attacks
        Tank,       // High health, slow, heavy hits
        Agile,      // Fast, dodges frequently
        Boss        // Special AI patterns
    }

    /// <summary>
    /// Enemy configuration data.
    /// Contains base combat stats and AI-specific settings.
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyData", menuName = "Combat/Enemy Data", order = 3)]
    public class EnemyDataSO : ScriptableObject
    {
        [Header("Identity")]
        public string EnemyName = "New Enemy";

        [TextArea(2, 4)]
        public string Description;

        public EnemyBehaviorType BehaviorType = EnemyBehaviorType.Melee;

        [Header("Base Stats")]
        [Tooltip("Combat statistics for this enemy")]
        public CombatStats BaseStats = CombatStats.CreateDefaultEnemy();

        [Header("AI Detection")]
        [Range(3f, 30f)]
        [Tooltip("Range at which enemy detects player")]
        public float DetectionRange = 15f;

        [Range(1f, 5f)]
        [Tooltip("Range at which enemy starts attacking")]
        public float AttackRange = 2f;

        [Range(10f, 50f)]
        [Tooltip("Max distance before enemy gives up chase")]
        public float MaxChaseRange = 25f;

        [Range(0.1f, 5f)]
        [Tooltip("Time between detection checks in idle")]
        public float DetectionCheckInterval = 0.5f;

        [Header("AI Combat")]
        [Range(0.5f, 5f)]
        [Tooltip("Minimum time between attacks")]
        public float AttackCooldown = 2f;

        [Range(0.5f, 5f)]
        [Tooltip("Time spent circling before attacking")]
        public float CircleTime = 1.5f;

        [Range(0f, 1f)]
        [Tooltip("Chance to perform heavy attack instead of light")]
        public float HeavyAttackChance = 0.2f;

        [Range(0f, 1f)]
        [Tooltip("Chance to block incoming attack (boss enemies)")]
        public float BlockChance = 0f;

        [Range(0f, 1f)]
        [Tooltip("Chance to dodge after taking hit")]
        public float DodgeChance = 0f;

        [Header("Lock-On")]
        [Range(-10, 10)]
        [Tooltip("Priority for lock-on targeting (higher = more likely)")]
        public int LockOnPriority = 1;

        [Tooltip("Lock-on point offset from center")]
        public Vector3 LockOnOffset = new Vector3(0f, 1.2f, 0f);

        [Header("Visual")]
        [Tooltip("Enemy model prefab")]
        public GameObject ModelPrefab;

        [Tooltip("Animator controller for enemy")]
        public RuntimeAnimatorController AnimatorController;

        [Tooltip("Health bar prefab (world space)")]
        public GameObject HealthBarPrefab;

        [Header("Audio")]
        [Tooltip("Idle ambient sounds")]
        public AudioClip[] IdleSounds;

        [Tooltip("Alert/aggro sound")]
        public AudioClip AlertSound;

        [Tooltip("Attack grunt sounds")]
        public AudioClip[] AttackSounds;

        [Tooltip("Damage taken sounds")]
        public AudioClip[] HurtSounds;

        [Tooltip("Death sound")]
        public AudioClip DeathSound;

        [Header("Loot")]
        [Range(0, 10000)]
        [Tooltip("Currency dropped on death")]
        public int CurrencyDrop = 100;

        [Tooltip("Items that can drop from this enemy")]
        public DropTableEntry[] DropTable;

        /// <summary>
        /// Create runtime combat data from this configuration.
        /// </summary>
        public CombatRuntimeData CreateRuntimeData()
        {
            return new CombatRuntimeData(BaseStats);
        }

        /// <summary>
        /// Check if this enemy can use heavy attacks.
        /// </summary>
        public bool CanHeavyAttack()
        {
            return HeavyAttackChance > 0 && Random.value < HeavyAttackChance;
        }

        /// <summary>
        /// Check if this enemy should block (if capable).
        /// </summary>
        public bool ShouldBlock()
        {
            return BlockChance > 0 && Random.value < BlockChance;
        }

        /// <summary>
        /// Check if this enemy should dodge.
        /// </summary>
        public bool ShouldDodge()
        {
            return DodgeChance > 0 && Random.value < DodgeChance;
        }
    }

    /// <summary>
    /// Entry in an enemy's drop table.
    /// </summary>
    [System.Serializable]
    public struct DropTableEntry
    {
        [Tooltip("Item identifier or prefab reference")]
        public string ItemId;

        [Range(0f, 1f)]
        [Tooltip("Chance to drop (0-1)")]
        public float DropChance;

        [Range(1, 99)]
        [Tooltip("Minimum quantity to drop")]
        public int MinQuantity;

        [Range(1, 99)]
        [Tooltip("Maximum quantity to drop")]
        public int MaxQuantity;
    }
}
