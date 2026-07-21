#if UNITY_EDITOR

using System;

namespace WorldGraphEditor
{
    [Serializable]
    public struct PortData : IEquatable<PortData>, IPortData
    {
        public string Name;
        public string Guid;
        public bool IsInput;
        public bool IsHorizontal;
        public bool IsAdditional;
        
        public string GetGuid() => Guid;

        public bool GetIsAdditional() => IsAdditional;

        public bool Equals(PortData other)
        {
            return Name == other.Name && Guid == other.Guid && IsInput == other.IsInput && IsHorizontal == other.IsHorizontal && IsAdditional == other.IsAdditional;
        }
        
        public override bool Equals(object obj)
        {
            return obj is PortData other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Name, Guid, IsInput, IsHorizontal, IsAdditional);
        }
        
        public static bool operator ==(PortData left, PortData right)
        {
            return left.Equals(right);
        }
        
        public static bool operator !=(PortData left, PortData right)
        {
            return !(left == right);
        }
    }
}

#endif