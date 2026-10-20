using System;

namespace Khronos_Test_Export
{
    /// <summary>
    /// Marks a test case for behaviour the KHR_interactivity specification leaves undefined or describes contradictorily.
    /// These cases don't have one correct result: each possible reading gets its own check, so the passing check shows
    /// which reading an engine implements. They are not part of the regular test list and are exported separately with
    /// "Sample Scenes/Export Khronos Undefined Behaviour Tests" (see <see cref="UndefinedBehaviourTestExport"/>).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class UndefinedBehaviourTestAttribute : Attribute
    {
    }
}
