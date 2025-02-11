using System;
using BeauData;
using BeauUtil;

namespace Aqua
{
    [Flags]
    public enum BFDiscoveredFlags : byte
    {
        Base = 0x01,
        Rate = 0x02,

        [Hidden]
        All = Base | Rate,
        [Hidden]
        None = 0,
        [Hidden]
        HasPair = 0x04,
        [Hidden]
        IsEncrypted = 0x80
    }
}