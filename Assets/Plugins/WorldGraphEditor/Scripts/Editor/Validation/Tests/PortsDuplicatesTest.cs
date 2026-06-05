using System.Linq;

namespace WorldGraphEditor.Editor.Tests
{
    internal struct PortsDuplicatesTest : ISceneTest
    {
        public TestResult Run(in TestContext _)
        {
            var ports = ObjectUtility.FindObjectsByInterface<ITransitionComponent>();
            var duplicates = ports
                .GroupBy(item => item.GetGuid())
                .Where(group => group.Count() > 1)
                .SelectMany(group => group);

            if (duplicates.Any())
                return new TestResult(TestStatusType.Error, "Scene contains duplicate ports.");

            return new TestResult(TestStatusType.Passed, "No duplicate ports found.");
        }
    }
}