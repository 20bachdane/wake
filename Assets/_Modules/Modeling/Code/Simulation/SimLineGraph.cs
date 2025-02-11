using System;
using System.Collections.Generic;
using BeauPools;
using BeauRoutine;
using BeauUtil;
using UnityEngine;

namespace Aqua.Modeling {
    public unsafe class SimLineGraph : MonoBehaviour {
        public struct StressedRegion {
            public ushort StartIdx;
            public ushort EndIdx;
        }

        #region Inspector

        [SerializeField] private Color m_StressColor = Color.red;
        [SerializeField] private Color m_InterventionGoalColor = Color.green;
        [SerializeField] private Color m_InterventionDiscrepancyColor = Color.red;
        [SerializeField] private SimGraphBlock.Pool m_Blocks = null;
        [SerializeField] private GraphTargetRegion.Pool m_Targets = null;
        [SerializeField] private GraphStressPoint.Pool m_StressIcons = null;
        [SerializeField] private GraphDivergencePoint.Pool m_DivergenceIcons = null;
        [SerializeField] private Sprite m_MissingOrganisms = null;

        #endregion // Inspector

        public Action<GraphDivergencePoint> OnDivergenceClicked;
        public Action OnDiscrepancyClicked;

        private readonly List<SimGraphBlock> m_AllocatedBlocks = new List<SimGraphBlock>();
        private readonly Dictionary<StringHash32, SimGraphBlock> m_AllocatedBlockMap = new Dictionary<StringHash32, SimGraphBlock>();
        private readonly SimGraphBlock[] m_AllocatedWaterMap = new SimGraphBlock[(int) WaterProperties.TrackedMax];

        [NonSerialized] private SimGraphBlock m_InterventionBlock;
        [NonSerialized] private List<GraphTargetRegion> m_InterveneRegions = new List<GraphTargetRegion>(2);
        private static readonly List<float> s_FinalBlockYs = new List<float>(8);
        private readonly GenerateStressPointsDelegate StressPointCallback;
        private readonly GenerateDivergenceDelegate DivergenceCallback;

        private SimLineGraph() {
            StressPointCallback = (SimGraphBlock block, SimRenderMask phase, StressedRegion* stressRegions, int stressRegionCount) => {
                if (block.StressPoints != null) {
                    while(block.StressPoints.TryPopBack(out var b)) {
                        b.Dispose();
                    }
                }

                if (stressRegionCount > 0) {
                    if (block.StressPoints == null) {
                        block.StressPoints = new RingBuffer<TempAlloc<GraphStressPoint>>();
                    }

                    block.StressPointMask = phase;

                    Transform targetParent = phase == SimRenderMask.Player ? block.DescribeGroup.transform : block.PredictGroup.transform;

                    for(int i = 0; i < stressRegionCount; i++) {
                        StressedRegion region = stressRegions[i];
                        
                        if (region.StartIdx != 0) {
                            var pointAlloc = m_StressIcons.TempAlloc();
                            var point = pointAlloc.Object;
                            point.transform.SetParent(targetParent, false);
                            block.StressPoints.PushBack(pointAlloc);
                            point.Index = region.StartIdx;
                            point.Icon.enabled = false;
                        }

                        {
                            var pointAlloc = m_StressIcons.TempAlloc();
                            var point = pointAlloc.Object;
                            point.transform.SetParent(targetParent, false);
                            block.StressPoints.PushBack(pointAlloc);
                            point.Index = region.EndIdx;
                            point.Icon.enabled = false;
                        }
                    }
                } else {
                    block.StressPointMask = 0;
                }
            };
        
            DivergenceCallback = (SimGraphBlock block) => {
                var temp = m_DivergenceIcons.TempAlloc();
                var obj = temp.Object;
                obj.transform.SetParent(block.DescribeGroup.transform, false);
                obj.Parent = block;
                obj.gameObject.SetActive(false);
                block.Divergence = temp;
            };
        }

        private void Start() {
            Services.Events.Register(ModelingConsts.Event_Intervene_Error, OnInterveneError, this);
            m_DivergenceIcons.TryInitialize(null, null, 0);
            
            Action<GraphDivergencePoint> callbackInvoker = (g) => OnDivergenceClicked?.Invoke(g);
            m_DivergenceIcons.Config.RegisterOnConstruct((_, i) => {
                i.OnClick = callbackInvoker;
            });
            m_DivergenceIcons.Prewarm();

            UnityEngine.Events.UnityAction targetDiscrepancyCallback = () => OnDiscrepancyClicked?.Invoke();
            m_Targets.TryInitialize(null, null, 0);
            m_Targets.Config.RegisterOnConstruct((_, i) => {
                i.Discrepancy.Click.onClick.AddListener(targetDiscrepancyCallback);
            });
        }

        private void OnDestroy() {
            Services.Events?.DeregisterAll(this);
        }

        public void AllocateBlocks(ModelState state) {
            ClearBlocks();
            foreach(var organismId in state.Simulation.RelevantCritterIds()) {
                GetBlock(organismId, state);
            }
            foreach(var waterProp in state.Simulation.RelevantWaterProperties()) {
                GetBlock(waterProp, state);
            }
        }

        public void Intervene(SimulationDataCtrl.InterventionData data, ModelState state) {
            StringHash32 id = data.Target?.Id() ?? StringHash32.Null;
            
            if (m_InterventionBlock != null) {
                if (m_InterventionBlock.ActorId == id) {
                    return;
                }

                m_AllocatedBlockMap.Remove(m_InterventionBlock.ActorId);
                m_AllocatedBlocks.FastRemove(m_InterventionBlock);
                m_Blocks.Free(m_InterventionBlock);
                m_InterventionBlock = null;
            }

            if (!id.IsEmpty && !m_AllocatedBlockMap.ContainsKey(id)) {
                m_InterventionBlock = GetBlock(id, state);
                m_InterventionBlock.transform.SetAsFirstSibling();
            }
        }

        public void PopulateData(ModelState state, ModelProgressInfo info, SimRenderMask mask) {
            Populate(state.Simulation, m_AllocatedBlocks, mask, m_StressColor, StressPointCallback, DivergenceCallback);
            if ((mask & SimRenderMask.Intervene) != 0) {
                PopulateTargets(state, info);
            } else {
                ResetInterveneError();
                m_Targets.Reset();
            }
        }

        public void RenderData(SimRenderMask mask, bool onlyFirstPoint = false) {
            Render(onlyFirstPoint ? 1 : 99, m_AllocatedBlocks, mask);
        }

        private void PopulateTargets(ModelState state, ModelProgressInfo info) {
            if (info.Scope) {
                ResetInterveneError();
                m_Targets.Reset();
                foreach(var target in info.Scope.InterventionTargets) {
                    var block = GetBlock(target.Id, state);
                    var targetObj = m_Targets.TempAlloc();
                    block.Intervention = targetObj;
                    targetObj.Object.Layout.SetParent(block.PredictGroup.transform, false);
                    targetObj.Object.MinValue = target.Population - target.Range;
                    targetObj.Object.MaxValue = target.Population + target.Range;
                    targetObj.Object.Background.color = m_InterventionGoalColor;
                    targetObj.Object.LocText.SetText("modeling.intervene.target");

                    m_InterveneRegions.Add(targetObj.Object);
                }
            } else {
                ResetInterveneError();
                m_Targets.Reset();
            }
        }

        public void ClearBlocks() {
            ResetInterveneError();
            m_Blocks.Reset();
            m_AllocatedBlockMap.Clear();
            m_AllocatedBlocks.Clear();
            m_StressIcons.Reset();
            m_DivergenceIcons.Reset();
            m_Targets.Reset();
            m_InterventionBlock = null;
            Array.Clear(m_AllocatedWaterMap, 0, m_AllocatedWaterMap.Length);
        }

        private void ResetInterveneError() {
            if (m_InterveneRegions != null) {
                foreach(var obj in m_InterveneRegions) {
                    obj.Discrepancy.gameObject.SetActive(false);
                }
            }
            m_InterveneRegions.Clear();
            s_FinalBlockYs.Clear();
        }

        #region Retrieving Blocks

        private SimGraphBlock GetBlock(StringHash32 organismId, ModelState state) {
            SimGraphBlock block;
            if (!m_AllocatedBlockMap.TryGetValue(organismId, out block)) {
                block = m_Blocks.Alloc();
                block.ActorId = organismId;
                block.PropertyId = WaterPropertyId.NONE;
                BestiaryDesc info = Assets.Bestiary(organismId);
                block.PrimaryColor = info.Color();
                block.IconBG.SetColor(block.PrimaryColor * 0.5f);
                block.Icon.sprite = state.Simulation.Intervention.Target == info || state.Conceptual.GraphedEntities.Contains(info) ? info.Icon() : m_MissingOrganisms;
                m_AllocatedBlockMap.Add(organismId, block);
                m_AllocatedBlocks.Add(block);
            }
            return block;
        }

        private SimGraphBlock GetBlock(WaterPropertyId propertyId, ModelState state) {
            SimGraphBlock block = m_AllocatedWaterMap[(int) propertyId];
            if (block == null) {
                block = m_Blocks.Alloc();
                block.ActorId = null;
                block.PropertyId = propertyId;
                WaterPropertyDesc info = Assets.Property(propertyId);
                block.PrimaryColor = info.Color();
                block.IconBG.SetColor(block.PrimaryColor * 0.5f);
                block.Icon.sprite = info.Icon();
                m_AllocatedWaterMap[(int) propertyId] = block;
                m_AllocatedBlocks.Add(block);
            }
            return block;
        }

        #endregion // Retrieving Blocks

        #region Populating Blocks

        static public void Populate(SimulationDataCtrl data, ListSlice<SimGraphBlock> blocks, SimRenderMask mask, Color stressColor, GenerateStressPointsDelegate stressCallback, GenerateDivergenceDelegate divergenceCallback) {
            Vector2* pointBuffer = Frame.AllocArray<Vector2>(Simulation.MaxTicks + 1);
            StressedRegion* stressBuffer = Frame.AllocArray<StressedRegion>(Simulation.MaxTicks);
            
            if ((mask & SimRenderMask.Historical) != 0) {
                SimSnapshot* historical = data.RetrieveHistoricalData(out uint countU);
                int count = (int) countU;
                for(int i = 0; i < blocks.Length; i++) {
                    PopulateBlock(historical, count, data.HistoricalProfile, blocks[i], SimRenderMask.Historical, pointBuffer, stressBuffer, stressColor, null);
                }
            }

            if ((mask & SimRenderMask.Player) != 0) {
                SimSnapshot* player = data.RetrievePlayerData(out uint countU);
                int count = (int) countU;
                for(int i = 0; i < blocks.Length; i++) {
                    PopulateBlock(player, count, data.PlayerProfile, blocks[i], SimRenderMask.Player, pointBuffer, stressBuffer, stressColor, stressCallback);
                }
            }

            if ((mask & SimRenderMask.HistoricalPlayer) == SimRenderMask.HistoricalPlayer) {
                for(int i = 0; i < blocks.Length; i++) {
                    SimGraphBlock block = blocks[i];
                    if (SimMath.HasDivergence(block.Historical.Points, block.Player.Points, block.Historical.PointCount) && divergenceCallback != null) {
                        divergenceCallback(blocks[i]);
                    } else {
                        block.Divergence.Free();
                    }
                }
            } else {
                for(int i = 0; i < blocks.Length; i++) {
                    blocks[i].Divergence.Free();
                }
            }

            if ((mask & SimRenderMask.Predict) != 0) {
                SimSnapshot* predict = data.RetrievePredictData(out uint countU);
                int count = (int) countU;
                for(int i = 0; i < blocks.Length; i++) {
                    PopulateBlock(predict, count, data.PredictProfile, blocks[i], SimRenderMask.Predict, pointBuffer, stressBuffer, stressColor, stressCallback);
                }
            }
        }

        static private void PopulateBlock(SimSnapshot* results, int count, SimProfile profile, SimGraphBlock block, SimRenderMask phase, Vector2* pointBuffer, StressedRegion* stressBuffer, Color stressColor, GenerateStressPointsDelegate stressCallback) {
            ref Rect dataRange = ref GetDataRegion(block, phase);
            GraphLineRenderer line = GetLine(block, phase);
            if (block.PropertyId != WaterPropertyId.NONE) {
                dataRange = GenerateWater(results, count, block.PropertyId, block.PrimaryColor, pointBuffer, line);
            } else {
                int idx = profile.IndexOfActorType(block.ActorId);
                switch(phase) {
                    case SimRenderMask.Historical: {
                        dataRange = GenerateHistoricalPopulation(results, count, idx, block.PrimaryColor, pointBuffer, line);
                        break;
                    }
                    case SimRenderMask.Player:
                    case SimRenderMask.Predict: {
                        int stressCount;
                        dataRange = GeneratePlayerPopulation(results, count, idx, block.PrimaryColor, stressColor, pointBuffer, stressBuffer, &stressCount, line);
                        if (stressCallback != null) {
                            stressCallback(block, phase, stressBuffer, stressCount);
                        }
                        break;
                    }
                }
            }
            line.InvalidateScale();
        }

        static private GraphLineRenderer GetLine(SimGraphBlock block, SimRenderMask phase) {
            switch(phase) {
                case SimRenderMask.Historical: {
                    return block.Historical;
                }
                case SimRenderMask.Player: {
                    return block.Player;
                }
                case SimRenderMask.Predict: {
                    return block.Predict;
                }
                default: {
                    throw new ArgumentOutOfRangeException("phase");
                }
            }
        }

        static private ref Rect GetDataRegion(SimGraphBlock block, SimRenderMask phase) {
            switch(phase) {
                case SimRenderMask.Historical: {
                    return ref block.LastRectHistorical;
                }
                case SimRenderMask.Player: {
                    return ref block.LastRectPlayer;
                }
                case SimRenderMask.Predict: {
                    return ref block.LastRectPredict;
                }
                default: {
                    throw new ArgumentOutOfRangeException("phase");
                }
            }
        }

        #endregion // Populating Blocks

        #region Rendering Blocks

        static public void Render(int pointCount, ListSlice<SimGraphBlock> blocks, SimRenderMask mask) {
            bool showHistorical = pointCount > 0 && (mask & SimRenderMask.Historical) != 0;
            bool showPlayer = pointCount > 0 && (mask & SimRenderMask.Player) != 0;
            bool showFill = showHistorical && showPlayer && (mask & SimRenderMask.Fill) != 0;
            bool showPredict = !showHistorical && !showPlayer && pointCount > 0 && (mask & SimRenderMask.Predict) != 0;
            bool showIntervene = showPredict && (mask & SimRenderMask.Intervene) != 0;

            for(int i = 0; i < blocks.Length; i++) {
                SimGraphBlock block = blocks[i];

                bool changed = false;
                changed |= Ref.Replace(ref block.Historical.PointRenderCount, pointCount);
                changed |= Ref.Replace(ref block.Player.PointRenderCount, pointCount);
                changed |= Ref.Replace(ref block.Predict.PointRenderCount, pointCount);

                if (showHistorical && showPlayer) {
                    Rect bounds = block.LastRectHistorical;
                    SimMath.CombineBounds(ref bounds, block.LastRectPlayer);
                    SimMath.FinalizeBounds(ref bounds);
                    block.LastRect = bounds;

                    changed |= block.Historical.ApplyScale(bounds);
                    changed |= block.Player.ApplyScale(bounds);
                    if (changed && showFill) {
                        block.Fill.LinesDirty();
                    }

                    block.IconPin.SetAnchorY(block.Historical.Points[0].y);
                } else if (showHistorical) {
                    Rect bounds = block.LastRectHistorical;
                    SimMath.FinalizeBounds(ref bounds);
                    block.LastRect = bounds;

                    changed |= block.Historical.ApplyScale(bounds);

                    block.IconPin.SetAnchorY(block.Historical.Points[0].y);
                } else if (showPlayer) {
                    Rect bounds = block.LastRectPlayer;
                    SimMath.FinalizeBounds(ref bounds);
                    block.LastRect = bounds;

                    changed |= block.Player.ApplyScale(bounds);
                } else if (showPredict) {
                    Rect bounds = block.LastRectPredict;
                    if (showIntervene && block.Intervention.IsAllocated) {
                        bounds.yMax = Math.Max(bounds.yMax, block.Intervention.Object.MaxValue);
                    }
                    SimMath.FinalizeBounds(ref bounds);
                    block.LastRect = bounds;

                    changed |= block.Predict.ApplyScale(bounds);

                    // temp setting
                    block.IconPin.SetAnchorY(0f);
                    if (block.Intervention.IsAllocated) {
                        if (block.Predict.PointCount != 0) {
                            float newY = block.IconPin.position.y +
                                block.Predict.Points[block.Predict.PointCount - 1].y / blocks.Length;
                            s_FinalBlockYs.Add(newY);
                        }
                    }

                    if (showIntervene && pointCount < 2) {
                        block.IconPin.SetAnchorY(0.5f);
                    } else {
                        block.IconPin.SetAnchorY(block.Predict.Points[0].y);
                    }

                }

                if (showIntervene && block.Intervention.IsAllocated) {
                    RenderIntervention(block.Intervention, block.LastRect);
                }

                RenderStressPoints(block, mask, pointCount);

                if (showFill) {
                    block.Historical.OverrideColor = AQColors.DarkerTeal;
                    block.Player.OverrideColor = AQColors.DarkerTeal;
                } else {
                    block.Historical.OverrideColor = null;
                    block.Player.OverrideColor = null;
                }

                block.Historical.enabled = showHistorical;
                block.Player.enabled = showPlayer;
                block.Predict.enabled = showPredict;
                block.Fill.enabled = showFill;
                if (block.Intervention.IsAllocated) {
                    block.Intervention.Object.Layout.gameObject.SetActive(showIntervene);
                }

                GraphDivergencePoint divergence = block.Divergence.Object;
                if (divergence != null) {
                    block.Fill.AnalyzeRegions();
                    if (showFill && block.Fill.Regions.Count > 0) {
                        GraphLineFillRenderer.Region region = block.Fill.Regions[0];
                        divergence.Sign = region.Sign;
                        ((RectTransform) divergence.transform).SetAnchors(region.Center);
                        divergence.gameObject.SetActive(true);
                        divergence.transform.SetAsLastSibling();
                    } else {
                        divergence.gameObject.SetActive(false);
                    }
                }

                if (changed) {
                    block.Historical.SubmitChanges();
                    block.Player.SubmitChanges();
                    block.Predict.SubmitChanges();
                }
            }
        }

        static private void RenderIntervention(GraphTargetRegion region, Rect rect) {
            float min = MathUtils.Remap(region.MinValue, rect.yMin, rect.yMax, 0, 1);
            float max = MathUtils.Remap(region.MaxValue, rect.yMin, rect.yMax, 0, 1);
            if (min > 1) { min = 0; }
            region.Layout.SetAnchorsY(min, max);
        }

        static private void RenderStressPoints(SimGraphBlock block, SimRenderMask mask, int pointCount) {
            bool enabled = (block.StressPointMask & mask) != 0 && (mask & SimRenderMask.Fill) == 0;
            if (block.StressPoints != null && block.StressPoints.Count > 0) {
                GraphLineRenderer line = GetLine(block, block.StressPointMask);
                for(int i = 0; i < block.StressPoints.Count; i++) {
                    var point = block.StressPoints[i].Object;
                    RectTransform r = (RectTransform) point.transform;
                    r.SetAnchors(line.Points[point.Index]);
                    point.Icon.enabled = enabled && point.Index < pointCount;
                }
            }
        }

        #endregion // Rendering Blocks

        #region Generating Lines

        static public Rect GenerateHistoricalPopulation(SimSnapshot* results, int count, int actorIdx, Color actorColor, Vector2* pointBuffer, GraphLineRenderer renderer) {
            Rect range = FillPopulationBuffer(results, count, actorIdx, pointBuffer, null, null, false);
            renderer.Colors = Array.Empty<Color>();
            renderer.SetColor(actorColor);
            renderer.EnsurePointBuffer(count);
            Unsafe.CopyArray(pointBuffer, count, renderer.Points);
            renderer.PointCount = count;
            renderer.PointRenderCount = count;
            return range;
        }

        static public Rect GeneratePlayerPopulation(SimSnapshot* results, int count, int actorIdx, Color actorColor, Color stressColor, Vector2* pointBuffer, StressedRegion* stressBuffer, int* stressCount, GraphLineRenderer renderer) {
            Rect range = FillPopulationBuffer(results, count, actorIdx, pointBuffer, stressBuffer, stressCount, true);
            renderer.EnsureColorBuffer(count);
            renderer.EnsurePointBuffer(count);
            Unsafe.CopyArray(pointBuffer, count, renderer.Points);
            Color[] colors = renderer.Colors;
            for(int i = 0; i < count; i++) {
                colors[i] = actorColor;
            }
            int stressCountFinal = *stressCount;
            for(int i = 0; i < stressCountFinal; i++) {
                int start = stressBuffer[i].StartIdx;
                int end = stressBuffer[i].EndIdx;
                for(int j = start; j < end; j++) {
                    colors[j] = stressColor;
                }
            }
            renderer.PointCount = count;
            renderer.PointRenderCount = count;
            return range;
        }

        static public Rect GenerateWater(SimSnapshot* results, int count, WaterPropertyId waterProperty, Color propertyColor, Vector2* pointBuffer, GraphLineRenderer renderer) {
            Rect range = FillWaterBuffer(results, count, waterProperty, pointBuffer);
            renderer.Colors = Array.Empty<Color>();
            renderer.SetColor(propertyColor);
            renderer.EnsurePointBuffer(count);
            Unsafe.CopyArray(pointBuffer, count, renderer.Points);
            renderer.PointCount = count;
            renderer.PointRenderCount = count;
            return range;
        }

        static private Rect FillPopulationBuffer(SimSnapshot* results, int resultCount, int organismIdx, Vector2* pointBuffer, StressedRegion* stressBuffer, int* stressRegionCount, bool trackStressed) {
            if (stressRegionCount != null) {
                *stressRegionCount = 0;
            }
            bool currentStress = false, stress = false;

            Vector2* pointHead = pointBuffer;
            StressedRegion* stressHead = stressBuffer;
            
            for(int i = 0; i < resultCount; i++) {
                pointHead->x = i;
                pointHead->y = results->Populations[organismIdx];

                if (trackStressed) {
                    stress = results->StressedRatio[organismIdx] >= 64; // stressed ratio values are 0 - 128; half is 64
                    if (stress != currentStress) {
                        ushort idx = (ushort) Math.Max(0, i - 1);
                        if (stress) {
                            stressHead->StartIdx = idx;
                        } else {
                            stressHead->EndIdx = idx;
                            (*stressRegionCount)++;
                            stressHead++;
                        }
                        currentStress = stress;
                    }
                }

                results++;
                pointHead++;
            }

            if (currentStress) {
                (*stressRegionCount)++;
                stressHead->EndIdx = (ushort) (resultCount - 1);
            }

            return SimMath.CalculateBounds(pointBuffer, resultCount);
        }

        static private Rect FillWaterBuffer(SimSnapshot* results, int resultCount, WaterPropertyId propertyId, Vector2* pointBuffer) {
            Vector2* pointHead = pointBuffer;
            
            for(int i = 0; i < resultCount; i++) {
                pointHead->x = i;
                pointHead->y = results->Water[propertyId];

                results++;
                pointHead++;
            }

            return SimMath.CalculateBounds(pointBuffer, resultCount);
        }

        #endregion // Generating Lines

        #region Handlers

        private void OnInterveneError() {
            if (m_InterveneRegions == null || m_InterveneRegions.Count == 0) { return; }

            for (int i = 0; i < m_InterveneRegions.Count; i++) {
                GraphTargetRegion region = m_InterveneRegions[i];
                if (region.Background == null) { continue; }

                // TODO: Check if this *specific* region is incorrect
                region.Background.color = m_InterventionDiscrepancyColor;
                region.LocText.SetText("modeling.noIntervenePopup.header");

                region.Discrepancy.gameObject.SetActive(true);

                region.Discrepancy.Layout.SetPosition(new Vector3(0, 0, 0), Axis.XY, Space.Self);
                
                Vector3 upperPos = new Vector3(region.Layout.position.x, region.Layout.position.y, 1);
                Vector3 lowerPos = new Vector3(region.Layout.position.x, s_FinalBlockYs[i], 1);

                if (upperPos.y < lowerPos.y) {
                    Vector3 tempPos = upperPos;
                    upperPos = lowerPos;
                    lowerPos = tempPos;
                }

                Vector3 midPos = (upperPos + lowerPos) / 2;

                region.Discrepancy.UpperDot.gameObject.transform.position = new Vector3(region.Discrepancy.UpperDot.gameObject.transform.position.x, upperPos.y, 1);
                region.Discrepancy.LowerDot.gameObject.transform.position = new Vector3(region.Discrepancy.LowerDot.gameObject.transform.position.x, lowerPos.y, 1);
                region.Discrepancy.Circle.gameObject.transform.position = new Vector3(region.Discrepancy.Circle.gameObject.transform.position.x, midPos.y, 1);
                region.Discrepancy.Line.gameObject.transform.position = new Vector3(region.Discrepancy.Line.gameObject.transform.position.x, midPos.y, 1);

                // TODO: create individual lines connecting upper to lower dot, instead of just adjusting scale
                float dotBuffer = region.Discrepancy.UpperDot.rectTransform.rect.height / 2;
                region.Discrepancy.Line.rectTransform.sizeDelta = new Vector2(region.Discrepancy.Line.rectTransform.rect.width,
                    Mathf.Abs(region.Discrepancy.UpperDot.gameObject.transform.localPosition.y - region.Discrepancy.LowerDot.gameObject.transform.localPosition.y + dotBuffer));
            }
        }

        #endregion // Handlers

        public delegate void GenerateStressPointsDelegate(SimGraphBlock block, SimRenderMask phase, StressedRegion* stressRegions, int stressRegionCount);
        public delegate void GenerateDivergenceDelegate(SimGraphBlock block);
    }
}