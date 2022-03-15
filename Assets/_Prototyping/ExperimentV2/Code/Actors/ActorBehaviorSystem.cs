using System;
using System.Collections;
using System.Collections.Generic;
using Aqua;
using Aqua.Scripting;
using BeauRoutine;
using BeauUtil;
using BeauUtil.Debugger;
using ScriptableBake;
using UnityEngine;

namespace ProtoAqua.ExperimentV2 {
    public sealed class ActorBehaviorSystem : MonoBehaviour, IBaked {
        static private bool s_Configured;
        private const float SpeedUpLifetimeThreshold = 60;

        public delegate bool HasFactDelegate(StringHash32 inFactId);

        private enum Phase {
            Spawning,
            Executing
        }

        [SerializeField] private SelectableTank m_Tank = null;
        [SerializeField, HideInInspector] private ActorAllocator m_Allocator = null;

        [NonSerialized] private Phase m_Phase = Phase.Spawning;
        [NonSerialized] public ActorWorld World;
        [NonSerialized] private bool m_AllowTick = false;

        public void Initialize() {
            if (World != null)
                return;

            World = new ActorWorld(m_Allocator, m_Tank.Bounds, null, OnFree, 16, m_Tank, m_Tank.Controller);

            ConfigureStates();
        }

        public void Begin() {
            if (!m_AllowTick) {
                m_AllowTick = true;
            }
        }

        public void End() {
            if (m_AllowTick) {
                m_AllowTick = false;
                foreach(var actor in World.Actors) {
                    actor.ActionAnimation.Stop();
                }
            }
        }

        private void LateUpdate() {
            if (!m_AllowTick || (m_Tank.CurrentState & TankState.Running) == 0 || Script.IsPaused)
                return;

            TickBehaviors(Time.deltaTime);
        }

        #region Critters

        public void Alloc(StringHash32 inId) {
            ActorWorld.AllocWithDefaultCount(World, inId);
        }

        public void Alloc(StringHash32 inId, int inCount) {
            ActorWorld.Alloc(World, inId, inCount);
        }

        public void FreeAll(StringHash32 inId) {
            ActorWorld.FreeAll(World, inId);
        }

        private void OnFree(ActorInstance inCritter, ActorWorld inWorld) {
            if (inCritter.CurrentState == ActorStateId.Dead) {
                inWorld.EnvDeaths++;
            }
            ActorWorld.ModifyPopulation(inWorld, inCritter.Definition.Id, -1);
            ActorInstance.ReleaseTargetsAndInteractions(inCritter, inWorld);
        }

        #endregion // Critters

        #region Env State

        public void UpdateEnvState(WaterPropertyBlockF32 inEnvironment) {
            ActorWorld.SetWaterState(World, inEnvironment);
        }

        public void ClearEnvState() {
            ActorWorld.SetWaterState(World, null);
        }

        #endregion // Env State

        #region Tick

        static public void ConfigureStates() {
            if (s_Configured) {
                return;
            }

            ActorInstance.ConfigureActionMethods(ActorActionId.Idle, OnActorIdleStart, null, null);
            ActorInstance.ConfigureActionMethods(ActorActionId.Hungry, OnActorHungryStart, null, ActorHungryAnimation);
            ActorInstance.ConfigureActionMethods(ActorActionId.Eating, OnActorEatStart, null, ActorEatAnimation);
            ActorInstance.ConfigureActionMethods(ActorActionId.BeingEaten, OnActorBeingEatenStart, OnActorBeingEatenEnd, ActorBeingEatenAnimation);
            ActorInstance.ConfigureActionMethods(ActorActionId.Dying, OnActorDyingStart, null, ActorDyingAnimation);
            ActorInstance.ConfigureInteractionMethods(OnInteractionAcquired, OnInteractionReleased);
            s_Configured = true;
        }

        public void TickBehaviors(float inDeltaTime) {
            if (IsSpawningComplete()) {
                World.Lifetime += inDeltaTime;
            }
        }

        private bool IsSpawningComplete() {
            if (m_Phase == Phase.Spawning) {
                foreach (var critter in World.Actors) {
                    if (critter.CurrentAction == ActorActionId.Spawning)
                        return false;
                }

                FinalizeCritterInitialization();
                m_Phase = Phase.Executing;
                return true;
            } else {
                return true;
            }
        }

        public bool AllSpawned() {
            foreach(var critter in World.Actors) {
                if (critter.CurrentAction == ActorActionId.Spawning) {
                    return false;
                }
            }
            return true;
        }

        private void FinalizeCritterInitialization() {
            World.Lifetime = 0;
            World.EnvDeaths = 0;

            ActorInstance critter;
            for (int i = World.Actors.Count - 1; i >= 0; i--) {
                critter = World.Actors[i];
                if (critter.CurrentState == ActorStateId.Dead) {
                    ActorInstance.SetActorAction(critter, ActorActionId.Dying, World);
                } else {
                    ActorInstance.SetActorAction(critter, ActorActionId.Idle, World);
                }
            }

            ActorWorld.RegenerateActorCounts(World);
        }

        static public int GetPotentialNewObservations(ActorWorld inWorld, HasFactDelegate inDelegate, ICollection<BFBase> outFactIds) {
            Assert.NotNull(inDelegate);

            int factCount = 0;
            ActorDefinition def;
            ActorStateId state;
            ActorDefinition.ValidEatTarget[] possibleEats;
            foreach (var critterCount in inWorld.ActorCounts) {
                if (critterCount.Population == 0)
                    continue;

                def = inWorld.Allocator.Define(critterCount.Id);
                state = def.StateEvaluator.Evaluate(inWorld.Water);
                possibleEats = ActorDefinition.GetEatTargets(def, state);

                foreach (var eat in possibleEats) {
                    if (inDelegate(eat.FactId) || ActorWorld.GetPopulation(inWorld, eat.TargetId) == 0)
                        continue;

                    factCount++;
                    if (outFactIds != null) {
                        outFactIds.Add(Assets.Fact(eat.FactId));
                    }
                }
            }

            return factCount;
        }

        public bool IsFactObservable(BFBase inFact) {
            switch (inFact.Type) {
                case BFTypeId.Eat: {
                        BFEat eat = (BFEat)inFact;
                        return ActorWorld.GetPopulation(World, eat.Parent.Id()) > 0 && ActorWorld.GetPopulation(World, eat.Critter.Id()) > 0;
                    }
            }

            return false;
        }

        #endregion // Tick

        #region Actor States

        #region Dying

        static private void OnActorDyingStart(ActorInstance inActor, ActorWorld inWorld, ActorActionId inPrev) {
            ActorInstance.ReleaseTargetsAndInteractions(inActor, inWorld);
        }

        static private IEnumerator ActorDyingAnimation(ActorInstance inActor, ActorWorld inWorld) {
            yield return Tween.Color(inActor.ColorAdjust.Color, Color.black, inActor.ColorAdjust.SetColor, 1);
            ActorWorld.EmitEmoji(inWorld, inActor, "Dead", null, 5);
            yield return Tween.Float(1, 0, inActor.ColorAdjust.SetAlpha, 0.5f);
            ActorWorld.EmitEmoji(inWorld, inActor, "Dead", null, 12);
            ActorWorld.Free(inWorld, inActor);
        }

        #endregion // Dying

        #region Idle

        static private void OnActorIdleStart(ActorInstance inActor, ActorWorld inWorld, ActorActionId inPrev) {
            ActorInstance.ReleaseInteraction(inActor, inWorld);
            ActorInstance.ReleaseTarget(inActor, inWorld);

            if (!inActor.Definition.IsPlant && inActor.Definition.Movement.MoveType != ActorDefinition.MovementTypeId.Stationary)
                inActor.ActionAnimation.Replace(inActor, ActorIdleAnimation(inActor, inWorld));
        }

        static private IEnumerator ActorIdleAnimation(ActorInstance inActor, ActorWorld inWorld) {
            ActorDefinition def = inActor.Definition;
            bool bLimitMovement = ActorDefinition.GetEatTargets(def, inActor.CurrentState).Length > 0;

            float intervalMultiplier = ActorDefinition.GetMovementIntervalMultiplier(def, inActor.CurrentState);
            float movementSpeed = def.Movement.MovementSpeed * ActorDefinition.GetMovementSpeedMultiplier(def, inActor.CurrentState);
            int moveCount;
            if (!bLimitMovement) {
                moveCount = 0;
            } else {
                if (inWorld.Lifetime >= SpeedUpLifetimeThreshold) {
                    moveCount = RNG.Instance.Next(0, 2);
                } else {
                    moveCount = RNG.Instance.Next(1, 3);
                }
            }
            Vector3 current;
            Vector3 target;
            float duration;
            float interval = intervalMultiplier * RNG.Instance.NextFloat() * (def.Movement.MovementInterval + def.Movement.MovementIntervalRandom);
            yield return interval;

            while (!bLimitMovement || moveCount-- > 0) {
                current = inActor.CachedTransform.localPosition;
                target = ActorDefinition.FindRandomTankLocationInRange(RNG.Instance, inWorld.WorldBounds, inActor.CachedTransform.localPosition, def.Movement.MovementIdleDistance, def.Spawning.AvoidTankTopBottomRadius, def.Spawning.AvoidTankSidesRadius);
                duration = Vector3.Distance(current, target) / movementSpeed;
                interval = intervalMultiplier * (def.Movement.MovementInterval + RNG.Instance.NextFloat(def.Movement.MovementIntervalRandom));

                yield return inActor.CachedTransform.MoveTo(target, duration, Axis.XY, Space.Self).Ease(def.Movement.MovementCurve);
                yield return interval;

                if (inWorld.Lifetime >= SpeedUpLifetimeThreshold && moveCount > 1) {
                    moveCount = 1;
                }
            }

            ActorInstance.SetActorAction(inActor, ActorActionId.Hungry, inWorld);
        }

        #endregion // Idle

        #region Hungry

        static private void OnActorHungryStart(ActorInstance inActor, ActorWorld inWorld, ActorActionId inPrev) {
            ActorInstance eatTarget = FindGoodEatTarget(inActor, inWorld);
            if (eatTarget == null || !ActorInstance.AcquireTarget(inActor, eatTarget, inWorld)) {
                ActorInstance.SetActorAction(inActor, ActorActionId.Idle, inWorld);
            }
        }

        static private IEnumerator ActorHungryAnimation(ActorInstance inActor, ActorWorld inWorld) {
            ActorDefinition def = inActor.Definition;
            ActorInstance target = inActor.CurrentTargetActor;

            if (!ActorInstance.IsValidTarget(target)) {
                ActorInstance.SetActorAction(inActor, ActorActionId.Idle, inWorld);
                yield break;
            }

            float movementSpeed = def.Movement.MovementSpeed * def.Eating.MovementMultiplier * ActorDefinition.GetMovementSpeedMultiplier(def, inActor.CurrentState);

            Vector3 currentPos;
            Vector3 targetPos;
            Vector3 targetPosOffset = FindGoodEatPositionOffset(target);
            Vector3 distanceVector;
            while (ActorInstance.IsValidTarget(target)) {
                currentPos = inActor.CachedTransform.localPosition;
                targetPos = target.CachedTransform.localPosition + targetPosOffset;
                targetPos.z = currentPos.z;
                targetPos = ActorDefinition.ClampToTank(inWorld.WorldBounds, targetPos, def.Spawning.AvoidTankTopBottomRadius, def.Spawning.AvoidTankSidesRadius);

                distanceVector = targetPos - currentPos;
                if (distanceVector.sqrMagnitude < 0.05f) {
                    if (ActorInstance.AcquireInteraction(inActor, target, inWorld)) {
                        ActorInstance.SetActorAction(inActor, ActorActionId.Eating, inWorld);
                        yield break;
                    } else {
                        ActorInstance.SetActorAction(inActor, ActorActionId.Idle, inWorld);
                    }
                } else {
                    float distanceToMove = movementSpeed * Routine.DeltaTime;
                    float distanceScalar = distanceVector.magnitude;
                    if (distanceScalar < movementSpeed) {
                        distanceToMove *= 1f - 0.7f * (distanceScalar / movementSpeed);
                    }
                    currentPos = Vector3.MoveTowards(currentPos, targetPos, distanceToMove);
                    inActor.CachedTransform.SetPosition(currentPos, Axis.XY, Space.Self);
                }

                yield return null;
            }

            ActorInstance.SetActorAction(inActor, ActorActionId.Idle, inWorld);
        }

        #endregion // Hungry

        #region Eat

        static private void OnActorEatStart(ActorInstance inActor, ActorWorld inWorld, ActorActionId inPrev) {
            ActorInstance.ResetAnimationTransform(inActor);

            if (inActor.IdleAnimation)
                inActor.IdleAnimation.AnimationScale = 0;
        }

        static private IEnumerator ActorEatAnimation(ActorInstance inActor, ActorWorld inWorld) {
            ActorInstance target = inActor.CurrentInteractionActor;
            BFEat eatRule = Assets.Fact<BFEat>(ActorDefinition.GetEatTarget(inActor.Definition, target.Definition.Id, inActor.CurrentState).FactId);

            bool bHas = Save.Bestiary.HasFact(eatRule.Id);
            using (var table = TempVarTable.Alloc()) {
                table.Set("factId", eatRule.Id);
                table.Set("newFact", !bHas);
                Services.Script.TriggerResponse(ExperimentTriggers.CaptureCircleVisible, table);
            }

            using (var capture = ObservationTank.CaptureCircle(eatRule.Id, inActor, inWorld, bHas)) {
                switch (inActor.Definition.Eating.EatType) {
                    case ActorDefinition.EatTypeId.Nibble: {
                            int nibbleCount = RNG.Instance.Next(3, 5);
                            while (nibbleCount-- > 0) {
                                yield return EatPulse(inActor, 0.2f);
                                ActorWorld.EmitEmoji(inWorld, inActor, eatRule, "Eat");
                                if (nibbleCount > 0)
                                    yield return 0.3f;
                            }
                            break;
                        }

                    case ActorDefinition.EatTypeId.LargeBite:
                    default: {
                            yield return EatPulse(inActor, 0.3f);
                            ActorWorld.EmitEmoji(inWorld, inActor, eatRule, "Eat");
                            yield return 2;
                            break;
                        }
                }

                if (target.Definition.FreeOnEaten)
                    ActorWorld.Free(inWorld, target);

                yield return 1;

                if (capture.IsValid()) {
                    bHas = Save.Bestiary.HasFact(eatRule.Id);
                    using (var table = TempVarTable.Alloc()) {
                        table.Set("factId", eatRule.Id);
                        table.Set("newFact", !bHas);
                        Services.Script.TriggerResponse(ExperimentTriggers.CaptureCircleExpired, table);
                    }
                }
            }

            ActorInstance.SetActorAction(inActor, ActorActionId.Idle, inWorld);
        }

        static private IEnumerator EatPulse(ActorInstance inInstance, float inDuration) {
            yield return inInstance.CachedTransform.ScaleTo(1.05f, inDuration, Axis.XY).Yoyo(true).Ease(Curve.CubeOut).RevertOnCancel();
            Services.Audio.PostEvent("urchin_eat");
        }

        static private void OnActorEatEnd(ActorInstance inActor, ActorWorld inWorld, ActorActionId inNext) {
            if (inActor.IdleAnimation)
                inActor.IdleAnimation.AnimationScale = 1;
        }

        #endregion // Eat

        #region Being Eaten

        static private void OnActorBeingEatenStart(ActorInstance inActor, ActorWorld inWorld, ActorActionId inPrev) {
            ActorInstance.ReleaseTargetsAndInteractions(inActor, inWorld);
            ActorInstance.ResetAnimationTransform(inActor);

            if (inActor.IdleAnimation)
                inActor.IdleAnimation.AnimationScale = 0;
        }

        static private IEnumerator ActorBeingEatenAnimation(ActorInstance inActor, ActorWorld inWorld) {
            yield return inActor.CachedTransform.MoveTo(inActor.CachedTransform.localPosition.x + 0.01f, 0.15f, Axis.X, Space.Self)
                .Wave(Wave.Function.Sin, 1).Loop().RevertOnCancel();
        }

        static private void OnActorBeingEatenEnd(ActorInstance inActor, ActorWorld inWorld, ActorActionId inNext) {
            ActorInstance.ResetAnimationTransform(inActor);

            if (inActor.IdleAnimation)
                inActor.IdleAnimation.AnimationScale = 1;
        }

        #endregion // Being Eaten

        static private void OnInteractionAcquired(ActorInstance inActor, ActorWorld inWorld) {
            ActorInstance.SetActorAction(inActor, ActorActionId.BeingEaten, inWorld);
        }

        static private void OnInteractionReleased(ActorInstance inActor, ActorWorld inWorld) {
            if (inActor.IncomingInteractionCount > 0 || inActor.CurrentAction != ActorActionId.BeingEaten)
                return;

            ActorInstance.SetActorAction(inActor, ActorActionId.Idle, inWorld);
        }

        #endregion // Actor States

        #region Eating

        static private RingBuffer<PriorityValue<ActorInstance>> s_EatTargetBuffer;

        static private ActorInstance FindGoodEatTarget(ActorInstance inInstance, ActorWorld inWorld) {
            RingBuffer<PriorityValue<ActorInstance>> eatBuffer = s_EatTargetBuffer ?? (s_EatTargetBuffer = new RingBuffer<PriorityValue<ActorInstance>>(16, RingBufferMode.Expand));
            ActorDefinition.ValidEatTarget[] validTargets = ActorDefinition.GetEatTargets(inInstance.Definition, inInstance.CurrentState);
            eatBuffer.Clear();
            Vector3 instancePosition = inInstance.CachedTransform.localPosition;
            Vector3 critterPosition;
            float critterDistance, priority;

            foreach (var critter in inWorld.Actors) {
                if (!IsValidEatTarget(validTargets, critter))
                    continue;

                critterPosition = critter.CachedTransform.localPosition;
                critterDistance = Vector3.Distance(instancePosition, critterPosition);
                priority = (critter.Definition.TargetLimit - critter.IncomingTargetCount) * (5 - critterDistance);
                eatBuffer.PushBack(new PriorityValue<ActorInstance>(critter, priority));
            }

            if (eatBuffer.Count == 0) {
                return null;
            } else if (eatBuffer.Count == 1) {
                return eatBuffer.PopFront();
            } else {
                eatBuffer.Sort();
                ActorInstance actor = eatBuffer.PopFront().Value;
                eatBuffer.Clear();
                return actor;
            }
        }

        static public bool HasPotentialEatTarget(ActorInstance inInstance, ActorWorld inWorld) {
            ActorDefinition.ValidEatTarget[] validTargets = ActorDefinition.GetEatTargets(inInstance.Definition, inInstance.CurrentState);

            foreach (var critter in inWorld.Actors) {
                if (!IsValidEatTarget(validTargets, critter))
                    continue;

                return true;
            }

            return false;
        }

        static private Vector3 FindGoodEatPositionOffset(ActorInstance inEatTarget) {
            return RNG.Instance.NextVector2(inEatTarget.Definition.EatOffsetRange);
        }

        static private bool IsValidEatTarget(ActorDefinition.ValidEatTarget[] inTargets, ActorInstance inPossibleTarget) {
            if (!ActorInstance.IsValidTarget(inPossibleTarget))
                return false;

            for (int i = 0, length = inTargets.Length; i < length; i++) {
                if (inTargets[i].TargetId == inPossibleTarget.Definition.Id)
                    return true;
            }

            return false;
        }

        #endregion // Eating

        public void ClearAll() {
            End();
            if (World != null) {
                ActorWorld.FreeAll(World);
                ActorWorld.SetWaterState(World, null);
            }
            m_Phase = Phase.Spawning;
            World.Lifetime = 0;
            World.EnvDeaths = 0;
        }

        public void ClearActors() {
            if (World != null) {
                ActorWorld.FreeAll(World);
            }
            m_Phase = Phase.Spawning;
        }

        #region IBaked

        #if UNITY_EDITOR

        int IBaked.Order { get { return 0; } }

        bool IBaked.Bake(BakeFlags flags) {
            m_Tank = GetComponentInParent<SelectableTank>();
            m_Allocator = FindObjectOfType<ActorAllocator>();
            return true;
        }

        #endif // UNITY_EDITOR

        #endregion // IBaked
    }
}