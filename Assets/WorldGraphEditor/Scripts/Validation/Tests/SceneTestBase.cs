#if UNITY_EDITOR
using UnityEngine;

namespace WorldGraphEditor
{
    public abstract class SceneTestBase : ScriptableObject, ISceneTest
    {
        public virtual void Init() { }

        public abstract TestResult Run(in TestContext context);
    }
}
#endif