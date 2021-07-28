using System;
using BeauRoutine;
using BeauUtil;
using UnityEngine;
using BeauPools;
using Aqua.Animation;
using Aqua;
using System.Collections;

namespace ProtoAqua.ExperimentV2
{
    public sealed class ActorInstance : MonoBehaviour, IPooledObject<ActorInstance>
    {
        public const float DropSpawnAnimationDistance = 8;
        public delegate void ActionStartDelegate(ActorInstance inActor, ActorWorld inSystem);

        #region Inspector

        public Transform CachedTransform;
        public Collider2D CachedCollider;
        public AmbientTransform IdleAnimation;
        public ColorGroup ColorAdjust;

        #endregion // Inspector
        
        [NonSerialized] public ActorDefinition Definition;

        [NonSerialized] public ActorActionId CurrentAction;
        [NonSerialized] public ActorStateId CurrentState;
        [NonSerialized] public Routine ActionAnimation;
        [NonSerialized] public TempAlloc<Transform> StateEffect;
        [NonSerialized] public Routine StateAnimation;
        [NonSerialized] public bool InWater;

        #region IPoolAllocHandler

        void IPooledObject<ActorInstance>.OnConstruct(IPool<ActorInstance> inPool) { }
        void IPooledObject<ActorInstance>.OnDestruct() { }
        void IPooledObject<ActorInstance>.OnAlloc() { }
        void IPooledObject<ActorInstance>.OnFree()
        {
            CurrentState = ActorStateId.Alive;
            CurrentAction = ActorActionId.Waiting;
            StateAnimation.Stop();
            ActionAnimation.Stop();
            Ref.Dispose(ref StateEffect);
            InWater = false;
            if (ColorAdjust)
                ColorAdjust.SetColor(Color.white);
            if (IdleAnimation)
                IdleAnimation.AnimationScale = 1;
        }

        #endregion // IPoolAllocHandler

        #region Methods

        static public bool SetActorState(ActorInstance ioInstance, ActorStateId inStateId, ActorWorld inWorld)
        {
            ActorStateId oldState = ioInstance.CurrentState;

            if (!Ref.Replace(ref ioInstance.CurrentState, inStateId))   
                return false;

            Ref.Dispose(ref ioInstance.StateEffect);
            ioInstance.StateAnimation.Stop();

            switch(oldState)
            {
                case ActorStateId.Stressed:
                    OnEndStressedState(ioInstance, inWorld);
                    break;

                case ActorStateId.Dead:
                    OnEndDeadState(ioInstance, inWorld);
                    break;
            }

            switch(inStateId)
            {
                case ActorStateId.Stressed:
                    OnBeginStressedState(ioInstance, inWorld);
                    break;

                case ActorStateId.Dead:
                    OnBeginDeadState(ioInstance, inWorld);
                    break;
            }
            return true;
        }

        static public bool SetActorAction(ActorInstance ioInstance, ActorActionId inActionId)
        {
            if (!Ref.Replace(ref ioInstance.CurrentAction, inActionId))   
                return false;

            ioInstance.ActionAnimation.Stop();
            return true;
        }

        static public bool SetActorAction(ActorInstance ioInstance, ActorActionId inActionId, ActorWorld inWorld, ActionStartDelegate inOnSet)
        {
            if (!Ref.Replace(ref ioInstance.CurrentAction, inActionId))   
                return false;

            ioInstance.ActionAnimation.Stop();
            inOnSet?.Invoke(ioInstance, inWorld);
            return true;
        }

        static public void ForceActorAction(ActorInstance ioInstance, ActorActionId inActionId, ActorWorld inWorld, ActionStartDelegate inOnSet)
        {
            ioInstance.CurrentAction = inActionId;
            ioInstance.ActionAnimation.Stop();
            inOnSet?.Invoke(ioInstance, inWorld);
        }

        #endregion // Methods
    
        #region Animations

        #region Spawning

        static public void StartSpawning(ActorInstance inInstance, ActorWorld inWorld)
        {
            ActorDefinition def = inInstance.Definition;
            Vector3 targetPos = ActorDefinition.FindRandomSpawnLocation(RNG.Instance, inWorld.WorldBounds, def.Spawning);

            if (inInstance.IdleAnimation)
                inInstance.IdleAnimation.AnimationScale = 0;

            if (def.Spawning.SpawnType == ActorDefinition.SpawnTypeId.Bottom)
            {
                inInstance.CachedTransform.SetPosition(targetPos, Axis.XYZ, Space.Self);
                inInstance.CachedTransform.SetScale(0);
                inInstance.ActionAnimation.Replace(inInstance, SproutFromBottom(inInstance, inWorld));
            }
            else
            {
                Vector3 offsetPos = targetPos;
                offsetPos.y += DropSpawnAnimationDistance;
                inInstance.CachedTransform.SetPosition(offsetPos, Axis.XYZ, Space.Self);
                inInstance.ActionAnimation.Replace(inInstance, FallToPosition(inInstance, targetPos, inWorld));
            }
        }

        static private IEnumerator SproutFromBottom(ActorInstance inInstance, ActorWorld inWorld)
        {
            yield return inInstance.CachedTransform.ScaleTo(1, 0.2f).Ease(Curve.CubeOut);
            SetActorAction(inInstance, ActorActionId.Waiting, inWorld, OnEnterWaiting);
        }

        static private IEnumerator FallToPosition(ActorInstance inInstance, Vector3 inTargetPosition, ActorWorld inWorld)
        {
            yield return inInstance.CachedTransform.MoveTo(inTargetPosition, 0.2f, Axis.XYZ, Space.Self).Ease(Curve.CubeOut);
            SetActorAction(inInstance, ActorActionId.Waiting, inWorld, OnEnterWaiting);
        }

        static private void OnEnterWaiting(ActorInstance inInstance, ActorWorld inWorld)
        {
            if (inInstance.IdleAnimation)
                inInstance.IdleAnimation.AnimationScale = 1;
        }

        #endregion // Spawning

        #region States

        static private void OnBeginStressedState(ActorInstance inInstance, ActorWorld inWorld)
        {
            if (inInstance.ColorAdjust)
            {
                inInstance.ColorAdjust.SetColor(Color.white);
                inInstance.StateAnimation.Replace(inInstance, Tween.Color(Color.white, Color.red, inInstance.ColorAdjust.SetColor, 0.5f).Wave(Wave.Function.Sin, 1).Loop());
            }
        }

        static private void OnEndStressedState(ActorInstance inInstance, ActorWorld inWorld)
        {
            if (inInstance.ColorAdjust)
                inInstance.ColorAdjust.SetColor(Color.white);
        }

        static private void OnBeginDeadState(ActorInstance inInstance, ActorWorld inWorld)
        {
            if (inInstance.ColorAdjust)
                inInstance.ColorAdjust.SetColor(Color.gray);
        }

        static private void OnEndDeadState(ActorInstance inInstance, ActorWorld inWorld)
        {
            if (inInstance.ColorAdjust)
                inInstance.ColorAdjust.SetColor(Color.white);
        }

        #endregion // States

        #endregion // Animations

        #if UNITY_EDITOR

        private void Reset()
        {
            this.CacheComponent(ref CachedTransform);
            this.CacheComponent(ref IdleAnimation);
            this.CacheComponent(ref ColorAdjust);
            this.CacheComponent(ref CachedCollider);
        }

        #endif // UNITY_EDITOR
    }

    public enum ActorActionId : byte
    {
        Spawning, // playing spawning animation
        Waiting, // waiting for experiment to start
        Idle, // standing still
        IdleMove, // idle moving
        Hungry, // moving towards food
        Eating, // eating food
        Dying // dying
    }
}