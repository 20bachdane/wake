using System;
using System.Collections.Generic;
using BeauData;
using BeauUtil;
using BeauUtil.Debugger;

namespace Aqua.Profile
{
    public class InventoryData : IProfileChunk, ISerializedVersion, ISerializedCallbacks
    {
        private RingBuffer<PlayerInv> m_Items = new RingBuffer<PlayerInv>();
        private HashSet<StringHash32> m_ScannerIds = new HashSet<StringHash32>();
        private HashSet<StringHash32> m_UpgradeIds = new HashSet<StringHash32>();
        private uint m_WaterProperties = 0;

        [NonSerialized] private bool m_ItemListDirty = true;
        [NonSerialized] private bool m_HasChanges;

        #region Items

        public ListSlice<PlayerInv> Items()
        {
            CleanItemList();
            return m_Items;
        }

        public IEnumerable<PlayerInv> GetItems(InvItemCategory inCategory)
        {
            if (inCategory == InvItemCategory.Upgrade)
            {
                foreach(var upgrade in m_UpgradeIds)
                {
                    yield return new PlayerInv(upgrade, 1, Assets.Item(upgrade));
                }
            }
            else
            {
                var db = Services.Assets.Inventory;
                CleanItemList();
                foreach(var item in m_Items)
                {
                    InvItem desc = Assets.Item(item.ItemId);
                    if ((item.Count > 0 || db.IsAlwaysVisible(item.ItemId)) && desc.Category() == inCategory)
                        yield return item;
                }
            }
        }

        public int GetItems(InvItemCategory inCategory, ICollection<PlayerInv> outItems)
        {
            if (inCategory == InvItemCategory.Upgrade)
            {
                foreach(var upgrade in m_UpgradeIds)
                {
                    outItems.Add(new PlayerInv(upgrade, 1, Assets.Item(upgrade)));
                }
                return m_UpgradeIds.Count;
            }
            else
            {
                var db = Services.Assets.Inventory;
                int count = 0;
                foreach(var item in m_Items)
                {
                    InvItem desc = db.Get(item.ItemId);
                    if ((item.Count > 0 || db.IsAlwaysVisible(item.ItemId)) && desc.Category() == inCategory)
                    {
                        outItems.Add(item);
                        count++;
                    }
                }
                return count;
            }
        }

        public bool HasItem(StringHash32 inId)
        {
            PlayerInv item;
            return TryFindInv(inId, out item) && item.Count > 0;
        }

        public uint ItemCount(StringHash32 inId)
        {
            InvItem itemDesc = Assets.Item(inId);
            if (itemDesc.Category() == InvItemCategory.Upgrade)
            {
                return m_UpgradeIds.Contains(inId) ? 1u : 0u;
            }

            PlayerInv item;
            TryFindInv(inId, out item);
            return item.Count;
        }
        
        public bool AdjustItem(StringHash32 inId, int inAmount)
        {
            if (inAmount == 0)
                return true;

            ref PlayerInv item = ref RequireInv(inId);
            if (TryAdjust(ref item, inAmount))
            {
                m_HasChanges = true;
                return true;
            }

            return false;
        }

        public bool SetItem(StringHash32 inId, int inAmount)
        {
            ref PlayerInv item = ref RequireInv(inId);
            if (TrySet(ref item, inAmount))
            {
                m_HasChanges = true;
                return true;
            }

            return false;
        }

        public PlayerInv GetItem(StringHash32 inId)
        {
            PlayerInv inv;
            TryFindInv(inId, out inv);
            return inv;
        }

        private bool TryFindInv(StringHash32 inId, out PlayerInv outItem)
        {
            CleanItemList();

            Assert.True(Services.Assets.Inventory.HasId(inId), "Could not find ItemDesc with id '{0}'", inId);
            Assert.True(Services.Assets.Inventory.IsCountable(inId), "Item '{0}' is not countable", inId);

            if (!m_Items.TryBinarySearch(inId, out outItem))
            {
                outItem = new PlayerInv(inId, 0, Assets.Item(inId));
                return false;
            }

            return true;
        }

        private ref PlayerInv RequireInv(StringHash32 inId)
        {
            CleanItemList();

            Assert.True(Services.Assets.Inventory.HasId(inId), "Could not find ItemDesc with id '{0}'", inId);
            Assert.True(Services.Assets.Inventory.IsCountable(inId), "Item '{0}' is not countable", inId);

            int index = m_Items.BinarySearch(inId);
            if (index < 0)
            {
                index = m_Items.Count;
                m_Items.PushBack(new PlayerInv(inId, 0, Assets.Item(inId)));
                m_ItemListDirty = true;
                m_HasChanges = true;
            }
            
            return ref m_Items[index];
        }

        private void CleanItemList()
        {
            if (!m_ItemListDirty)
                return;

            m_Items.SortByKey<StringHash32, PlayerInv, PlayerInv>();
            m_ItemListDirty = false;
        }

        private bool TryAdjust(ref PlayerInv ioItem, int inValue)
        {
            if (inValue == 0 || (ioItem.Count + inValue) < 0)
                return false;

            ioItem.Count = (uint) (ioItem.Count + inValue);
            Services.Events.QueueForDispatch(GameEvents.InventoryUpdated, ioItem.ItemId);
            return true;
        }

        private bool TrySet(ref PlayerInv ioItem, int inValue)
        {
            if (inValue < 0)
            {
                inValue = 0;
            }

            if (ioItem.Count != inValue)
            {
                ioItem.Count = (uint) inValue;
                Services.Events.QueueForDispatch(GameEvents.InventoryUpdated, ioItem.ItemId);
                return true;
            }

            return false;
        }

        #endregion // Inventory

        #region Scanner

        public bool WasScanned(StringHash32 inId)
        {
            return m_ScannerIds.Contains(inId);
        }

        public bool RegisterScanned(StringHash32 inId)
        {
            if (m_ScannerIds.Add(inId))
            {
                m_HasChanges = true;
                Services.Events.QueueForDispatch(GameEvents.ScanLogUpdated, inId);
                return true;
            }

            return false;
        }

        #endregion // Scanner

        #region Upgrades

        public bool HasUpgrade(StringHash32 inUpgradeId)
        {
            Assert.True(Services.Assets.Inventory.HasId(inUpgradeId), "Could not find ItemDesc with id '{0}'", inUpgradeId);
            return m_UpgradeIds.Contains(inUpgradeId);
        }

        public bool AddUpgrade(StringHash32 inUpgradeId)
        {
            Assert.True(Services.Assets.Inventory.HasId(inUpgradeId), "Could not find ItemDesc with id '{0}'", inUpgradeId);
            if (m_UpgradeIds.Add(inUpgradeId))
            {
                m_HasChanges = true;
                Services.Events.QueueForDispatch(GameEvents.InventoryUpdated, inUpgradeId);
                return true;
            }

            return false;
        }

        public bool RemoveUpgrade(StringHash32 inUpgradeId)
        {
            Assert.True(Services.Assets.Inventory.HasId(inUpgradeId), "Could not find ItemDesc with id '{0}'", inUpgradeId);
            if (m_UpgradeIds.Remove(inUpgradeId))
            {
                m_HasChanges = true;
                Services.Events.QueueForDispatch(GameEvents.InventoryUpdated, inUpgradeId);
                return true;
            }

            return false;
        }

        #endregion // Upgrades

        #region Water Properties

        public bool IsPropertyUnlocked(WaterPropertyId inId)
        {
            return Bits.Contains(m_WaterProperties, (int) inId);
        }

        public WaterPropertyMask GetPropertyUnlockedMask()
        {
            return new WaterPropertyMask((byte) m_WaterProperties);
        }

        public bool UnlockProperty(WaterPropertyId inId)
        {
            if (!Bits.Contains(m_WaterProperties, (int) inId))
            {
                Bits.Add(ref m_WaterProperties, (int) inId);
                m_HasChanges = true;
                Services.Events.QueueForDispatch(GameEvents.WaterPropertiesUpdated, inId);
                return true;
            }

            return false;
        }

        public bool LockProperty(WaterPropertyId inId)
        {
            if (Bits.Contains(m_WaterProperties, (int) inId))
            {
                Bits.Remove(ref m_WaterProperties, (int) inId);
                m_HasChanges = true;
                Services.Events.QueueForDispatch(GameEvents.WaterPropertiesUpdated, inId);
                return true;
            }

            return false;
        }

        #endregion // Water Properties

        #region IProfileChunk

        public void SetDefaults()
        {
            foreach(var item in Services.Assets.Inventory.Objects)
            {
                if (item.DefaultAmount() > 0)
                {
                    ref PlayerInv playerInv = ref RequireInv(item.Id());
                    playerInv.Count = item.DefaultAmount();
                }
            }

            foreach(var property in Services.Assets.WaterProp.DefaultUnlocked())
            {
                m_WaterProperties |= (1U << (int) property);
            }
        }

        ushort ISerializedVersion.Version { get { return 2; } }

        void ISerializedObject.Serialize(Serializer ioSerializer)
        {
            ioSerializer.ObjectArray("items", ref m_Items);
            ioSerializer.UInt32ProxySet("scannerIds", ref m_ScannerIds);
            ioSerializer.UInt32ProxySet("upgradeIds", ref m_UpgradeIds);
            if (ioSerializer.ObjectVersion >= 2)
            {
                ioSerializer.Serialize("waterProps", ref m_WaterProperties);
            }
            else
            {
                foreach(var property in Services.Assets.WaterProp.DefaultUnlocked())
                {
                    m_WaterProperties |= (1U << (int) property);
                }
            }
        }

        void ISerializedCallbacks.PostSerialize(Serializer.Mode inMode, ISerializerContext inContext)
        {
            for(int i = 0; i < m_Items.Count; i++)
            {
                ref PlayerInv inv = ref m_Items[i];
                inv.Descriptor = Assets.Item(inv.ItemId);
            }
        }

        public bool HasChanges()
        {
            return m_HasChanges;
        }

        public void MarkChangesPersisted()
        {
            m_HasChanges = false;
        }

        #endregion // IProfileChunk
    }
}