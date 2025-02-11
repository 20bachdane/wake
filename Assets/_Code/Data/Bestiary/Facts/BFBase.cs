using System;
using System.Collections.Generic;
using BeauUtil;
using BeauUtil.Debugger;
using UnityEngine;
using UnityEngine.Serialization;

namespace Aqua
{
    public abstract class BFBase : ScriptableObject, IEditorOnlyData
    {
        [Header("-- DEBUG DISPLAY --")]
        [SerializeField, HideInInspector] public StringHash32 Id;
        public readonly BFTypeId Type;
        
        [SerializeField, HideInEditor] public BestiaryDesc Parent;
        [SerializeField, HideInInspector] public Sprite Icon;
        [SerializeField, HideInInspector] public BFMode Mode;

        #region Inspector

        [SerializeField, FormerlySerializedAs("m_Icon")] private Sprite m_IconOverride = null;

        #endregion // Inspector

        protected BFBase(BFTypeId inType)
        {
            Type = inType;
        }

        #region Sorting

        /// <summary>
        /// Sorts facts by parent and type.
        /// </summary>
        static public readonly Comparison<BFBase> SortByParentAndType = (x, y) =>
        {
            int parentCompare = x.Parent.Id().CompareTo(y.Parent.Id());
            if (parentCompare != 0)
                return parentCompare;

            int typeCompare = x.Type.CompareTo(y.Type);
            if (typeCompare != 0)
                return typeCompare;

            return x.Id.CompareTo(y.Id);
        };

        /// <summary>
        /// Sorts facts by mode and id.
        /// </summary>
        static public readonly Comparison<BFBase> SortByMode = (x, y) =>
        {
            int modeCompare = x.Mode.CompareTo(y.Mode);
            if (modeCompare != 0)
                return modeCompare;

            return x.Id.CompareTo(y.Id);
        };

        #endregion // Sorting

        #region Initialization

        #if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        #endif // UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static private void ConfigureAll()
        {
            BFState.Configure();
            BFModel.Configure();
            BFWaterPropertyHistory.Configure();
            BFWaterProperty.Configure();
            BFPopulationHistory.Configure();
            BFPopulation.Configure();
            BFReproduce.Configure();
            BFProduce.Configure();
            BFGrow.Configure();
            BFEat.Configure();
            BFParasite.Configure();
            BFDeath.Configure();
            BFConsume.Configure();
            BFBody.Configure();
            BFSim.Configure();
        }

        #endregion // Initialization

        #region Editor

        #if UNITY_EDITOR

        internal void BakeProperties(BestiaryDesc inParent)
        {
            bool bChanged = Ref.Replace(ref Id, name);
            bChanged |= Ref.Replace(ref Parent, inParent);
            bChanged |= Ref.Replace(ref Icon, BFType.ResolveIcon(this, m_IconOverride));
            bChanged |= Ref.Replace(ref Mode, BFType.ResolveMode(this));

            if (bChanged)
            {
                UnityEditor.EditorUtility.SetDirty(this);
            }
        }

        protected virtual void OnValidate() { }

        protected virtual void Reset() { }

        public virtual void ClearEditorOnlyData()
        {
            m_IconOverride = null;
        }

        #endif // UNITY_EDITOR

        #endregion // Editor
    }

    public enum BFMode : byte
    {
        Player = 0,
        Always,
        Internal
    }

    public enum BFFlags : byte
    {
        IsGraphable = 0x01,
        IsBehavior = 0x02,
        HideFactInDetails = 0x04,
        EnvironmentFact = 0x08,
        SelfTarget = 0x10,
        HasRate = 0x20
    }

    public struct BFDetails
    {
        public string Header;
        public StreamedImageSet Image;
        public string Description;
    }

    public class FactIdAttribute : PropertyAttribute
    {
        public Type FactType;

        public FactIdAttribute(Type inFactType = null)
        {
            FactType = inFactType ?? typeof(BFBase);
        }
    }
}