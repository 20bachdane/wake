using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using BeauUtil;
using BeauUtil.Variants;
using BeauPools;

namespace Aqua
{
    public class FactSentenceDisplay : MonoBehaviour
    {
        [Serializable]
        public class Pool : SerializablePool<FactSentenceDisplay> { }

        #region Inspector

        [SerializeField] private bool m_DisplayOptionalFragments = false;
        [SerializeField] private bool m_InteractiveFragments = false;
        [SerializeField] private LayoutGroup m_Layout = null;

        #endregion // Inspector

        [NonSerialized] private List<FactSentenceFragment> m_AllocatedFragments = new List<FactSentenceFragment>();
        [NonSerialized] private FactSentenceTweaks m_Tweaks = null;

        public void Clear()
        {
            foreach(var frag in m_AllocatedFragments)
            {
                frag.Recycle();
            }

            m_AllocatedFragments.Clear();
        }

        public void Populate(BestiaryFactBase inFact, PlayerFactParams inFactParams)
        {
            if (!m_Tweaks)
            {
                m_Tweaks = Services.Tweaks.Get<FactSentenceTweaks>();
            }

            Clear();

            foreach(var fragment in inFact.GenerateFragments(inFactParams))
            {
                TryAllocFragment(fragment, inFactParams);
            }

            m_Layout.ForceRebuild();
        }

        private bool TryAllocFragment(in BestiaryFactFragment inFragment, PlayerFactParams inFactParams)
        {
            StringSlice str = inFragment.String;

            if (!m_DisplayOptionalFragments)
            {
                switch(inFragment.Word)
                {
                    case BestiaryFactFragmentWord.ConditionOperand:
                    case BestiaryFactFragmentWord.ConditionOperator:
                    case BestiaryFactFragmentWord.ConditionQuality:
                        {
                            if (inFactParams == null || inFactParams.ConditionData.Id.IsEmpty)
                                return false;

                            break;
                        }

                    case BestiaryFactFragmentWord.Amount:
                        {
                            if (inFactParams == null ||inFactParams.Value.StrictEquals(Variant.Null))
                                return false;

                            break;
                        }

                    case BestiaryFactFragmentWord.SubjectVariant:
                        {
                            if (inFactParams == null ||inFactParams.SubjectVariantId.IsEmpty)
                                return false;

                            break;
                        }

                    case BestiaryFactFragmentWord.TargetVariant:
                        {
                            if (inFactParams == null || inFactParams.TargetVariantId.IsEmpty)
                                return false;

                            break;
                        }
                }
            }

            FactSentenceFragment fragment = m_Tweaks.Alloc(inFragment, m_Layout.transform, m_InteractiveFragments);
            fragment.Configure(inFragment.String);
            m_AllocatedFragments.Add(fragment);

            // TODO: Any other logic for setting up interactive parts
            return true;
        }
    }
}