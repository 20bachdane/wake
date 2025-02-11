using System;
using System.Collections;
using System.Collections.Generic;
using Aqua;
using Aqua.Cameras;
using Aqua.Character;
using AquaAudio;
using BeauRoutine;
using BeauUtil;
using BeauUtil.UI;
using ScriptableBake;
using UnityEngine;

namespace ProtoAqua.ExperimentV2
{
    public class SelectableTank : MonoBehaviour, IBaked
    {
        #region Consts

        static public readonly StringHash32 Emoji_Stressed = "Stress";
        static public readonly StringHash32 Emoji_Death = "Dead";
        static public readonly StringHash32 Emoji_Eat = "Eat";
        static public readonly StringHash32 Emoji_Reproduce = "Repro";
        static public readonly StringHash32 Emoji_Parasite = "Parasite";
        static public readonly StringHash32 Emoji_Breath = "Breath";

        #endregion // Consts

        #region Inspector

        public TankType Type = TankType.Observation;
        [Required] public CameraPose CameraPose = null;
        [Required] public CameraPose ZoomPose = null;
        [Required] public BoxCollider BoundsCollider;
        [HideInInspector] public Bounds Bounds;
        [Required] public MonoBehaviour Controller;

        [Header("Navigation Arrows")]
        [Required(ComponentLookupDirection.Children)] public NavArrow[] NavArrows = null;
        [Required(ComponentLookupDirection.Children)] public GameObject NavArrowParent = null;
        [HideInInspector] public bool[] NavArrowStates;

        [Header("Guide Target")]
        [Required] public Transform GuideTarget = null;
        public Transform GuideTargetZoomed = null;

        [Header("Canvas")]
        [Required] public Canvas Interface = null;
        [Required] public InputRaycasterLayer InterfaceRaycaster = null;
        [Required] public CanvasGroup InterfaceFader = null;

        [Header("Water")]
        [Required] public BoxCollider2D WaterTrigger;
        [Required] public BoxCollider WaterCollider3D;
        [Required] public Transform WaterTransform3D;
        [HideInInspector] public Rect WaterRect;

        [Header("Caustics")]
        public MeshRenderer InteriorCaustics;
        public MeshRenderer FloorCaustics;

        [Header("Lights")]
        [Required] public Light[] Lights;
        
        [Header("Emojis")]
        [SerializeField, HideInInspector] public ParticleSystem[] EmojiEmitters = Array.Empty<ParticleSystem>();
        [SerializeField, HideInInspector] public StringHash32[] EmojiIds = Array.Empty<StringHash32>();

        [Header("Actors")]
        [SerializeField] public ActorBehaviorSystem ActorBehavior = null;

        [HideInInspector] public ExperimentScreen[] AllScreens;

        #endregion // Inspector

        [NonSerialized] private StringHash32 m_Id;
        [NonSerialized] public TankState CurrentState;

        [NonSerialized] public GuideBody Guide;
        
        [NonSerialized] public TankWaterSystem WaterSystem;
        [NonSerialized] public AudioHandle WaterAudioLoop;

        [NonSerialized] public ExperimentScreen CurrentScreen;
        [NonSerialized] public Routine ScreenTransition;
        [NonSerialized] public Routine WaterTransition;

        public StringHash32 Id { get { return m_Id.IsEmpty ? (m_Id = name) : m_Id; } }

        public Action ActivateMethod;
        public Func<bool> CanDeactivate;
        public Action DeactivateMethod;
        
        public Predicate<StringHash32> HasCritter;
        public Predicate<StringHash32> HasEnvironment;

        public Predicate<StringHash32> CanEmitEmoji;
        public Action<StringHash32> OnEmitEmoji;

        static public void Reset(SelectableTank tank, bool full = false) {
            foreach(var screen in tank.AllScreens) {
                ExperimentScreen.Reset(screen);
            }
            tank.ScreenTransition.Stop();
            if (full) {
                tank.ActorBehavior.ClearAll();
                tank.ActorBehavior.World.Allocator.Cleanup(60);
            } else {
                tank.ActorBehavior.ClearActors();
            }
            foreach(var emoji in tank.EmojiEmitters) {
                emoji.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            tank.CurrentScreen = null;
        }

        static public void SetLights(SelectableTank tank, bool active) {
            for(int i = 0; i < tank.Lights.Length; i++) {
                tank.Lights[i].enabled = active;
            }
        }

        #region Helper Methods

        public static void InitNavArrows(SelectableTank inTank) {
            inTank.NavArrowStates = new bool[inTank.NavArrows.Length];
        }

        public static void SetNavArrowsActive(SelectableTank inTank, bool isActive) {
            if (isActive) {
                // restore to prev states
                for (int i = 0; i < inTank.NavArrows.Length; i++) {
                    inTank.NavArrows[i].gameObject.SetActive(inTank.NavArrowStates[i]);
                }
            }
            else {
                // save prev states and deactivate arrows
                for (int i = 0; i < inTank.NavArrows.Length; i++) {
                    inTank.NavArrowStates[i] = inTank.NavArrows[i].gameObject.activeSelf;
                    inTank.NavArrows[i].gameObject.SetActive(false);
                }
            }
        }

        #endregion // Helper Methods

        #region Sequences

        static public IEnumerator FillTankSequence(SelectableTank tank) {
            yield return tank.WaterSystem.RequestFill(tank, 3f);
            yield return 0.2f;
        }

        static public IEnumerator DespawnSequence(SelectableTank tank) {
            tank.ActorBehavior.ClearActors();
            yield break;
        }

        static public IEnumerator SpawnSequence(SelectableTank tank, BestiaryAddPanel organismPanel) {
            foreach(var species in organismPanel.Selected) {
                tank.ActorBehavior.Alloc(species.Id());
                yield return 0.15f;
            }
            while (!tank.ActorBehavior.AllSpawned()) {
                yield return null;
            }
        }

        #endregion // Sequences

        #if UNITY_EDITOR

        int IBaked.Order { get { return 0; } }

        bool IBaked.Bake(BakeFlags flags, BakeContext context)
        {
            ActorBehavior = GetComponentInChildren<ActorBehaviorSystem>(false);
            AllScreens = GetComponentsInChildren<ExperimentScreen>(true);

            List<ParticleSystem> emitters = new List<ParticleSystem>();
            foreach(var particle in GetComponentsInChildren<ParticleSystem>()) {
                if (particle.main.loop) {
                    continue;
                }

                emitters.Add(particle);
            }

            EmojiEmitters = emitters.ToArray();
            EmojiIds = new StringHash32[EmojiEmitters.Length];
            for(int i = 0; i < EmojiIds.Length; i++) {
                StringHash32 id = EmojiEmitters[i].name.Replace("Emoji", "").Replace("Emitter", "").Replace("Particles", "");
                EmojiIds[i] = id;
            }

            return true;
        }

        #endif // UNITY_EDITOR
    }

    [Flags]
    public enum TankState : byte
    {
        Idle = 0x00,
        Selected = 0x01,
        Filling = 0x02,
        Draining = 0x04,
        Running = 0x08,
        Completed = 0x10
    }

    public enum TankType : byte
    {
        Observation = RunningExperimentData.Type.Observation,
        Stress = RunningExperimentData.Type.Stress,
        Measurement = RunningExperimentData.Type.Measurement,

        Unknown = 255
    }
}