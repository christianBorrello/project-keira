using System;
using UnityEngine;

namespace _Scripts.Scriptables
{
    /// <summary>
    /// Create a scriptable hero 
    /// </summary>
    [CreateAssetMenu(fileName = "New Scriptable Example")]
    public class ScriptableExampleHero : ScriptableExampleUnitBase {
        public ExampleHeroType HeroType;
 
    }

    [Serializable]
    public enum ExampleHeroType {
        Tarodev = 0,
        Snorlax = 1
    }
}