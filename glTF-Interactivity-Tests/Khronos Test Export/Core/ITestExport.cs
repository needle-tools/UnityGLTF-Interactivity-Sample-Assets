using System.Collections.Generic;

namespace Khronos_Test_Export
{
    public interface ITestCase
    {
        string GetTestName();

        string GetTestDescription();
        void PrepareObjects(TestContext context);
        void CreateNodes(TestContext context);
    }

    /// <summary>
    /// Implemented by test cases that cannot self-check without an external, simulated input
    /// (e.g. hovering/selecting a node) - as opposed to regular tests, which run and grade
    /// themselves purely from within the graph. An automated runner needs this structured
    /// description to know which node to target and what gesture to simulate, since it can't
    /// be derived from the human-readable test description alone.
    /// </summary>
    public interface IUserInteractionTestCase
    {
        IEnumerable<RequiredInteraction> GetRequiredInteractions();
    }

    /// <summary>
    /// One synthetic input an automated runner must perform for a <see cref="IUserInteractionTestCase"/>
    /// to be able to pass. Serialized into the test-Json oracle as "requiredInteractions".
    /// </summary>
    public class RequiredInteraction
    {
        /// <summary> The kind of gesture to simulate, e.g. "hover" or "select". </summary>
        public string type;

        /// <summary> "mustFire" if the corresponding event is expected to trigger, "mustNotFire" if it must not. </summary>
        public string expectation;

        /// <summary> Node index (in the exported glTF) the gesture must target. </summary>
        public int targetNodeId;

        /// <summary> Node name, for human-readable debugging. </summary>
        public string targetNodeName;

        /// <summary> Free-form clarification, e.g. "inherits hoverable=false from its parent". </summary>
        public string notes;
    }
}