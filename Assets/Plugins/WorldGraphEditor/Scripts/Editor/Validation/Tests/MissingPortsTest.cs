using System.Linq;

namespace WorldGraphEditor.Editor.Tests
{
    internal struct MissingPortsTest : ISceneTest
    {
        public TestResult Run(in TestContext context)
        {
            var ports = ObjectUtility.FindObjectsByInterface<ITransitionComponent>();
            var missing = context.SceneNodeData.PortsData
                .Where(data => ports.All(port => port.GetGuid() != data.Guid));

            if (missing.Any())
                return new TestResult(TestStatusType.Warning, "Scene has missing ports.");
            
            return new TestResult(TestStatusType.Passed, "No missing ports found.");
        }
    }
}