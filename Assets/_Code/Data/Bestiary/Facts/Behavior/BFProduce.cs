using System;
using System.Collections.Generic;
using BeauPools;
using BeauUtil;
using BeauUtil.Debugger;
using ScriptableBake;
using UnityEngine;
using UnityEngine.Serialization;

namespace Aqua
{
    [CreateAssetMenu(menuName = "Aqualab Content/Fact/Produce")]
    public class BFProduce : BFBehavior
    {
        #region Inspector

        [Header("Produce")]
        [AutoEnum] public WaterPropertyId Property = WaterPropertyId.Oxygen;
        public float Amount = 0;
        [SerializeField, HideInInspector] private QualCompare m_Relative;

        #endregion // Inspector

        private BFProduce() : base(BFTypeId.Produce) { }

        #region Behavior

        static public readonly TextId ProduceVerb = "words.produce";
        static private readonly TextId ProduceSentence = "factFormat.produce";
        static private readonly TextId ProduceSentenceStressed = "factFormat.produce.stressed";

        static public void Configure()
        {
            BFType.DefineAttributes(BFTypeId.Produce, BFShapeId.Behavior, BFFlags.IsBehavior, BFDiscoveredFlags.All, Compare);
            BFType.DefineMethods(BFTypeId.Produce, null, GenerateDetails, GenerateFragments, null, (f) => ((BFProduce) f).Property);
            BFType.DefineEditor(BFTypeId.Produce, DefaultIcon, BFMode.Player);
        }

        static private IEnumerable<BFFragment> GenerateFragments(BFBase inFact, BestiaryDesc inReference, BFDiscoveredFlags inFlags)
        {
            BFProduce fact = (BFProduce) inFact;

            yield return BFFragment.CreateLocNoun(fact.Parent.CommonName());
            yield return BFFragment.CreateLocVerb(ProduceVerb);
            if (fact.OnlyWhenStressed)
            {
                yield return BFFragment.CreateLocAdjective(QualitativeLowerId(fact.m_Relative));
            }
            yield return BFFragment.CreateLocNoun(BestiaryUtils.Property(fact.Property).LabelId());
        }

        static private BFDetails GenerateDetails(BFBase inFact, BFDiscoveredFlags inFlags)
        {
            BFProduce fact = (BFProduce) inFact;
            WaterPropertyDesc property = BestiaryUtils.Property(fact.Property);

            BFDetails details;
            details.Header = Loc.Find(DetailsHeader);
            details.Image = property.ImageSet();

            if (fact.OnlyWhenStressed)
            {
                details.Description = Loc.Format(ProduceSentenceStressed, inFact.Parent.CommonName(), QualitativeLowerId(fact.m_Relative), BestiaryUtils.Property(fact.Property).LabelId());
            }
            else
            {
                details.Description = Loc.Format(ProduceSentence, inFact.Parent.CommonName(), BestiaryUtils.Property(fact.Property).LabelId());
            }

            return details;
        }

        static private int Compare(BFBase x, BFBase y)
        {
            int propCompare = WaterPropertyDB.SortByVisualOrder(((BFProduce) x).Property, ((BFProduce) y).Property);
            if (propCompare != 0)
                return propCompare;

            return CompareStressedPair(x, y);
        }

        static private Sprite DefaultIcon(BFBase inFact)
        {
            BFProduce fact = (BFProduce) inFact;
            return BestiaryUtils.Property(fact.Property).Icon();
        }

        #endregion // Behavior

        #if UNITY_EDITOR

        protected override bool IsPair(BFBehavior inOther)
        {
            BFProduce produce = inOther as BFProduce;
            return produce != null && produce.Property == Property;
        }

        public override bool Bake(BakeFlags flags)
        {
            if (OnlyWhenStressed)
            {
                var pair = FindPairedFact<BFProduce>();
                if (pair != null)
                {
                    long compare = (long) Amount - (long) pair.Amount;
                    return Ref.Replace(ref m_Relative, MapDescriptor(compare, QualCompare.Less, QualCompare.More, QualCompare.SameAmount));
                }
            }

            return Ref.Replace(ref m_Relative, QualCompare.Null);
        }

        #endif // UNITY_EDITOR
    }
}