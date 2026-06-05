using System;

namespace WorldGraphEditor
{
    [Serializable]
    public struct TestResult
    {
        public string Description;
        public TestStatusType TestStatusType;

        public TestResult(TestStatusType statusType, string description)
        {
            Description = description;
            TestStatusType = statusType;
        }
    }
}