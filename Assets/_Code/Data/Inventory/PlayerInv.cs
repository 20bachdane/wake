using BeauUtil;
using System;
using BeauData;

namespace Aqua {
    public struct PlayerInv : IKeyValuePair<StringHash32, PlayerInv>, ISerializedObject
    {
        public StringHash32 ItemId;
        public uint Count;

        [NonSerialized] public InvItem Item;

        #region KeyValue

        StringHash32 IKeyValuePair<StringHash32, PlayerInv>.Key { get { return ItemId; } }
        PlayerInv IKeyValuePair<StringHash32, PlayerInv>.Value { get { return this; } }

        #endregion // KeyValue

        public PlayerInv(StringHash32 inId, uint inCount, InvItem inDesc)
        {
            ItemId = inId;
            Count = inCount;
            Item = inDesc;
        }

        public void Serialize(Serializer ioSerializer)
        {
            ioSerializer.UInt32Proxy("id", ref ItemId);
            ioSerializer.Serialize("value", ref Count, 1);
        }
    }
    
}