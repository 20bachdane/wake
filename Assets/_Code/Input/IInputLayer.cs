using System;
using System.Collections.Generic;
using BeauData;
using BeauPools;
using BeauUtil;
using Aqua;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Aqua
{
    public interface IInputLayer
    {
        int Priority { get; }
        InputLayerFlags Flags { get; }
        bool? Override { get; set; }

        bool IsInputEnabled { get; }
        DeviceInput Device { get; }

        void UpdateSystem(int inSystemPriority, InputLayerFlags inFlags, bool inbForceUpdate);

        UnityEvent OnInputEnabled { get; }
        UnityEvent OnInputDisabled { get; }
    }

    [Flags]
    public enum InputLayerFlags : UInt32
    {
        [Label("World/Player Controls")] PlayerControls = 0x01,

        [Hidden] AllWorld = PlayerControls,
        
        [Label("UI/World UI")] WorldUI = 0x100,
        [Label("UI/Game UI")] GameUI = 0x200,
        [Label("UI/Tutorial UI")] TutorialUI = 0x400,
        [Label("UI/Portable")] Portable = 0x80,

        [Hidden] AllUI = WorldUI | GameUI | TutorialUI | Portable,

        [Label("System/System")] System = 0x100000,
        [Label("System/Error")] SystemError = 0x200000,
        [Label("System/Transition")] Transition = 0x400000,

        [Hidden] AllSystem = System | SystemError | Transition,

        [Hidden] All = AllWorld | AllUI | AllSystem,

        [Hidden] Default = AllWorld | AllUI | System | SystemError
    }
}