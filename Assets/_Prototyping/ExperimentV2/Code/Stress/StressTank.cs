using System;
using Aqua;
using Aqua.Profile;
using BeauRoutine;
using BeauUtil;
using BeauUtil.Debugger;
using UnityEngine;

namespace ProtoAqua.ExperimentV2
{
    public class StressTank : MonoBehaviour, ISceneOptimizable
    {
        #region Inspector

        [SerializeField, Required(ComponentLookupDirection.Self)] private SelectableTank m_ParentTank = null;
        
        [SerializeField, Required] private BestiaryAddPanel m_AddCrittersPanel = null;
        [SerializeField, Required] private CanvasGroup m_WaterPropertyGroup = null;

        [SerializeField] private ActorAllocator m_Allocator = null;

        #endregion // Inspector

        [SerializeField, HideInInspector] private WaterPropertyDial[] m_Dials;

        [NonSerialized] private BestiaryDesc m_SelectedCritter;
        [NonSerialized] private ActorStateTransitionSet m_CritterTransitions;

        [NonSerialized] private ActorInstance m_SelectedCritterInstance;
        [NonSerialized] private ActorWorld m_World;

        [NonSerialized] private Routine m_DialFadeAnim;

        [NonSerialized] private int m_DialsUsed = 0;
        [NonSerialized] private WaterPropertyDial.ValueChangedDelegate m_DialChangedDelegate;
        [NonSerialized] private WaterPropertyDial.ReleasedDelegate m_DialReleasedDelegate;
        [NonSerialized] private WaterPropertyDial[] m_DialMap = new WaterPropertyDial[(int) WaterPropertyId.TRACKED_COUNT];
        [NonSerialized] private bool m_DialsDirty = true;

        [NonSerialized] private WaterPropertyMask m_RevealedLeftMask;
        [NonSerialized] private WaterPropertyMask m_RevealedRightMask;
        [NonSerialized] private WaterPropertyMask m_RequiredReveals;
        [NonSerialized] private WaterPropertyMask m_VisiblePropertiesMask;

        private void Awake()
        {
            m_ParentTank.ActivateMethod = Activate;
            m_ParentTank.DeactivateMethod = Deactivate;

            m_AddCrittersPanel.OnAdded = OnCritterAdded;
            m_AddCrittersPanel.OnRemoved = OnCritterRemoved;
            m_AddCrittersPanel.OnCleared = OnCrittersCleared;

            Services.Events.Register(GameEvents.WaterPropertiesUpdated, RebuildPropertyDials);
        }

        private void OnDestroy()
        {
            Services.Events?.DeregisterAll(this);
        }

        #region Tank

        private void Activate()
        {
            if (m_World == null)
            {
                m_World = new ActorWorld(m_Allocator, m_ParentTank.Bounds, null, null, 1);
            }

            m_WaterPropertyGroup.alpha = 0;
            m_WaterPropertyGroup.gameObject.SetActive(false);

            if (m_DialsDirty)
            {
                RebuildPropertyDials();
            }
        }

        private void Deactivate()
        {
            m_AddCrittersPanel.Hide();
            m_AddCrittersPanel.ClearSelection();

            m_RevealedLeftMask = default;
            m_RevealedRightMask = default;
        }

        #endregion // Tank

        private void CheckForAllRangesFound()
        {
            if (m_RequiredReveals.Mask == 0)
                return;

            if (m_RevealedLeftMask != m_RevealedRightMask || m_RevealedLeftMask != m_RequiredReveals)
                return;

            BFState state;
            BestiaryData saveData = Services.Data.Profile.Bestiary;
            foreach(WaterPropertyId id in m_RequiredReveals)
            {
                state = BestiaryUtils.FindStateRangeRule(m_SelectedCritter, id);
                Assert.NotNull(state, "No BFState {0} fact found for critter {1}", id, m_SelectedCritter.Id());
                saveData.RegisterFact(state.Id());
            }

            m_AddCrittersPanel.ClearSelection();
        }

        #region Critter Callbacks

        private void OnCritterAdded(BestiaryDesc inDesc)
        {
            if (Ref.Replace(ref m_SelectedCritter, inDesc))
            {
                if (m_SelectedCritterInstance != null)
                {
                    ActorWorld.Free(m_World, ref m_SelectedCritterInstance);
                }

                m_DialFadeAnim.Replace(this, m_WaterPropertyGroup.Show(0.1f, true));
                m_CritterTransitions = m_SelectedCritter.GetActorStateTransitions();
                ResetWaterPropertiesForCritter(Services.Data.Profile.Bestiary);

                m_SelectedCritterInstance = m_Allocator.Alloc(inDesc.Id(), null);

                m_AddCrittersPanel.Hide();
            }
        }

        private void OnCritterRemoved(BestiaryDesc inDesc)
        {
            if (Ref.CompareExchange(ref m_SelectedCritter, inDesc, null))
            {
                ActorWorld.Free(m_World, ref m_SelectedCritterInstance);
                m_DialFadeAnim.Replace(this, m_WaterPropertyGroup.Hide(0.1f, true));
            }
        }

        private void OnCrittersCleared()
        {
            m_SelectedCritter = null;
            ActorWorld.Free(m_World, ref m_SelectedCritterInstance);
            m_DialFadeAnim.Replace(this, m_WaterPropertyGroup.Hide(0.1f, true));
        }

        #endregion // Critter Callbacks

        #region Property Callbacks

        private void OnPropertyChanged(WaterPropertyId inId, float inValue)
        {
            m_World.Water[inId] = inValue;
            ActorStateTransitionRange range = m_CritterTransitions[inId];
            WaterPropertyDesc property = Services.Assets.WaterProp.Property(inId);
            
            bool bHasMin = !float.IsInfinity(range.AliveMin);
            bool bHasMax = !float.IsInfinity(range.AliveMax);
            float min = bHasMin ? range.AliveMin : property.MinValue();
            float max = bHasMax ? range.AliveMax : property.MaxValue();

            if (inValue <= min)
            {
                inValue = min;
                m_DialMap[(int) inId].SetValue(inValue);

                if (bHasMin)
                {
                    ActorInstance.SetActorState(m_SelectedCritterInstance, ActorStateId.Stressed, m_World);
                }

                if (!m_RevealedLeftMask[inId])
                {
                    m_RevealedLeftMask[inId] = true;
                    WaterPropertyDial.DisplayRanges(m_DialMap[(int) inId], true, m_RevealedRightMask[inId]);
                    Services.Audio.PostEvent("experiment.stress.reveal");
                    CheckForAllRangesFound();
                }
            }
            else if (inValue >= max)
            {
                inValue = max;
                m_DialMap[(int) inId].SetValue(inValue);

                if (bHasMax)
                {
                    ActorInstance.SetActorState(m_SelectedCritterInstance, ActorStateId.Stressed, m_World);
                }

                if (!m_RevealedRightMask[inId])
                {
                    m_RevealedRightMask[inId] = true;
                    WaterPropertyDial.DisplayRanges(m_DialMap[(int) inId], m_RevealedLeftMask[inId], true);
                    Services.Audio.PostEvent("experiment.stress.reveal");
                    CheckForAllRangesFound();
                }
            }
            else
            {
                ActorInstance.SetActorState(m_SelectedCritterInstance, ActorStateId.Alive, m_World);
            }
        }

        private void OnPropertyReleased(WaterPropertyId inId)
        {
            if (m_SelectedCritter == null)
                return;
            
            float value = m_World.Water[inId];
            ActorStateTransitionRange range = m_CritterTransitions[inId];
            if (value < range.AliveMin)
            {
                m_World.Water[inId] = range.AliveMin;
                m_DialMap[(int) inId].SetValue(range.AliveMin);
            }
            else if (value > range.AliveMax)
            {
                m_World.Water[inId] = range.AliveMax;
                m_DialMap[(int) inId].SetValue(range.AliveMax);
            }
            ActorInstance.SetActorState(m_SelectedCritterInstance, ActorStateId.Alive, m_World);
        }

        #endregion // Property Callbacks

        #region Dials

        private void RebuildPropertyDials()
        {
            if (!isActiveAndEnabled)
            {
                m_DialsDirty = true;
                return;
            }

            Assert.True(m_Dials.Length == (int) WaterPropertyId.TRACKED_COUNT, "{0} dials to handle {1} properties", m_Dials.Length, (int) WaterPropertyId.TRACKED_COUNT);
            Array.Clear(m_DialMap, 0, m_DialMap.Length);

            m_DialsUsed = 0;
            m_VisiblePropertiesMask = default;

            WaterPropertyDial dial;
            foreach(var property in Services.Assets.WaterProp.Sorted())
            {
                if (!Services.Data.Profile.Inventory.IsPropertyUnlocked(property.Index()))
                    continue;

                dial = m_Dials[m_DialsUsed++];
                dial.Property = property;
                dial.Label.SetText(property.LabelId());
                dial.OnChanged = m_DialChangedDelegate ?? (m_DialChangedDelegate = OnPropertyChanged);
                dial.OnReleased = m_DialReleasedDelegate ?? (m_DialReleasedDelegate = OnPropertyReleased);

                m_DialMap[(int) property.Index()] = dial;
                m_VisiblePropertiesMask[property.Index()] = true;

                dial.gameObject.SetActive(true);
            }

            for(int i = m_DialsUsed; i < m_Dials.Length; i++)
            {
                m_Dials[i].gameObject.SetActive(false);
            }

            m_DialsDirty = false;
        }

        private void ResetWaterPropertiesForCritter(BestiaryData inSaveData)
        {
            m_RevealedLeftMask = default;
            m_RevealedRightMask = default;
            m_RequiredReveals = default;

            m_World.Water = BestiaryUtils.FindHealthyWaterValues(m_CritterTransitions, Services.Assets.WaterProp.DefaultValues());
            WaterPropertyDial dial;
            BFState state;
            WaterPropertyId propId;
            for(int i = 0; i < m_DialsUsed; i++)
            {
                dial = m_Dials[i];
                propId = dial.Property.Index();
                dial.SetValue(m_World.Water[propId]);

                state = BestiaryUtils.FindStateRangeRule(m_SelectedCritter, propId);
                if (state != null)
                {
                    WaterPropertyDial.ConfigureStress(dial, state.Range());
                    if (inSaveData.HasFact(state.Id()))
                    {
                        m_RevealedLeftMask[propId] = m_RevealedRightMask[propId] = true;
                        WaterPropertyDial.DisplayRanges(dial, true, true);
                    }
                    else
                    {
                        m_RequiredReveals[propId] = true;
                        WaterPropertyDial.DisplayRanges(dial, false, false);
                    }
                }
                else
                {
                    WaterPropertyDial.ConfigureStress(dial, ActorStateTransitionRange.Default);
                    WaterPropertyDial.DisplayRanges(dial, false, false);
                }
            }
        }
        
        #endregion // Dials

        #if UNITY_EDITOR

        void ISceneOptimizable.Optimize()
        {
            m_Allocator = FindObjectOfType<ActorAllocator>();
            m_Dials = GetComponentsInChildren<WaterPropertyDial>(true);
        }

        #endif // UNITY_EDITOR
    }
}