#if UNITY_EDITOR

using System;

namespace WorldGraphEditor
{
    [Serializable]
    public struct EdgeData : IEquatable<EdgeData>, IConnectionData
    {
        public TransitionType TransitionType;
        public bool IsFacingRight;
        public string FromPortGuid;
        public string ToPortGuid;
        public string EdgeGuid;
     
        public TransitionType GetTransitionType() => TransitionType;
        public bool GetIsFacingRight() => IsFacingRight;
        public string GetFromPortGuid() => FromPortGuid;
        public string GetToPortGuid() => ToPortGuid;
        
        public override string ToString()
        {
            return $"edge type: {TransitionType}, is facing right: {IsFacingRight}, input: {FromPortGuid}, output: {ToPortGuid}";
        }
        
        public bool Equals(EdgeData other)
        {
            return TransitionType == other.TransitionType && IsFacingRight == other.IsFacingRight && FromPortGuid == other.FromPortGuid && ToPortGuid == other.ToPortGuid && EdgeGuid == other.EdgeGuid;
        }

        public override bool Equals(object obj)
        {
            return obj is EdgeData other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine((int) TransitionType, IsFacingRight, FromPortGuid, ToPortGuid, EdgeGuid);
        }
        
        public static bool operator ==(EdgeData left, EdgeData right)
        {
            return left.Equals(right);
        }
        
        public static bool operator !=(EdgeData left, EdgeData right)
        {
            return !(left == right);
        }
    }
}

#endif