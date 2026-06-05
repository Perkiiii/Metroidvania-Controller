#if UNITY_EDITOR
namespace WorldGraphEditor
{
    public interface ISceneTest
    {
        public virtual void Init() { }

        public TestResult Run(in TestContext context);
    }
}
#endif