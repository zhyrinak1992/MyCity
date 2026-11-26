using System;

namespace MyCity.Core
{
    [Serializable]
    public struct StableId
    {
        public string Value;

        public StableId(string value) { Value = value; }

        public static StableId NewId()
        {
            return new StableId(Guid.NewGuid().ToString("N"));
        }

        public bool IsValid { get { return !string.IsNullOrEmpty(Value); } }

        public override string ToString() { return Value ?? string.Empty; }
    }
}
