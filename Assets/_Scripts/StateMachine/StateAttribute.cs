using System;

namespace _Scripts.StateMachine
{
    /// <summary>
    /// Attribute to mark a class as a state that should be auto-registered.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class RegisterStateAttribute : Attribute
    {
    }
}
