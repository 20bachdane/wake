using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BeauPools;
using BeauRoutine;
using BeauUtil;
using BeauUtil.Debugger;
using BeauUtil.UI;
using UnityEngine;

namespace Aqua.Modeling {
    public class ModelWorldDisplay : MonoBehaviour, IScenePreloader {

        #region Types

        [Serializable] private class OrganismPool : SerializablePool<ModelOrganismDisplay> { }
        [Serializable] private class ConnectionPool : SerializablePool<ModelConnectionDisplay> { }
        [Serializable] private class AttachmentPool : SerializablePool<ModelAttachmentDisplay> { }

        private unsafe struct GraphSolverState {
            public Unsafe.ArenaHandle Allocator;
            public Vector2* Positions;
            public Vector2* Forces;
            public uint* ConnectionMasks;
            public uint MovedOutputMask;
            public Vector2* FixedPropertyPositions;
            public uint RandomState;
        }

        private struct MaskableEntry {
            public CanvasGroup Group;
            public PolyRaycastFilter RaycastFilter;
            public WorldFilterMask Mask;
            public WorldFilterMask Valid;

            public MaskableEntry(CanvasGroup group, WorldFilterMask mask, WorldFilterMask valid) {
                Group = group;
                RaycastFilter = group.GetComponent<PolyRaycastFilter>();
                Mask = mask;
                Valid = valid;
            }
        }

        #endregion // Types

        #region Consts

        private const int MaxOrganismNodes = 16;
        private const int PropertyIndexOffset = 16;
        private const int MaxPropertyNodes = (int) WaterProperties.TrackedMax + 1;
        private const int MaxGraphNodes = MaxOrganismNodes + MaxPropertyNodes;
        private const int MaxSolverIterations = 64;
        private const int MaxGraphRetries = 300;
        private const float SolverVelocityThresholdSq = .1f * .1f;
        private const int ConnectionMaskSize = MaxOrganismNodes + MaxPropertyNodes;

        private const WorldFilterMask OrganismValidMask = WorldFilterMask.Organism | WorldFilterMask.Relevant;
        private const WorldFilterMask WaterChemMask = WorldFilterMask.AnyWaterChem | WorldFilterMask.Relevant;
        private const WorldFilterMask ConnectionValidMask = WorldFilterMask.AnyBehavior | WorldFilterMask.AnyWaterChem | WorldFilterMask.HasRate | WorldFilterMask.Relevant;
        private const WorldFilterMask AttachmentValidMask = WorldFilterMask.AnyBehavior | WorldFilterMask.HasRate | WorldFilterMask.Relevant | WorldFilterMask.AnyMissing;

        #endregion // Consts

        #region Inspector

        [Header("Root")]
        [SerializeField] private Canvas m_Canvas = null;
        [SerializeField] private CanvasGroup m_Group = null;
        [SerializeField] private InputRaycasterLayer m_Input = null;

        [Header("Water Properties")]
        [SerializeField] private ModelWaterPropertyDisplay m_LightProperty = null;
        [SerializeField] private ModelWaterPropertyDisplay m_TemperatureProperty = null;
        [SerializeField] private ModelWaterPropertyDisplay m_PHProperty = null;
        [SerializeField] private ModelWaterPropertyDisplay m_OxygenProperty = null;
        [SerializeField] private ModelWaterPropertyDisplay m_CarbonDioxideProperty = null;

        [Header("Organisms")]
        [SerializeField] private OrganismPool m_OrganismPool = null;
        [SerializeField] private ConnectionPool m_ConnectionPool = null;
        [SerializeField] private AttachmentPool m_AttachmentPool = null;

        [Header("Constants")]
        [SerializeField] private float m_PositionScale = 96;
        [SerializeField] private float m_CameraHeightScale = 1;
        [SerializeField] private float m_CameraHeightBoundary = 1f;
        [SerializeField] private float m_ForceMultiplier = 1;
        [SerializeField] private float m_RepulsiveForce = 1024;
        [SerializeField] private float m_SpringForce = 1024;
        [SerializeField] private float m_IdealSpringLength = 256;
        [SerializeField] private float m_GravityForce = 10;
        [SerializeField] private float m_ConnectionOffsetFactor = 1.25f;
        [SerializeField] private float m_ConnectionArrowOffsetFactor = 1.25f;
        [SerializeField] private float m_ConnectionOverlapOffset = 0.2f;
        [SerializeField] private float m_AttachmentOverlapOffset = 24;
        [SerializeField] private float m_OrganismRadius = 48;
        [SerializeField] private float m_WaterPropertyRadius = 36;
        // [SerializeField] private float m_BoundaryForce = 2;

        [Header("Textures")]
        [SerializeField] private Texture2D m_PartialLineTexture = null;
        [SerializeField] private Texture2D m_FullLineTexture = null;
        [SerializeField] private Texture2D m_DottedLineTexture = null;

        [Header("Colors")]
        [SerializeField] private Color m_StressedLineColor = ColorBank.Red;
        [SerializeField] private Color m_NormalLineColor = ColorBank.Aqua;
        [SerializeField] private Color m_ReproLineColor = ColorBank.Purple;

        [Header("Sprites")]
        [SerializeField] private Sprite m_MissingReproIcon = null;
        [SerializeField] private Sprite m_MissingReproStressedIcon = null;
        [SerializeField] private Sprite m_MissingEatIcon = null;
        [SerializeField] private Sprite m_MissingEatStressedIcon = null;
        [SerializeField] private Sprite m_MissingChemIcon = null;
        [SerializeField] private Sprite m_MissingChemStressedIcon = null;
        [SerializeField] private Sprite m_MissingStressRangeIcon = null;
        [SerializeField] private Sprite m_MissingParasiteIcon = null;
        [SerializeField] private Sprite m_MissingHistoryIcon = null;

        #endregion // Inspector

        private ModelState m_State;
        private Routine m_ShowHideRoutine;
        private AsyncHandle m_ReconstructHandle;
        private TransformState m_OriginalCanvasState;
        [NonSerialized] private Rect m_AllNodesCameraRect;
        [NonSerialized] private Rect m_WaterChemCameraRect;
        [NonSerialized] private Rect m_VisibleNodesCameraRect;
        [NonSerialized] private StringHash32 m_LastConstructedId;
        [NonSerialized] private BestiaryDesc m_LastConstructedInterventionTarget;

        [NonSerialized] private WorldFilterMask m_FilterAll;
        [NonSerialized] private WorldFilterMask m_FilterAny  = WorldFilterMask.Any;
        [NonSerialized] private WorldFilterMask m_FilterNone  = 0;
        [NonSerialized] private WorldFilterMask m_MaskInUse = 0;

        private readonly Dictionary<StringHash32, ModelOrganismDisplay> m_OrganismMap = new Dictionary<StringHash32, ModelOrganismDisplay>();
        private readonly ModelWaterPropertyDisplay[] m_WaterChemMap = new ModelWaterPropertyDisplay[(int) WaterPropertyId.TRACKED_COUNT];
        private readonly Dictionary<int, int> m_ConnectionCount = new Dictionary<int, int>(32);
        private readonly RingBuffer<MaskableEntry> m_MaskableElements = new RingBuffer<MaskableEntry>(64, RingBufferMode.Expand);
        private GraphSolverState m_SolverState;

        private readonly ModelOrganismDisplay.OnAddRemoveDelegate m_OrganismInterventionDelegate;
        private readonly Predicate<StringHash32> m_HasOrganismPredicate;

        private ModelWorldDisplay() {
            m_OrganismInterventionDelegate = OnOrganismRequestAddRemove;
            m_HasOrganismPredicate = (s) => {
                return m_OrganismMap.ContainsKey(s);
            };
        }

        private void Awake() {
            m_WaterChemMap[(int) WaterPropertyId.Light] = m_LightProperty;
            m_WaterChemMap[(int) WaterPropertyId.Temperature] = m_TemperatureProperty;
            m_WaterChemMap[(int) WaterPropertyId.PH] = m_PHProperty;
            m_WaterChemMap[(int) WaterPropertyId.Oxygen] = m_OxygenProperty;
            m_WaterChemMap[(int) WaterPropertyId.CarbonDioxide] = m_CarbonDioxideProperty;

            InitMemoryArena();
            InitFixedPositions();

            m_OriginalCanvasState = TransformState.LocalState(m_Canvas.transform);
        }

        private void OnDestroy() {
            Unsafe.TryDestroyArena(ref m_SolverState.Allocator);
        }

        private unsafe void InitMemoryArena() {
            m_SolverState.Allocator = Unsafe.CreateArena(1024);
            m_SolverState.Positions = Unsafe.AllocArray<Vector2>(m_SolverState.Allocator, MaxGraphNodes);
            m_SolverState.Forces = Unsafe.AllocArray<Vector2>(m_SolverState.Allocator, ConnectionMaskSize);
            m_SolverState.ConnectionMasks = Unsafe.AllocArray<uint>(m_SolverState.Allocator, ConnectionMaskSize);
            m_SolverState.FixedPropertyPositions = m_SolverState.Positions + MaxOrganismNodes;
        }

        private unsafe void InitFixedPositions() {
            m_SolverState.FixedPropertyPositions[(int) WaterPropertyId.Light] = GetPositionInv(m_LightProperty);
            m_SolverState.FixedPropertyPositions[(int) WaterPropertyId.Temperature] = GetPositionInv(m_TemperatureProperty);
            m_SolverState.FixedPropertyPositions[(int) WaterPropertyId.PH] = GetPositionInv(m_PHProperty);
            m_SolverState.FixedPropertyPositions[(int) WaterPropertyId.Oxygen] = GetPositionInv(m_OxygenProperty);
            m_SolverState.FixedPropertyPositions[(int) WaterPropertyId.CarbonDioxide] = GetPositionInv(m_CarbonDioxideProperty);

            Rect size = SimMath.CalculatePositionBounds(m_SolverState.FixedPropertyPositions, 5);
            SimMath.ScaleBounds(ref size, m_CameraHeightBoundary, m_CameraHeightScale);
            m_WaterChemCameraRect = size;
        }

        public void SetData(ModelState state) {
            m_State = state;
            m_State.OnPhaseChanged += OnPhaseChanged;
        }

        public void Show() {
            m_Canvas.enabled = true;
            m_ShowHideRoutine.Replace(this, m_Group.FadeTo(1, 0.2f));
            m_Input.Override = null;
            UpdateWaterPropertiesUnlocked();
            if (m_LastConstructedId != m_State.Environment.Id()) {
                SyncEnvironmentChemistry(m_State.Environment.GetEnvironment());
                Reconstruct();
            }
        }

        public void Hide() {
            m_ShowHideRoutine.Replace(this, m_Group.FadeTo(0, 0.2f)).OnComplete(OnHideCompleted);
            m_Input.Override = false;
        }

        private void OnHideCompleted() {
            m_Canvas.enabled = false;
            if (m_ReconstructHandle.IsRunning()) {
                m_ReconstructHandle.Cancel();
                m_OrganismPool.Reset();
                m_ConnectionPool.Reset();
                m_AttachmentPool.Reset();
                m_MaskableElements.Clear();
                m_MaskInUse = 0;
                m_AllNodesCameraRect = default;
                m_LastConstructedId = null;
            }
        }

        public void EnableIntervention() {
            UpdateInterventionControls();
        }

        public void DisableIntervention() {
            foreach(var display in m_OrganismPool.ActiveObjects) {
                display.DisableIntervention();
            }
        }

        #if UNITY_EDITOR

        private void LateUpdate() {
            if (m_Canvas.enabled && m_Input.Device.KeyPressed(KeyCode.Y)) {
                Reconstruct(true);
            }
        }

        #endif // UNITY_EDITOR

        #region Reconstruction

        public void ReconstructForIntervention() {
            bool prevExtraConstruct = m_LastConstructedInterventionTarget != null && !m_State.Conceptual.SimulatedEntities.Contains(m_LastConstructedInterventionTarget);
            bool newExtraConstruct = m_State.Simulation.IsInterventionNewOrganism();
            if (prevExtraConstruct && newExtraConstruct) {
                if (m_LastConstructedInterventionTarget == m_State.Simulation.Intervention.Target) {
                    return;
                }
            } else if (!prevExtraConstruct && !newExtraConstruct) {
                return;
            }
            
            Reconstruct();
        }
        
        public IEnumerator Reconstruct(bool ignorePreviousSeed = false) {
            m_ReconstructHandle.Cancel();
            m_LastConstructedId = m_State.Environment.Id();
            m_ReconstructHandle = Async.Schedule(ReconstructProcess(ignorePreviousSeed), AsyncFlags.HighPriority | AsyncFlags.MainThreadOnly);
            m_LastConstructedInterventionTarget = m_State.Simulation.Intervention.Target;
            return m_ReconstructHandle;
        }

        private IEnumerator ReconstructProcess(bool ignorePreviousSeed) {
            m_OrganismPool.Reset();
            m_ConnectionPool.Reset();
            m_AttachmentPool.Reset();
            m_OrganismMap.Clear();
            m_ConnectionCount.Clear();
            m_MaskableElements.Clear();
            m_MaskInUse = 0;
            m_Input.Override = false;
            m_AllNodesCameraRect = default;
            yield return null;

            AddWaterChemMaskables();

            int organismCount = m_State.Conceptual.GraphedEntities.Count;
            if (organismCount == 0) {
                yield break;
            }

            Assert.True(organismCount <= MaxOrganismNodes, "More than maximum allowed critters ({0}) in graph - attempting to populate with {1} nodes", organismCount, MaxOrganismNodes);
            
            ModelOrganismDisplay display;
            int index = 0;
            foreach(var organism in m_State.Conceptual.GraphedEntities) {
                display = AllocOrganism(organism, index++);
                yield return null;
            }

            var intervention = m_State.Simulation.Intervention;
            foreach(var entity in intervention.AdditionalEntities) {
                if (!m_State.Conceptual.GraphedEntities.Contains(entity)) {
                    organismCount++;
                    display = AllocOrganism(entity, index++);
                    yield return null;
                }
            }

            m_SolverState.MovedOutputMask = (1u << organismCount) - 1;
            foreach(var fact in m_State.Conceptual.GraphedFacts) {
                if (!CanGenerateConnection(fact, m_HasOrganismPredicate)) {
                    continue;
                }

                if (BFType.IsSelfTargeting(fact)) {
                    GenerateAttachment(fact, fact.Parent, Save.Bestiary.GetDiscoveredFlags(fact.Id));
                }

                BestiaryDesc target = BFType.Target(fact);
                if (target != null) {
                    if (fact.Type == BFTypeId.Parasite) {
                        GenerateConnection(fact, target, fact.Parent, Save.Bestiary.GetDiscoveredFlags(fact.Id));
                    } else {
                        GenerateConnection(fact, fact.Parent, target, Save.Bestiary.GetDiscoveredFlags(fact.Id));
                    }
                }

                WaterPropertyId property = BFType.WaterProperty(fact);
                if (property != WaterPropertyId.NONE) {
                    GenerateConnection(fact, fact.Parent, property, Save.Bestiary.GetDiscoveredFlags(fact.Id));
                }

                yield return null;
            }

            foreach(var fact in intervention.AdditionalFacts) {
                if (m_State.Conceptual.GraphedFacts.Contains(fact) || !CanGenerateConnection(fact, m_HasOrganismPredicate)) {
                    continue;
                }

                if (BFType.IsSelfTargeting(fact)) {
                    GenerateAttachment(fact, fact.Parent, Save.Bestiary.GetDiscoveredFlags(fact.Id));
                }

                BestiaryDesc target = BFType.Target(fact);
                if (target != null) {
                    GenerateConnection(fact, fact.Parent, target, Save.Bestiary.GetDiscoveredFlags(fact.Id));
                }

                WaterPropertyId property = BFType.WaterProperty(fact);
                if (property != WaterPropertyId.NONE) {
                    GenerateConnection(fact, fact.Parent, property, Save.Bestiary.GetDiscoveredFlags(fact.Id));
                }

                yield return null;
            }

            foreach(var missing in m_State.Conceptual.MissingFacts) {
                BestiaryDesc ownerBestiary = missing.OrganismId.IsEmpty ? null : Assets.Bestiary(missing.OrganismId);
                WaterPropertyId ownerProperty = missing.PropertyId;

                MissingFactTypes mask;
                for(int i = 0; i < 10; i++) {
                    mask = (MissingFactTypes) (1 << i);
                    if ((missing.FactTypes & mask) == mask) {
                        if (ownerProperty != WaterPropertyId.NONE) {
                            GenerateAttachment(mask, ownerProperty);
                        } else {
                            GenerateAttachment(mask, ownerBestiary);
                        }
                        yield return null;
                    }
                }
            }

            int lineCount = m_ConnectionPool.ActiveObjects.Count;

            using(Profiling.Time("solving conceptual model graph")) {
                int iterations = MaxSolverIterations;
                uint seed = ignorePreviousSeed ? 0 : m_State.SiteData.GraphLayoutSeed;
                if (seed == 0) {
                    seed = (uint) RNG.Instance.Next(1, int.MaxValue);
                }
                int tries = MaxGraphRetries;
                uint bestSeed = 0;
                float bestScore = float.MaxValue;
                do {
                    m_SolverState.RandomState = seed;
                    iterations = MaxSolverIterations;
                    RandomizeSolverPositions(ref m_SolverState.RandomState, organismCount);
                    while(m_SolverState.MovedOutputMask != 0 && iterations > 0) {
                        iterations--;
                        SolveStep(ref m_SolverState, organismCount);
                        yield return null;
                    }
                    if (m_SolverState.MovedOutputMask != 0) {
                        Log.Warn("[ModelWorldDisplay] Graph layout did not reach equilibrium in {0} iterations", MaxSolverIterations - iterations);
                    }

                    float badness = CalculateArrangementBadness(ref m_SolverState, organismCount, lineCount);
                    Log.Msg("[ModelWorldDisplay] Solved graph layout (seed {0}) in {1} iterations with badness score {2}", seed, MaxSolverIterations - iterations, badness);
                    yield return null;

                    if (badness < bestScore) {
                        bestScore = badness;
                        bestSeed = seed;
                        if (badness < 0.3f) {
                            if (tries > 0) {
                                tries--;
                            }
                            break;
                        }
                    }
                    if (tries > 1) {
                        seed = (uint) RNG.Instance.Next(1, int.MaxValue);
                    }
                } while(tries-- > 0);

                Log.Msg("[ModelWorldDisplay] Best graph layout seed is {0} with badness score {1} in {2} tries", bestSeed, bestScore, MaxGraphRetries - tries);

                if (bestSeed != seed) {
                    m_SolverState.RandomState = bestSeed;
                    iterations = MaxSolverIterations;
                    RandomizeSolverPositions(ref m_SolverState.RandomState, organismCount);
                    while(m_SolverState.MovedOutputMask != 0 && iterations > 0) {
                        iterations--;
                        SolveStep(ref m_SolverState, organismCount);
                        yield return null;
                    }
                }

                m_State.SiteData.GraphLayoutSeed = bestSeed;
            }

            UpdateOrganismPositions(organismCount);
            yield return null;
            UpdateConnectionPositions();
            yield return null;
            UpdateAttachmentPositions();
            yield return null;
            UpdateInterventionControls();
            yield return null;
            m_State.Conceptual.GraphedMask = m_MaskInUse;
            ReevaluateMaskedElements();
            yield return null;

            m_State.OnGraphChanged?.Invoke(m_MaskInUse);
            m_Input.Override = null;
        }

        private unsafe void RandomizeSolverPositions(ref uint rngState, int count) {
            for(int i = 0; i < count; i++) {
                m_SolverState.Positions[i] = new Vector2(PseudoRandom.Float(ref rngState, -2, 2), PseudoRandom.Float(ref rngState, -2, 2));
                m_SolverState.Forces[i] = default;
            }
            m_SolverState.MovedOutputMask = (1u << count) - 1;
        }

        private unsafe ModelOrganismDisplay AllocOrganism(BestiaryDesc desc, int index) {
            ModelOrganismDisplay display = m_OrganismPool.Alloc();
            display.Initialize(desc, index, m_OrganismInterventionDelegate, m_State.Simulation.IsOrganismRelevant(desc.Id()) ? WorldFilterMask.Relevant : 0);
            m_OrganismMap.Add(desc.Id(), display);
            m_SolverState.ConnectionMasks[index] = 0;

            #if UNITY_EDITOR
            display.gameObject.name = desc.name;
            #endif // UNITY_EDITOR

            AddMaskable(display.CanvasGroup, display.Mask, OrganismValidMask);

            return display;
        }

        private unsafe void GenerateConnection(BFBase fact, BestiaryDesc owner, BestiaryDesc target, BFDiscoveredFlags flags) {
            int indexA = GetIndex(target);
            int indexB = GetIndex(owner);
            m_SolverState.ConnectionMasks[indexA] |= (1u << indexB);
            m_SolverState.ConnectionMasks[indexB] |= (1u << indexA);

            ModelConnectionDisplay connection = m_ConnectionPool.Alloc();
            connection.Fact = fact;
            connection.Key = GenerateConnectionKey(indexA, indexB);
            connection.IndexA = (ushort) indexA;
            connection.IndexB = (ushort) indexB;
            connection.ConnectionIndex = IncrementConnectionCount(connection.Key);
            connection.Texture.texture = BFType.HasAll(flags) ? m_FullLineTexture : m_PartialLineTexture;
            connection.Texture.color = connection.Arrow.color = BFType.OnlyWhenStressed(fact) ? m_StressedLineColor : m_NormalLineColor;
            connection.Arrow.gameObject.SetActive(true);
            connection.Fader.SetActive(false);
            connection.Scroll.enabled = false;
            connection.Order = 1;
            connection.Icon.gameObject.SetActive(true);
            connection.Icon.sprite = fact.Icon;

            #if UNITY_EDITOR
            connection.gameObject.name = fact.name;
            #endif // UNITY_EDITOR

            if (m_State.Simulation.IsOrganismRelevant(owner.Id()) && m_State.Simulation.IsOrganismRelevant(target.Id())) {
                connection.Mask = WorldFilterMask.Relevant;
            } else {
                connection.Mask = 0;
            }

            switch(fact.Type) {
                case BFTypeId.Eat: {
                    connection.Mask |= WorldFilterMask.Eats;
                    break;
                }

                case BFTypeId.Parasite: {
                    connection.Mask |= WorldFilterMask.Parasites;
                    break;
                }

                case BFTypeId.Reproduce:
                case BFTypeId.Grow:  {
                    connection.Mask |= WorldFilterMask.Repro;
                    break;
                }
            }

            if (BFType.HasRate(flags)) {
                connection.Mask |= WorldFilterMask.HasRate;
            }

            AddMaskable(connection.CanvasGroup, connection.Mask, ConnectionValidMask);
        }

        private unsafe void GenerateConnection(BFBase fact, BestiaryDesc owner, WaterPropertyId property, BFDiscoveredFlags flags) {
            int indexA = GetIndex(property);
            int indexB = GetIndex(owner);

            if (fact.Type == BFTypeId.Produce) {
                Ref.Swap(ref indexA, ref indexB);
            }

            int key = GenerateConnectionKey(indexA, indexB);
            if (m_ConnectionCount.ContainsKey(key)) {
                GetFirstConnectionForKey(key).Fact2 = fact;
                return;
            }

            m_SolverState.ConnectionMasks[indexA] |= (1u << indexB);
            m_SolverState.ConnectionMasks[indexB] |= (1u << indexA);

            ModelConnectionDisplay connection = m_ConnectionPool.Alloc();
            connection.Fact = fact;
            connection.Key = key;
            connection.IndexA = (ushort) indexA;
            connection.IndexB = (ushort) indexB;
            connection.ConnectionIndex = IncrementConnectionCount(key);
            connection.Texture.texture = m_DottedLineTexture;
            connection.Texture.color = Assets.Property(property).Color();
            connection.Arrow.gameObject.SetActive(false);
            connection.Fader.SetActive(true);
            connection.Scroll.enabled = true;
            connection.Order = 0;
            connection.Icon.gameObject.SetActive(false);

            #if UNITY_EDITOR
            connection.gameObject.name = fact.name;
            #endif // UNITY_EDITOR

            if (m_State.Simulation.IsOrganismRelevant(owner.Id()) && m_State.Simulation.IsWaterPropertyRelevant(property)) {
                connection.Mask = WorldFilterMask.Relevant;
            } else {
                connection.Mask = 0;
            }

            switch(property) {
                case WaterPropertyId.Oxygen:
                case WaterPropertyId.CarbonDioxide: {
                    connection.Mask |= WorldFilterMask.OxygenAndCarbonDioxide;
                    break;
                }

                case WaterPropertyId.Light: {
                    connection.Mask |= WorldFilterMask.Light;
                    break;
                }
            }

            if (BFType.HasRate(flags)) {
                connection.Mask |= WorldFilterMask.HasRate;
            }

            AddMaskable(connection.CanvasGroup, connection.Mask, ConnectionValidMask);
        }

        private unsafe void GenerateAttachment(BFBase fact, BestiaryDesc owner, BFDiscoveredFlags flags) {
            int index = GetIndex(owner);
            bool onlyStressed = BFType.OnlyWhenStressed(fact);

            ModelAttachmentDisplay attachment = m_AttachmentPool.Alloc();
            attachment.Fact = fact;
            attachment.Key = GenerateConnectionKey(index, index);
            attachment.Index = (ushort) index;
            attachment.AttachmentIndex = IncrementConnectionCount(attachment.Key);
            attachment.Arrow.color = onlyStressed ? m_StressedLineColor : m_ReproLineColor;
            attachment.Arrow.gameObject.SetActive(true);
            attachment.Stressed.SetActive(onlyStressed);
            attachment.Icon.sprite = fact.Icon;

            #if UNITY_EDITOR
            attachment.gameObject.name = fact.name;
            #endif // UNITY_EDITOR

            if (m_State.Simulation.IsOrganismRelevant(owner.Id())) {
                attachment.Mask = WorldFilterMask.Relevant;
            } else {
                attachment.Mask = 0;
            }

            switch(fact.Type) {
                case BFTypeId.Reproduce:
                case BFTypeId.Grow:  {
                    attachment.Mask |= WorldFilterMask.Repro;
                    break;
                }
            }

            if (BFType.HasRate(flags)) {
                attachment.Mask |= WorldFilterMask.HasRate;
            }

            AddMaskable(attachment.CanvasGroup, attachment.Mask, AttachmentValidMask);
        }

        private unsafe void GenerateAttachment(MissingFactTypes missingType, BestiaryDesc owner) {
            int index = GetIndex(owner);

            ModelAttachmentDisplay attachment = m_AttachmentPool.Alloc();
            attachment.Key = GenerateConnectionKey(index, index);
            attachment.Index = (ushort) index;
            attachment.Missing = missingType;
            attachment.AttachmentIndex = IncrementConnectionCount(attachment.Key);
            attachment.Arrow.gameObject.SetActive(false);

            #if UNITY_EDITOR
            attachment.gameObject.name = string.Format("{0}.Missing.{1}", owner.name, missingType.ToString());
            #endif // UNITY_EDITOR

            if (m_State.Simulation.IsOrganismRelevant(owner.Id())) {
                attachment.Mask = WorldFilterMask.Relevant;
            } else {
                attachment.Mask = 0;
            }

            attachment.Mask |= WorldFilterMask.Missing;

            switch(missingType) {
                case MissingFactTypes.Repro: {
                    attachment.Mask |= WorldFilterMask.Repro;
                    attachment.Icon.sprite = m_MissingReproIcon;
                    break;
                }

                case MissingFactTypes.Repro_Stressed: {
                    attachment.Mask |= WorldFilterMask.Repro;
                    attachment.Icon.sprite = m_MissingReproStressedIcon;
                    break;
                }

                case MissingFactTypes.Eat: {
                    attachment.Mask |= WorldFilterMask.Eats;
                    attachment.Icon.sprite = m_MissingEatIcon;
                    break;
                }

                case MissingFactTypes.Eat_Stressed: {
                    attachment.Mask |= WorldFilterMask.Eats;
                    attachment.Icon.sprite = m_MissingEatStressedIcon;
                    break;
                }

                case MissingFactTypes.WaterChem: {
                    attachment.Mask |= WorldFilterMask.AnyWaterChem;
                    attachment.Icon.sprite = m_MissingChemIcon;
                    break;
                }

                case MissingFactTypes.WaterChem_Stressed: {
                    attachment.Mask |= WorldFilterMask.AnyWaterChem;
                    attachment.Icon.sprite = m_MissingChemStressedIcon;
                    break;
                }

                case MissingFactTypes.StressRange: {
                    attachment.Mask |= WorldFilterMask.AnyWaterChem;
                    attachment.Icon.sprite = m_MissingStressRangeIcon;
                    break;
                }

                case MissingFactTypes.Parasite: {
                    attachment.Mask |= WorldFilterMask.Parasites;
                    attachment.Icon.sprite = m_MissingParasiteIcon;
                    break;
                }

                case MissingFactTypes.PopulationHistory: {
                    attachment.Mask |= WorldFilterMask.History;
                    attachment.Icon.sprite = m_MissingHistoryIcon;
                    break;
                }
            }

            AddMaskable(attachment.CanvasGroup, attachment.Mask, AttachmentValidMask);
        }

        private unsafe void GenerateAttachment(MissingFactTypes missingType, WaterPropertyId propertyId) {
            int index = GetIndex(propertyId);

            ModelAttachmentDisplay attachment = m_AttachmentPool.Alloc();
            attachment.Key = GenerateConnectionKey(index, index);
            attachment.Index = (ushort) index;
            attachment.Missing = missingType;
            attachment.AttachmentIndex = IncrementConnectionCount(attachment.Key);
            attachment.Arrow.gameObject.SetActive(false);

            #if UNITY_EDITOR
            attachment.gameObject.name = string.Format("{0}.Missing.{1}", propertyId.ToString(), missingType.ToString());
            #endif // UNITY_EDITOR

            if (m_State.Simulation.IsWaterPropertyRelevant(propertyId)) {
                attachment.Mask = WorldFilterMask.Relevant;
            } else {
                attachment.Mask = 0;
            }

            attachment.Mask |= WorldFilterMask.Missing;

            switch(missingType) {
                case MissingFactTypes.WaterChemHistory: {
                    attachment.Mask |= WorldFilterMask.History;
                    attachment.Icon.sprite = m_MissingHistoryIcon;
                    break;
                }
            }

            AddMaskable(attachment.CanvasGroup, attachment.Mask, AttachmentValidMask);
        }

        private unsafe void UpdateOrganismPositions(int count) {
            var allocatedOrganisms = m_OrganismPool.ActiveObjects;
            for(int i = 0; i < count; i++) {
                PositionOrganism(allocatedOrganisms[i], ref m_SolverState, i);
            }

            Rect size = SimMath.CalculatePositionBounds(m_SolverState.Positions, count);
            SimMath.ScaleBounds(ref size, m_CameraHeightBoundary, m_CameraHeightScale);
            m_AllNodesCameraRect = size;
        }

        private unsafe void RecalculateCameraBounds() {
            Vector2* pointBuffer = Frame.AllocArray<Vector2>(MaxGraphNodes);
            int count = 0;

            var allocatedOrganisms = m_OrganismPool.ActiveObjects;
            for(int i = 0; i < allocatedOrganisms.Count; i++) {
                if (allocatedOrganisms[i].CanvasGroup.alpha > 0) {
                    pointBuffer[count++] = m_SolverState.Positions[i];
                }
            }

            for(int i = 0; i < 5; i++) {
                var chem = m_WaterChemMap[i];
                if (chem.isActiveAndEnabled && chem.CanvasGroup.alpha > 0) {
                    pointBuffer[count++] = m_SolverState.FixedPropertyPositions[i];
                }
            }

            Rect size = SimMath.CalculatePositionBounds(pointBuffer, count);
            SimMath.ScaleBounds(ref size, m_CameraHeightBoundary, m_CameraHeightScale);
            m_VisibleNodesCameraRect = size;
        }

        private void UpdateConnectionPositions() {
            using(PooledList<ModelConnectionDisplay> allocatedConnections = PooledList<ModelConnectionDisplay>.Create(m_ConnectionPool.ActiveObjects)) {
                allocatedConnections.Sort((x, y) => y.Order - x.Order);
                int count = allocatedConnections.Count;
                Vector2 a, b, vecAB, centerAB, cross;
                float distAB, rotZ;
                int connectionCount;
                float connectionStart, connectionOffset;
                ModelConnectionDisplay display;
                for(int i = 0; i < count; i++) {
                    display = allocatedConnections[i];
                    a = GetTransform(display.IndexA).anchoredPosition;
                    b = GetTransform(display.IndexB).anchoredPosition;
                    vecAB = b - a;
                    centerAB = (a + b) * 0.5f;
                    distAB = vecAB.magnitude;
                    vecAB.Normalize();
                    cross = new Vector2(-vecAB.y, vecAB.x);
                    distAB -= m_PositionScale * m_ConnectionOffsetFactor;
                    if (display.Arrow.isActiveAndEnabled) {
                        distAB -= m_PositionScale * m_ConnectionArrowOffsetFactor;
                        centerAB -= vecAB * m_PositionScale * m_ConnectionArrowOffsetFactor * 0.5f;
                    }
                    m_ConnectionCount.TryGetValue(display.Key, out connectionCount);
                    if (connectionCount > 1) {
                        connectionStart = -(connectionCount - 1) * 0.5f;
                        connectionOffset = m_PositionScale * m_ConnectionOverlapOffset * (connectionStart + display.ConnectionIndex);
                        centerAB.x += cross.x * connectionOffset;
                        centerAB.y += cross.y * connectionOffset;
                    }

                    rotZ = Mathf.Atan2(vecAB.y, vecAB.x) * Mathf.Rad2Deg;
                    display.Transform.SetSizeDelta(distAB, Axis.X);
                    display.Transform.SetAnchorPos(centerAB);
                    display.Transform.SetRotation(rotZ, Axis.Z, Space.Self);
                    display.Transform.SetAsFirstSibling();
                    display.Icon.transform.SetRotation(-rotZ, Axis.Z, Space.Self);
                }
            }
        }

        private void UpdateAttachmentPositions() {
            using(PooledList<ModelAttachmentDisplay> allocatedAttachments = PooledList<ModelAttachmentDisplay>.Create(m_AttachmentPool.ActiveObjects)) {
                int count = allocatedAttachments.Count;
                RectTransform aTrans;
                Vector2 a, offset;
                float radius;
                int attachmentCount;
                float attachmentStart, attachmentOffset;
                ModelAttachmentDisplay display;
                for(int i = 0; i < count; i++) {
                    display = allocatedAttachments[i];
                    aTrans = GetTransform(display.Index);
                    a = aTrans.anchoredPosition;
                    radius = display.Index >= PropertyIndexOffset ? m_WaterPropertyRadius : m_OrganismRadius;

                    m_ConnectionCount.TryGetValue(display.Key, out attachmentCount);
                    attachmentCount = Mathf.Max(attachmentCount, 3);
                    attachmentStart = (attachmentCount - 1) * 0.5f;
                    attachmentOffset = Mathf.PI / 2 + (m_AttachmentOverlapOffset / radius) * (attachmentStart - display.AttachmentIndex);
                    offset.x = Mathf.Cos(attachmentOffset) * radius;
                    offset.y = Mathf.Sin(attachmentOffset) * radius;

                    display.Transform.SetAnchorPos(a + offset);
                    if (display.Arrow.isActiveAndEnabled) {
                        display.Icon.rectTransform.SetSizeDelta(24, Axis.XY);
                        display.Transform.SetSiblingIndex(aTrans.GetSiblingIndex());
                        display.Arrow.rectTransform.SetRotation(Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg, Axis.Z, Space.Self);
                    } else {
                        display.Icon.rectTransform.SetSizeDelta(48, Axis.XY);
                        display.Transform.SetSiblingIndex(aTrans.GetSiblingIndex() + 1);
                    }
                }
            }
        }

        private unsafe void PositionOrganism(ModelOrganismDisplay display, ref GraphSolverState solverState, int index) {
            display.Transform.SetAnchorPos(m_SolverState.Positions[index] * m_PositionScale, Axis.XY);
        }

        private Vector2 GetPositionInv(ModelWaterPropertyDisplay display) {
            return display.GetComponent<RectTransform>().anchoredPosition / m_PositionScale;
        }

        private RectTransform GetTransform(int index) {
            if (index >= PropertyIndexOffset) {
                return (RectTransform) m_WaterChemMap[index - PropertyIndexOffset].transform;
            } else if (index >= 0) {
                return m_OrganismPool.ActiveObjects[index].Transform;
            } else {
                return null;
            }
        }

        private int GetIndex(BestiaryDesc organism) {
            return m_OrganismMap[organism.Id()].Index;
        }

        private int GetIndex(WaterPropertyId property) {
            return PropertyIndexOffset + (int) property;
        }

        private ModelConnectionDisplay GetFirstConnectionForKey(int key) {
            foreach(var connection in m_ConnectionPool.ActiveObjects) {
                if (connection.Key == key) {
                    return connection;
                }
            }

            return null;
        }

        private int IncrementConnectionCount(int key) {
            int current;
            m_ConnectionCount.TryGetValue(key, out current);
            m_ConnectionCount[key] = current + 1;
            return current;
        }
    
        static private bool CanGenerateConnection(BFBase fact, Predicate<StringHash32> hasEntity) {
            if (!BFType.IsOrganism(fact)) {
                return false;
            }

            return BFType.IsBehavior(fact) && hasEntity(fact.Parent.Id());
        }

        static private int GenerateConnectionKey(int a, int b) {
            return (a << 16) | (b);
        }

        private unsafe void SolveStep(ref GraphSolverState solverState, int count) {
            solverState.MovedOutputMask = 0;
            Vector2 posA, posB, vecAB, vecForce;
            Vector2* forceA, forceB;
            int a, b;
            uint maskA;
            float distAB;
            float tempForce;

            // update velocities
            for(a = 0; a < count; a++) {
                posA = solverState.Positions[a];
                forceA = &solverState.Forces[a];
                maskA = solverState.ConnectionMasks[a];

                // gravity force
                posB = default(Vector2);
                vecAB = -posA;
                vecAB.y *= 1.3f;
                *forceA += vecAB * m_GravityForce;

                for(b = a + 1; b < count; b++) {
                    posB = solverState.Positions[b];
                    forceB = &solverState.Forces[b];

                    vecAB = posB - posA; // a to b
                    distAB = vecAB.magnitude;

                    // spring force
                    if ((maskA & (1 << b)) != 0) {
                        tempForce = m_SpringForce * Mathf.Log(distAB / m_IdealSpringLength);
                        vecForce = vecAB.normalized * tempForce;
                        *forceA += vecForce;
                        *forceB -= vecForce;
                    } else { // repulse force
                        tempForce = m_RepulsiveForce / (distAB * distAB);
                        vecForce = vecAB.normalized * tempForce;
                        *forceA -= vecForce;
                        *forceB += vecForce;
                    }
                }
            }

            // update positions
            for(a = 0; a < count; a++) {
                forceA = &solverState.Forces[a];
                solverState.Positions[a] += *forceA * m_ForceMultiplier;
                if (forceA->sqrMagnitude > SolverVelocityThresholdSq) {
                    solverState.MovedOutputMask |= (1u << a);
                }
                *forceA = default;
            }
        }

        static private unsafe float CalculateArrangementBadness(ref GraphSolverState solverState, int count, int lineBufferSize) {
            Vector2 posA, posB;
            int a, b;
            uint maskA;

            // more moved pieces
            float movedScore = Bits.Count(solverState.MovedOutputMask) * 2;
            
            // line intersections
            Vector2* lineComp = stackalloc Vector2[lineBufferSize * 2];
            int lineCompCount = 0;

            // get all lines
            for(a = 0; a < count; a++) {
                posA = solverState.Positions[a];
                maskA = solverState.ConnectionMasks[a];

                for(b = a + 1; b < count; b++) {
                    if ((maskA & (1 << b)) != 0) {
                        posB = solverState.Positions[b];
                        lineComp[lineCompCount++] = posA;
                        lineComp[lineCompCount++] = posB - posA;
                    }
                }
            }

            int lineCount = lineCompCount >> 1;
            int aOffset, bOffset;
            float intersectionScore = 0;

            for(a = 0; a < lineCompCount; a++) {
                aOffset = a * 2;
                for(b = a + 1; b < lineCompCount; b++) {
                    bOffset = b * 2;
                    if (IntersectionPoint(lineComp[aOffset], lineComp[aOffset + 1], lineComp[bOffset], lineComp[bOffset + 1], out float intersectionPoint) && intersectionPoint > 0.15 && intersectionPoint < 0.85) {
                        float perpendicular = 1.25f - Math.Abs(Vector2.Dot(lineComp[aOffset + 1].normalized, lineComp[bOffset + 1].normalized));
                        intersectionScore += perpendicular * (0.8f - Math.Abs(intersectionPoint - 0.5f)) * 4;
                    }
                }
            }

            return movedScore + intersectionScore;
        }

        static private bool IntersectionPoint(Vector2 a, Vector2 aVec, Vector2 b, Vector2 bVec, out float point) {
            float perp = aVec.x * bVec.y - aVec.y * bVec.x;

            if (perp != 0) {
                Vector2 c = new Vector2(b.x - a.x, b.y - a.y);
                point = (c.x * bVec.y - c.y * bVec.x) / perp;
                return point >= 0 && point <= 1;
            } else {
                point = 0;
                return false;
            }
        }

        #endregion // Reconstruction

        #region Filters

        public void SetFilters(WorldFilterMask any, WorldFilterMask all, WorldFilterMask none, bool force = false) {
            if (force || all != m_FilterAll || any != m_FilterAny || none != m_FilterNone) {
                m_FilterAll = all;
                m_FilterAny = any;
                m_FilterNone = none;
                if (!m_ReconstructHandle.IsRunning()) {
                    ReevaluateMaskedElements();
                }
            }
        }

        private void AddMaskable(CanvasGroup group, WorldFilterMask mask, WorldFilterMask valid) {
            m_MaskableElements.PushBack(new MaskableEntry(group, mask, valid));
            m_MaskInUse |= mask;
        }

        private void AddWaterChemMaskables() {
            AddMaskable(m_OxygenProperty.CanvasGroup, WorldFilterMask.OxygenAndCarbonDioxide, WaterChemMask);
            AddMaskable(m_CarbonDioxideProperty.CanvasGroup, WorldFilterMask.OxygenAndCarbonDioxide, WaterChemMask);
            AddMaskable(m_LightProperty.CanvasGroup, WorldFilterMask.Light, WaterChemMask);
            AddMaskable(m_PHProperty.CanvasGroup, WorldFilterMask.PhAndTemp, WaterChemMask);
            AddMaskable(m_TemperatureProperty.CanvasGroup, WorldFilterMask.PhAndTemp, WaterChemMask);
        }

        private unsafe void ReevaluateMaskedElements() {
            const float hiddenAlpha = 0;
            for(int i = 0, len = m_MaskableElements.Count; i < len; i++) {
                ref var element = ref m_MaskableElements[i];
                if (CheckMasks(element.Mask, m_FilterAny, m_FilterAll, m_FilterNone, element.Valid)) {
                    element.Group.alpha = 1;
                    element.Group.blocksRaycasts = true;
                    if (element.RaycastFilter) {
                        element.RaycastFilter.enabled = m_State.Phase == ModelPhases.Concept;
                    }
                } else {
                    element.Group.alpha = hiddenAlpha;
                    element.Group.blocksRaycasts = false;
                    if (element.RaycastFilter) {
                        element.RaycastFilter.enabled = false;
                    }
                }
            }

            RecalculateCameraBounds();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static private bool CheckMasks(WorldFilterMask src, WorldFilterMask any, WorldFilterMask all, WorldFilterMask none, WorldFilterMask valid) {
            return (src & none & valid) == 0 && (src & all & valid) == all && (any == 0 || (src & any & valid) != 0);
        }

        #endregion // Filters

        public IEnumerator OnPreloadScene(SceneBinding inScene, object inContext)
        {
            m_LightProperty.Initialize(Assets.Property(WaterPropertyId.Light));
            m_TemperatureProperty.Initialize(Assets.Property(WaterPropertyId.Temperature));
            m_PHProperty.Initialize(Assets.Property(WaterPropertyId.PH));
            m_OxygenProperty.Initialize(Assets.Property(WaterPropertyId.Oxygen));
            m_CarbonDioxideProperty.Initialize(Assets.Property(WaterPropertyId.CarbonDioxide));

            foreach(var prop in m_WaterChemMap) {
                prop.Index = PropertyIndexOffset + (int) prop.Property.Index();
            }

            yield return null;
            m_Canvas.enabled = false;
            m_Group.alpha = 0;
            m_Input.Override = false;
            yield return null;
            m_ConnectionPool.Initialize();
            m_OrganismPool.Initialize();
            m_AttachmentPool.Initialize();
        }

        private void UpdateWaterPropertiesUnlocked() {
            var save = Save.Inventory;
            bool canSee = save.HasUpgrade(ItemIds.WaterModeling);
            m_LightProperty.gameObject.SetActive(canSee);
            m_TemperatureProperty.gameObject.SetActive(canSee);
            m_PHProperty.gameObject.SetActive(canSee);
            m_OxygenProperty.gameObject.SetActive(canSee);
            m_CarbonDioxideProperty.gameObject.SetActive(canSee);
        }

        private void SyncEnvironmentChemistry(WaterPropertyBlockF32 environment) {
            m_LightProperty.SetValue(environment.Light);
            m_LightProperty.Mask = m_State.Simulation.IsWaterPropertyRelevant(WaterPropertyId.Light) ? WorldFilterMask.Relevant : 0;

            m_TemperatureProperty.SetValue(environment.Temperature);
            m_TemperatureProperty.Mask = m_State.Simulation.IsWaterPropertyRelevant(WaterPropertyId.Temperature) ? WorldFilterMask.Relevant : 0;

            m_PHProperty.SetValue(environment.PH);
            m_PHProperty.Mask = m_State.Simulation.IsWaterPropertyRelevant(WaterPropertyId.PH) ? WorldFilterMask.Relevant : 0;

            m_OxygenProperty.SetValue(environment.Oxygen);
            m_OxygenProperty.Mask = m_State.Simulation.IsWaterPropertyRelevant(WaterPropertyId.Oxygen) ? WorldFilterMask.Relevant : 0;

            m_CarbonDioxideProperty.SetValue(environment.CarbonDioxide);
            m_CarbonDioxideProperty.Mask = m_State.Simulation.IsWaterPropertyRelevant(WaterPropertyId.CarbonDioxide) ? WorldFilterMask.Relevant : 0;
        }

        private ModelOrganismDisplay.AddRemoveResult OnOrganismRequestAddRemove(BestiaryDesc organism, int sign) {
            bool changedTarget = Ref.Replace(ref m_State.Simulation.Intervention.Target, organism);
            if (changedTarget) {
                m_State.Simulation.Intervention.Amount = 0;
            }

            BFBody body = organism.FactOfType<BFBody>();

            uint startingPopulation = m_State.Simulation.GetInterventionStartingPopulation(organism);
            int adjust = (int) (m_State.Simulation.Intervention.Amount + sign * body.PopulationSoftIncrement);
            long next = startingPopulation + adjust;
            
            if (next < 0) {
                next = 0;
            } else if (adjust > 0 && next > body.PopulationSoftCap) {
                next = body.PopulationSoftCap;
            } else if (adjust > -body.PopulationSoftIncrement && adjust < body.PopulationSoftIncrement) {
                next = startingPopulation;
            }

            adjust = (int) (next - startingPopulation);
            if (m_State.Simulation.Intervention.Amount != adjust) {
                m_State.Simulation.Intervention.Amount = adjust;
                if (adjust == 0 && !m_State.Simulation.IsInterventionNewOrganism()) {
                    changedTarget = true;
                    m_State.Simulation.Intervention.Target = null;
                } else if (!changedTarget) {
                    m_State.Simulation.DispatchInterventionUpdate();
                }
            }

            if (changedTarget) {
                m_State.Simulation.DispatchInterventionUpdate();
                UpdateInterventionControls();
            }

            ModelOrganismDisplay.AddRemoveResult result;
            result.CanAdd = adjust < 0 || next + body.PopulationSoftIncrement <= body.PopulationSoftCap;
            result.CanRemove = next - body.PopulationSoftIncrement >= 0;
            result.DifferenceValue = adjust;

            InterveneUpdateData data;
            data.Organism = organism.name;
            data.DifferenceValue = result.DifferenceValue;
            Services.Events.Dispatch(ModelingConsts.Event_Intervene_Update, data);

            return result;
        }

        private void OnPhaseChanged(ModelPhases prev, ModelPhases current) {
            if (current == ModelPhases.Intervene) {
                EnableIntervention();
            } else if (prev == ModelPhases.Intervene) {
                DisableIntervention();
            }
        }

        private void UpdateInterventionControls() {
            if (m_State.Phase != ModelPhases.Intervene) {
                return;
            }

            if (m_State.Simulation.Intervention.Target == null) {
                foreach(var display in m_OrganismPool.ActiveObjects) {
                    if (m_State.Conceptual.SimulatedEntities.Contains(display.Organism)) {
                        display.EnableIntervention(GetDesiredState(display.Organism, 0));
                    } else {
                        display.DisableIntervention();
                    }
                }
            } else {
                foreach(var display in m_OrganismPool.ActiveObjects) {
                    if (display.Organism == m_State.Simulation.Intervention.Target) {
                        display.EnableIntervention(GetDesiredState(display.Organism, m_State.Simulation.Intervention.Amount));
                    } else {
                        display.DisableIntervention();
                    }
                }
            }
        }

        private ModelOrganismDisplay.AddRemoveResult GetDesiredState(BestiaryDesc organism, int adjust) {
            BFBody body = organism.FactOfType<BFBody>();

            uint startingPopulation = m_State.Simulation.GetInterventionStartingPopulation(organism);
            long next = startingPopulation + adjust;

            ModelOrganismDisplay.AddRemoveResult result;
            result.CanAdd = adjust < 0 || next + body.PopulationSoftIncrement <= body.PopulationSoftCap;
            result.CanRemove = next - body.PopulationSoftIncrement >= 0;
            result.DifferenceValue = adjust;
            return result;
        }
    }

    public struct InterveneUpdateData {
        public string Organism;
        public int DifferenceValue;
    }

    [Flags]
    public enum WorldFilterMask : uint {
        Relevant        = 2 << 0,
        HasRate         = 2 << 1,
        Organism        = 2 << 2,
        Eats            = 2 << 3,
        Parasites       = 2 << 4,
        Repro           = 2 << 5,
        OxygenAndCarbonDioxide = 2 << 6,
        Light           = 2 << 7,
        History         = 2 << 8,
        Missing         = 2 << 9,
        PhAndTemp        = 2 << 10,

        AnyBehavior = Eats | Parasites | Repro,
        AnyWaterChem = OxygenAndCarbonDioxide | Light | PhAndTemp,
        AnyMissing = History | Missing,

        Any = AnyBehavior | AnyWaterChem | AnyMissing | Relevant | Organism | HasRate
    }
}
