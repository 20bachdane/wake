using System;
using BeauUtil;
using UnityEngine;
using UnityEngine.Serialization;
using EasyAssetStreaming;

#if UNITY_EDITOR
using UnityEditor;
#endif // UNITY_EDITOR

namespace Aqua {
    [CreateAssetMenu(menuName = "Aqualab Content/Inventory Item", fileName = "NewInvItem")]
    public class InvItem : DBObject, IEditorOnlyData {
        #region Inspector

        [SerializeField, AutoEnum] private InvItemCategory m_Category = InvItemCategory.Currency;
        [SerializeField, AutoEnum] private InvItemFlags m_Flags = InvItemFlags.None;

        [Header("Text")]
        [SerializeField] private TextId m_NameTextId = default;
        [SerializeField] private TextId m_PluralNameTextId = default;
        [SerializeField] private TextId m_DescriptionTextId = default;

        [Header("Value")]
        [SerializeField] private uint m_Default = 0;
        [SerializeField, FormerlySerializedAs("m_BuyCoinsValue")] private uint m_CashCost = 0;
        [SerializeField] private uint m_RequiredLevel = 1;
        [SerializeField] private InvItem m_Prerequisite = null;

        [Header("Assets")]
        [SerializeField] private Sprite m_Icon = null;
        [SerializeField, StreamingImagePath] private string m_SketchPath = null;
        [SerializeField] private Color m_Color = Color.white;

        [Header("Sorting")]
        [SerializeField, AutoEnum] private InvItemSubCategory m_SubCategory = InvItemSubCategory.None;
        [SerializeField] private int m_SortingOrder = 0;

        #endregion

        public InvItemCategory Category() { return m_Category; }
        public InvItemSubCategory SubCategory() { return m_SubCategory; }
        public InvItemFlags Flags() { return m_Flags; }

        public bool HasFlags(InvItemFlags inFlags) { return (m_Flags & inFlags) != 0; }
        public bool HasAllFlags(InvItemFlags inFlags) { return (m_Flags & inFlags) == inFlags; }

        public InvItem Prerequisite() { return m_Prerequisite; }

        [LeafLookup("Name")] public TextId NameTextId() { return m_NameTextId; }
        [LeafLookup("PluralName")] public TextId PluralNameTextId() { return m_PluralNameTextId.IsEmpty ? m_NameTextId : m_PluralNameTextId; }
        public TextId DescriptionTextId() { return m_DescriptionTextId; }

        public Sprite Icon() { return m_Icon; }
        public string SketchPath() { return m_SketchPath; }
        public StreamedImageSet ImageSet() { return new StreamedImageSet(m_SketchPath, m_Icon); }

        public uint DefaultAmount() { return m_Default; }

        [LeafLookup("Cost")] public int CashCost() { return (int) m_CashCost; }
        [LeafLookup("Level")] public int RequiredLevel() { return (int) m_RequiredLevel; }

        #region Sorting

        static public readonly Comparison<InvItem> SortByCategoryAndOrder = (x, y) => {
            int categoryOrder = x.m_Category.CompareTo(y.m_Category);
            if (categoryOrder != 0)
                return categoryOrder;

            int subcategoryOrder = x.m_SubCategory.CompareTo(y.m_SubCategory);
            if (subcategoryOrder != 0)
                return subcategoryOrder;

            int orderOrder = x.m_SortingOrder.CompareTo(y.m_SortingOrder);
            if (orderOrder != 0)
                return orderOrder;

            return x.Id().CompareTo(y.Id());
        };

        #endregion // Sorting

        #if UNITY_EDITOR

        // [CustomEditor(typeof(InvItem)), CanEditMultipleObjects]
        private class Inspector : Editor {
            private SerializedProperty m_CategoryProperty;
            private SerializedProperty m_FlagsProperty;

            private void OnEnable() {
                m_CategoryProperty = serializedObject.FindProperty("m_Category");
                m_FlagsProperty = serializedObject.FindProperty("m_Flags");
            }

            public override void OnInspectorGUI() {
                serializedObject.UpdateIfRequiredOrScript();

                EditorGUILayout.PropertyField(m_CategoryProperty);
                EditorGUILayout.PropertyField(m_FlagsProperty);
                EditorGUILayout.Space();

                serializedObject.ApplyModifiedProperties();
            }
        }

        void IEditorOnlyData.ClearEditorOnlyData() {
            ValidationUtils.StripDebugInfo(ref m_NameTextId);
            ValidationUtils.StripDebugInfo(ref m_PluralNameTextId);
            ValidationUtils.StripDebugInfo(ref m_DescriptionTextId);
        }

        #endif // UNITY_EDITOR
    }

    public enum InvItemCategory : byte {
        Currency,
        Upgrade,
        Artifact
    }

    public enum InvItemSubCategory : byte {
        None,
        Ship,
        Submarine,
        Experimentation,
        Portable,
        Modeling
    }

    [Flags]
    public enum InvItemFlags : byte {
        [Hidden]
        None = 0x00,

        Hidden = 0x01,
        Sellable = 0x02,
        AlwaysDisplay = 0x4,
        Buyable = 0x8,
        OnlyOne = 0x10,
        SkipPopup = 0x20
    }

    public class ItemIdAttribute : DBObjectIdAttribute {
        public InvItemCategory? Category;

        public ItemIdAttribute() : base(typeof(InvItem)) {
            Category = null;
        }

        public ItemIdAttribute(InvItemCategory inCategory) : base(typeof(InvItem)) {
            Category = inCategory;
        }

        public override bool Filter(DBObject inObject) {
            if (Category.HasValue) {
                InvItem item = (InvItem)inObject;
                return item.Category() == Category.Value;
            } else {
                return true;
            }
        }

        public override string Name(DBObject inObject) {
            if (Category.HasValue) {
                return inObject.name;
            }

            InvItem desc = (InvItem)inObject;
            return string.Format("{0}/{1}", desc.Category().ToString(), desc.name);
        }
    }
}