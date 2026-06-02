using System;
using System.Collections.Generic;
using System.Linq;

namespace WorldGraphEditor.Editor.Tests
{
    [Serializable]
    internal struct SCCGroup
    {
        public int SCCIndex;
        public SceneTestResult[] TestResults;

        public SCCGroup(int sccIndex, IEnumerable<SceneTestResult> sccGroup)
        {
            SCCIndex = sccIndex;
            TestResults = sccGroup.ToArray();
        }
    }
}