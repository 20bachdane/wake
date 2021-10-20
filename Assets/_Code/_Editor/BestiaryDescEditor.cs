using UnityEngine;
using UnityEditor;
using BeauUtil.Editor;
using UnityEditorInternal;
using BeauUtil;
using System;
using System.Reflection;
using Leaf;
using System.IO;
using BeauUtil.Debugger;
using System.Collections.Generic;

namespace Aqua.Editor
{
    [CustomEditor(typeof(BestiaryDesc)), CanEditMultipleObjects]
    public class BestiaryDescEditor : UnityEditor.Editor {
        private SerializedProperty m_TypeProperty;
        private SerializedProperty m_FlagsProperty;
        private SerializedProperty m_SizeProperty;
        private SerializedProperty m_StationIdProperty;
        private SerializedProperty m_DiveSiteIdProperty;
        private SerializedProperty m_ScientificNameProperty;
        private SerializedProperty m_CommonNameIdProperty;
        private SerializedProperty m_PluralCommonNameIdProperty;
        private SerializedProperty m_FactsProperty;
        private SerializedProperty m_HistoricalRecordDurationProperty;
        private SerializedProperty m_WaterColorProperty;
        private SerializedProperty m_IconProperty;
        private SerializedProperty m_SketchProperty;
        private SerializedProperty m_ColorProperty;
        private SerializedProperty m_ListenAudioEventProperty;
        private SerializedProperty m_SortingOrderProperty;

        [SerializeField] private bool m_CategoryExpanded = true;
        [SerializeField] private bool m_TextExpanded = true;
        [SerializeField] private bool m_AssetsExpanded = true;
        [SerializeField] private bool m_FactsExpanded = true;
        [SerializeField] private bool m_ShortcutsExpanded = true;

        [SerializeField] private Vector2 m_FactListScroll;
        [SerializeField] private int m_SelectedFactIdx = -1;

        [SerializeField] private string m_RenameFactName;
        [NonSerialized] private string m_AutoNameFact;
        [NonSerialized] private BFBase m_CachedLastFact;

        private ReorderableList m_FactList;
        private UnityEditor.Editor m_CurrentFactEditor;

        static private GUIContent s_TempContent;

        private void OnEnable() {
            m_TypeProperty = serializedObject.FindProperty("m_Type");
            m_FlagsProperty = serializedObject.FindProperty("m_Flags");
            m_SizeProperty = serializedObject.FindProperty("m_Size");
            m_StationIdProperty = serializedObject.FindProperty("m_StationId");
            m_DiveSiteIdProperty = serializedObject.FindProperty("m_DiveSiteId");
            m_ScientificNameProperty = serializedObject.FindProperty("m_ScientificName");
            m_CommonNameIdProperty = serializedObject.FindProperty("m_CommonNameId");
            m_PluralCommonNameIdProperty = serializedObject.FindProperty("m_PluralCommonNameId");
            m_FactsProperty = serializedObject.FindProperty("m_Facts");
            m_HistoricalRecordDurationProperty = serializedObject.FindProperty("m_HistoricalRecordDuration");
            m_WaterColorProperty = serializedObject.FindProperty("m_WaterColor");
            m_IconProperty = serializedObject.FindProperty("m_Icon");
            m_SketchProperty = serializedObject.FindProperty("m_Sketch");
            m_ColorProperty = serializedObject.FindProperty("m_Color");
            m_ListenAudioEventProperty = serializedObject.FindProperty("m_ListenAudioEvent");
            m_SortingOrderProperty = serializedObject.FindProperty("m_SortingOrder");

            m_FactList = new ReorderableList(serializedObject, m_FactsProperty);
            m_FactList.headerHeight = 0;
            m_FactList.drawHeaderCallback = (r) => { };
            m_FactList.showDefaultBackground = false;
            m_FactList.drawElementCallback = RenderFactListElement;
            m_FactList.footerHeight = 0;
            m_FactList.drawFooterCallback = (r) => { };
        }

        private void OnDisable() {
            if (m_CurrentFactEditor != null) {
                DestroyImmediate(m_CurrentFactEditor);
                m_CurrentFactEditor = null;
            }
        }

        public override void OnInspectorGUI() {
            serializedObject.UpdateIfRequiredOrScript();

            EditorGUILayout.PropertyField(m_TypeProperty);
            EditorGUILayout.PropertyField(m_FlagsProperty);
            EditorGUILayout.PropertyField(m_StationIdProperty);

            BestiaryDescCategory category = m_TypeProperty.hasMultipleDifferentValues ? BestiaryDescCategory.ALL : (BestiaryDescCategory) m_TypeProperty.intValue;

            switch(category) {
                case BestiaryDescCategory.Critter: {
                    RenderCritterSettings();
                    break;
                }

                case BestiaryDescCategory.Environment: {
                    RenderEnvironmentSettings();
                    break;
                }

                default: {
                    EditorGUILayout.HelpBox("Cannot simultaneously edit multiple bestiary entries with different types", MessageType.Warning);
                    break;
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void RenderCritterSettings() {
            EditorGUILayout.PropertyField(m_SortingOrderProperty, TempContent("Sorting Order", "Visual sorting order within the station"));

            if (Section("Organism", ref m_CategoryExpanded)) {
                EditorGUILayout.PropertyField(m_SizeProperty);
            }

            if (Section("Text", ref m_TextExpanded)) {
                EditorGUILayout.PropertyField(m_ScientificNameProperty);
                EditorGUILayout.PropertyField(m_CommonNameIdProperty);
                EditorGUILayout.PropertyField(m_PluralCommonNameIdProperty);
            }

            if (Section("Assets", ref m_AssetsExpanded)) {
                EditorGUILayout.PropertyField(m_IconProperty);
                EditorGUILayout.PropertyField(m_SketchProperty);
                EditorGUILayout.PropertyField(m_ColorProperty);
                EditorGUILayout.PropertyField(m_ListenAudioEventProperty);
            }

            RenderFacts(BestiaryDescCategory.Critter);

            if (targets.Length == 1) {
                if (Section("Quick Entry", ref m_ShortcutsExpanded)) {
                    BestiaryDesc desc = (BestiaryDesc) target;

                    Header("Stress Thresholds");

                    WaterPropertyBlock<BFState> stateFacts = default;
                    foreach(var fact in GetFacts<BFState>(desc)) {
                        stateFacts[fact.Property] = fact;
                    }

                    using(new GUIScopes.LabelWidthScope(40)) {
                        using(new EditorGUILayout.HorizontalScope()) {
                            GUILayout.FlexibleSpace();
                            RenderStressStateWizard(stateFacts, WaterPropertyId.Oxygen);
                            RenderStressStateWizard(stateFacts, WaterPropertyId.Temperature);
                            GUILayout.FlexibleSpace();
                        }

                        using(new EditorGUILayout.HorizontalScope()) {
                            GUILayout.FlexibleSpace();
                            RenderStressStateWizard(stateFacts, WaterPropertyId.Light);
                            RenderStressStateWizard(stateFacts, WaterPropertyId.PH);
                            GUILayout.FlexibleSpace();
                        }

                        using(new EditorGUILayout.HorizontalScope()) {
                            GUILayout.FlexibleSpace();
                            RenderStressStateWizard(stateFacts, WaterPropertyId.CarbonDioxide);
                            GUILayout.FlexibleSpace();
                        }
                    }
                }
            }
        }

        private void RenderEnvironmentSettings() {
            EditorGUILayout.PropertyField(m_SortingOrderProperty, TempContent("Sorting Order", "Visual sorting order within the station"));

            if (Section("Environment", ref m_CategoryExpanded)) {
                m_SizeProperty.intValue = (int) BestiaryDescSize.Ecosystem;
                EditorGUILayout.PropertyField(m_DiveSiteIdProperty);
                EditorGUILayout.PropertyField(m_HistoricalRecordDurationProperty);
            }

            if (Section("Text", ref m_TextExpanded)) {
                EditorGUILayout.PropertyField(m_CommonNameIdProperty);
            }

            if (Section("Assets", ref m_AssetsExpanded)) {
                EditorGUILayout.PropertyField(m_IconProperty);
                EditorGUILayout.PropertyField(m_SketchProperty);
                EditorGUILayout.PropertyField(m_ColorProperty);
                EditorGUILayout.PropertyField(m_ListenAudioEventProperty);
                EditorGUILayout.PropertyField(m_WaterColorProperty);
            }

            RenderFacts(BestiaryDescCategory.Environment);

            if (targets.Length == 1) {
                if (Section("Quick Entry", ref m_ShortcutsExpanded)) {
                    BestiaryDesc desc = (BestiaryDesc) target;

                    Header("Water Properties");

                    WaterPropertyBlock<BFWaterProperty> propertyFacts = default;
                    foreach(var fact in GetFacts<BFWaterProperty>(desc)) {
                        propertyFacts[fact.Property] = fact;
                    }

                    using(new EditorGUILayout.HorizontalScope()) {
                        GUILayout.FlexibleSpace();
                        RenderWaterPropertyWizard(propertyFacts, WaterPropertyId.Oxygen);
                        RenderWaterPropertyWizard(propertyFacts, WaterPropertyId.Temperature);
                        RenderWaterPropertyWizard(propertyFacts, WaterPropertyId.Light);
                        GUILayout.FlexibleSpace();
                    }

                    using(new EditorGUILayout.HorizontalScope()) {
                        GUILayout.FlexibleSpace();
                        RenderWaterPropertyWizard(propertyFacts, WaterPropertyId.PH);
                        RenderWaterPropertyWizard(propertyFacts, WaterPropertyId.CarbonDioxide);
                        GUILayout.FlexibleSpace();
                    }

                    Header("Water Property History");

                    WaterPropertyBlock<BFWaterPropertyHistory> historyFacts = default;
                    foreach(var fact in GetFacts<BFWaterPropertyHistory>(desc)) {
                        historyFacts[fact.Property] = fact;
                    }

                    using(new EditorGUILayout.HorizontalScope()) {
                        GUILayout.FlexibleSpace();
                        RenderWaterPropertyHistoryWizard(historyFacts, WaterPropertyId.Oxygen);
                        RenderWaterPropertyHistoryWizard(historyFacts, WaterPropertyId.Temperature);
                        RenderWaterPropertyHistoryWizard(historyFacts, WaterPropertyId.Light);
                        GUILayout.FlexibleSpace();
                    }

                    using(new EditorGUILayout.HorizontalScope()) {
                        GUILayout.FlexibleSpace();
                        RenderWaterPropertyHistoryWizard(historyFacts, WaterPropertyId.PH);
                        RenderWaterPropertyHistoryWizard(historyFacts, WaterPropertyId.CarbonDioxide);
                        GUILayout.FlexibleSpace();
                    }
                }
            }
        }

        private void RenderFacts(BestiaryDescCategory inCategory) {
            if (Section("Facts", ref m_FactsExpanded)) {
                if (targets.Length > 1) {
                    EditorGUILayout.HelpBox("Facts cannot be edited while multiple bestiary entries are selected", MessageType.Warning);
                    if (GUILayout.Button("Refresh Lists")) {
                        foreach(BestiaryDesc desc in targets) {
                            desc.FindAllFacts();
                        }
                    }
                } else {
                    BestiaryDesc desc = (BestiaryDesc) targets[0];
                    EditorGUILayout.BeginHorizontal(GUILayout.MinHeight(320));
                    
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(250));
                    m_FactListScroll = EditorGUILayout.BeginScrollView(m_FactListScroll, GUILayout.ExpandHeight(true));
                    m_SelectedFactIdx = RenderFactList(desc, m_SelectedFactIdx);
                    EditorGUILayout.EndScrollView();
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("Create")) {
                        OpenCreateFactContextMenu(desc, inCategory);
                    }
                    using(new EditorGUI.DisabledScope(m_SelectedFactIdx < 0 || m_FactsProperty.arraySize == 0)) {
                        if (GUILayout.Button("Delete")) {
                            SerializedProperty toDeleteProp = m_FactsProperty.GetArrayElementAtIndex(m_SelectedFactIdx);
                            BFBase fact = (BFBase) toDeleteProp.objectReferenceValue;
                            if (fact == null) {
                                m_FactsProperty.DeleteArrayElementAtIndex(m_SelectedFactIdx);
                            } else {
                                if (EditorUtility.DisplayDialog(
                                    string.Format("Delete '{0}'?", fact.name),
                                    string.Format("Are you sure you want to delete '{0}'? This cannot be undone.", fact.name),
                                    "Yes!", "Nope")) {
                                    toDeleteProp.objectReferenceValue = null;
                                    AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(fact));
                                    m_FactsProperty.DeleteArrayElementAtIndex(m_SelectedFactIdx);
                                }
                            }
                            if (m_SelectedFactIdx >= m_FactsProperty.arraySize - 1) {
                                m_SelectedFactIdx = m_FactsProperty.arraySize - 1;
                            }
                            serializedObject.ApplyModifiedProperties();
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("Refresh List")) {
                        desc.FindAllFacts();
                        m_SelectedFactIdx = -1;
                    }
                    if (GUILayout.Button("Auto Name")) {
                        if (EditorUtility.DisplayDialog("Update all fact names?", "You will have to update all fact name references manually", "Sure!", "Yikes, no thanks")) {
                            foreach(var fact in desc.m_Facts) {
                                TryRenameFact(fact, BFType.AutoNameFact(fact), true);
                            }
                            m_SelectedFactIdx = -1;
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandHeight(true));
                    RenderFactEditor(desc, m_SelectedFactIdx);
                    EditorGUILayout.EndVertical();
                    
                    EditorGUILayout.EndHorizontal();
                }
            }
        }

        private int RenderFactList(BestiaryDesc inEntry, int inIndex) {
            m_FactList.index = inIndex;
            m_FactList.DoLayoutList();
            return m_FactList.index;
        }

        private void RenderFactListElement(Rect rect, int index, bool isActive, bool isFocused) {
            BFBase fact = (BFBase) m_FactsProperty.GetArrayElementAtIndex(index).objectReferenceValue;
            if (fact == null) {
                EditorGUI.LabelField(rect, "-- NULL -- ");
            } else {
                BestiaryDesc desc = (BestiaryDesc) target;
                string factName = fact.name;
                string descName = desc.name;
                if (factName.StartsWith(descName) && factName.Length > descName.Length + 1) {
                    factName = factName.Substring(descName.Length + 1);
                }
                Type type = fact.GetType();
                string typeName = type.Name.Substring(2);
                factName = string.Format("{0} ({1})", factName, typeName);
                EditorGUI.LabelField(rect, factName);
            }
        }

        private void RenderFactEditor(BestiaryDesc inEntry, int inIndex) {
            if (inIndex < 0 || inIndex >= inEntry.m_Facts.Length) {
                EditorGUILayout.LabelField("No fact selected", EditorStyles.boldLabel);
                m_CachedLastFact = null;
            } else {
                BFBase fact = inEntry.m_Facts[inIndex];
                if (fact == null) {
                    EditorGUILayout.HelpBox("Fact was deleted", MessageType.Error);
                    m_CachedLastFact = null;
                } else {
                    CreateCachedEditor(fact, null, ref m_CurrentFactEditor);

                    if (m_CachedLastFact != fact) {
                        m_CachedLastFact = fact;
                        string trueName = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(fact));
                        if (trueName != fact.name) {
                            Log.Error("Fact name was weird: {0} when it should have been {1}", fact.name, trueName);
                            fact.name = trueName;
                            EditorUtility.SetDirty(fact); 
                        }
                        m_RenameFactName = fact.name;
                        m_AutoNameFact = BFType.AutoNameFact(fact);
                    }

                    using(new GUIScopes.LabelWidthScope(150)) {
                        using(new EditorGUILayout.HorizontalScope()) {
                            m_RenameFactName = EditorGUILayout.TextField("Id", m_RenameFactName);
                            using(new EditorGUI.DisabledScope(m_RenameFactName == fact.name)) {
                                if (GUILayout.Button("Rename", GUILayout.Width(60))) {
                                    TryRenameFact(fact, m_RenameFactName, false);
                                }
                            }

                            if (Event.current.type == EventType.Layout) {
                                m_AutoNameFact = BFType.AutoNameFact(fact);
                            }
                            using(new EditorGUI.DisabledScope(m_AutoNameFact == null || m_AutoNameFact == fact.name)) {
                                if (GUILayout.Button("Auto", GUILayout.Width(40))) {
                                    TryRenameFact(fact, m_AutoNameFact, false);
                                    m_RenameFactName = fact.name;
                                }
                            }
                        }

                        EditorGUILayout.Space();

                        m_CurrentFactEditor.OnInspectorGUI();
                    }
                }
            }
        }

        private void RenderWaterPropertyWizard(in WaterPropertyBlock<BFWaterProperty> inProperties, WaterPropertyId inId) {
            using(new EditorGUILayout.VerticalScope(GUILayout.Width(130))) {
                BFWaterProperty fact = inProperties[inId];
                GUILayout.Label(ObjectNames.NicifyVariableName(inId.ToString()));
                if (fact == null) {
                    if (GUILayout.Button("Missing")) {
                        CreateAndAddFact(new FactCreateParams(BFTypeId.WaterProperty, inId));
                    }
                } else {
                    WaterPropertyDesc waterProp = Assets.Property(inId);
                    float newValue = EditorGUILayout.Slider(fact.Value, waterProp.MinValue(), waterProp.MaxValue());
                    if (newValue != fact.Value) {
                        Undo.RecordObject(fact, "Changing water property values");
                        fact.Value = newValue;
                        EditorUtility.SetDirty(fact);
                    }
                }
            }
        }

        private void RenderWaterPropertyHistoryWizard(in WaterPropertyBlock<BFWaterPropertyHistory> inProperties, WaterPropertyId inId) {
            using(new EditorGUILayout.VerticalScope(GUILayout.Width(130))) {
                BFWaterPropertyHistory fact = inProperties[inId];
                GUILayout.Label(ObjectNames.NicifyVariableName(inId.ToString()));
                if (fact == null) {
                    if (GUILayout.Button("Missing")) {
                        CreateAndAddFact(new FactCreateParams(BFTypeId.WaterPropertyHistory, inId));
                    }
                } else {
                    BFGraphType newValue = (BFGraphType) EnumGUILayout.EnumField(fact.Graph, GUILayout.Width(120));
                    if (newValue != fact.Graph) {
                        Undo.RecordObject(fact, "Changing water property history values");
                        fact.Graph = newValue;
                        EditorUtility.SetDirty(fact);
                    }
                }
            }
        }

        private void RenderStressStateWizard(in WaterPropertyBlock<BFState> inProperties, WaterPropertyId inId) {
            using(new EditorGUILayout.VerticalScope(GUILayout.Width(200))) {
                BFState fact = inProperties[inId];
                GUILayout.Label(ObjectNames.NicifyVariableName(inId.ToString()));
                if (fact == null) {
                    if (GUILayout.Button("Missing")) {
                        CreateAndAddFact(new FactCreateParams(BFTypeId.State, inId));
                    }
                } else {
                    WaterPropertyDesc waterProp = Assets.Property(inId);
                    using(new EditorGUILayout.HorizontalScope()) {
                        bool hasStress = fact.HasStressed;
                        bool newStress = EditorGUILayout.Toggle("Stress", hasStress, GUILayout.Width(60));
                        if (hasStress != newStress) {
                            Undo.RecordObject(fact, "Changing stress state");
                            fact.HasStressed = newStress;
                            EditorUtility.SetDirty(fact);
                        }

                        using(new EditorGUI.DisabledScope(!newStress)) {
                            EditorGUI.BeginChangeCheck();
                            float min = fact.m_MinSafe;
                            float max = fact.m_MaxSafe;
                            EditorGUILayout.MinMaxSlider(ref min, ref max, waterProp.MinValue(), waterProp.MaxValue());
                            min = EditorGUILayout.FloatField(min, GUILayout.Width(60));
                            GUILayout.Label("-");
                            max = EditorGUILayout.FloatField(max, GUILayout.Width(60));
                            if (EditorGUI.EndChangeCheck()) {
                                Undo.RecordObject(fact, "Changing stress state");
                                fact.m_MinSafe = min;
                                fact.m_MaxSafe = max;
                                EditorUtility.SetDirty(fact);
                            }
                        }
                    }

                    using(new EditorGUILayout.HorizontalScope()) {
                        bool hasDeath = fact.HasDeath;
                        bool newDeath = EditorGUILayout.Toggle("Death", hasDeath, GUILayout.Width(60));
                        if (newDeath != hasDeath) {
                            Undo.RecordObject(fact, "Changing stress state");
                            fact.HasDeath = newDeath;
                            EditorUtility.SetDirty(fact);
                        }

                        using(new EditorGUI.DisabledScope(!newDeath)) {
                            EditorGUI.BeginChangeCheck();
                            float min = fact.m_MinStressed;
                            float max = fact.m_MaxStressed;
                            EditorGUILayout.MinMaxSlider(ref min, ref max, waterProp.MinValue(), waterProp.MaxValue());
                            min = EditorGUILayout.FloatField(min, GUILayout.Width(60));
                            GUILayout.Label("-");
                            max = EditorGUILayout.FloatField(max, GUILayout.Width(60));
                            if (EditorGUI.EndChangeCheck()) {
                                Undo.RecordObject(fact, "Changing stress state");
                                fact.m_MinStressed = min;
                                fact.m_MaxStressed = max;
                                EditorUtility.SetDirty(fact);
                            }
                        }
                    }
                }
            }
        }

        #region Fact Creation

        private struct FactCreateParams {
            public readonly BFTypeId Type;
            public readonly bool Stressed;
            public readonly WaterPropertyId PropertyId;

            public FactCreateParams(BFTypeId inType, bool inbStressed = false) {
                Type = inType;
                Stressed = inbStressed;
                PropertyId = WaterPropertyId.MAX;
            }

            public FactCreateParams(BFTypeId inType, WaterPropertyId inWaterProperty, bool inbStressed = false) {
                Type = inType;
                Stressed = inbStressed;
                PropertyId = inWaterProperty;
            }
        }

        private void OpenCreateFactContextMenu(BestiaryDesc inDesc, BestiaryDescCategory inCategory) {
            GenericMenu menu = new GenericMenu();

            switch(inCategory) {
                case BestiaryDescCategory.Critter: {

                    // body

                    if (HasFact<BFBody>(inDesc)) {
                        menu.AddDisabledItem(new GUIContent("Body"), true);
                    } else {
                        menu.AddItem(new GUIContent("Body"), false, CreateAndAddFactCallback, new FactCreateParams(BFTypeId.Body));
                    }

                    menu.AddSeparator("");

                    // states

                    WaterPropertyMask stateMask = default;
                    foreach(var state in GetFacts<BFState>(inDesc)) {
                        stateMask[state.Property] = true;
                    }

                    AddStateFactCreator(menu, WaterPropertyId.Oxygen, stateMask);
                    AddStateFactCreator(menu, WaterPropertyId.Temperature, stateMask);
                    AddStateFactCreator(menu, WaterPropertyId.Light, stateMask);
                    AddStateFactCreator(menu, WaterPropertyId.PH, stateMask);
                    AddStateFactCreator(menu, WaterPropertyId.CarbonDioxide, stateMask);

                    menu.AddSeparator("");

                    // eat

                    menu.AddItem(new GUIContent("Eat (Default)"), false, CreateAndAddFactCallback, new FactCreateParams(BFTypeId.Eat));
                    menu.AddItem(new GUIContent("Eat (When Stressed)"), false, CreateAndAddFactCallback, new FactCreateParams(BFTypeId.Eat, true));

                    menu.AddSeparator("");

                    // grow and reproduce

                    AddSingleBehaviorCreator<BFGrow>(inDesc, menu, "Grow (Default)", false);
                    AddSingleBehaviorCreator<BFGrow>(inDesc, menu, "Grow (When Stressed)", true);

                    AddSingleBehaviorCreator<BFReproduce>(inDesc, menu, "Reproduce (Default)", false);
                    AddSingleBehaviorCreator<BFReproduce>(inDesc, menu, "Reproduce (When Stressed)", true);

                    menu.AddSeparator("");

                    // produce and consume

                    WaterPropertyMask produceMaskDefault = default;
                    WaterPropertyMask produceMaskStressed = default;
                    foreach(var produce in GetFacts<BFProduce>(inDesc)) {
                        if (produce.OnlyWhenStressed) {
                            produceMaskStressed[produce.Property] = true;
                        } else {
                            produceMaskDefault[produce.Property] = true;
                        }
                    }

                    AddProduceConsumeCreator<BFProduce>(menu, "Produce/Oxygen (Default)", WaterPropertyId.Oxygen, produceMaskDefault, false);
                    AddProduceConsumeCreator<BFProduce>(menu, "Produce/Oxygen (When Stressed)", WaterPropertyId.Oxygen, produceMaskStressed, true);

                    AddProduceConsumeCreator<BFProduce>(menu, "Produce/Carbon Dioxide (Default)", WaterPropertyId.CarbonDioxide, produceMaskDefault, false);
                    AddProduceConsumeCreator<BFProduce>(menu, "Produce/Carbon Dioxide (When Stressed)", WaterPropertyId.CarbonDioxide, produceMaskStressed, true);

                    WaterPropertyMask consumeMaskDefault = default;
                    WaterPropertyMask consumeMaskStressed = default;
                    foreach(var consume in GetFacts<BFConsume>(inDesc)) {
                        if (consume.OnlyWhenStressed) {
                            consumeMaskStressed[consume.Property] = true;
                        } else {
                            consumeMaskDefault[consume.Property] = true;
                        }
                    }

                    AddProduceConsumeCreator<BFConsume>(menu, "Consume/Oxygen (Default)", WaterPropertyId.Oxygen, consumeMaskDefault, false);
                    AddProduceConsumeCreator<BFConsume>(menu, "Consume/Oxygen (When Stressed)", WaterPropertyId.Oxygen, consumeMaskStressed, true);

                    AddProduceConsumeCreator<BFConsume>(menu, "Consume/Carbon Dioxide (Default)", WaterPropertyId.CarbonDioxide, consumeMaskDefault, false);
                    AddProduceConsumeCreator<BFConsume>(menu, "Consume/Carbon Dioxide (When Stressed)", WaterPropertyId.CarbonDioxide, consumeMaskStressed, true);

                    AddProduceConsumeCreator<BFConsume>(menu, "Consume/Light (Default)", WaterPropertyId.Light, consumeMaskDefault, false);
                    AddProduceConsumeCreator<BFConsume>(menu, "Consume/Light (When Stressed)", WaterPropertyId.Light, consumeMaskStressed, true);

                    break;
                }

                case BestiaryDescCategory.Environment: {

                    // water properties

                    WaterPropertyMask currentValMask = default;
                    foreach(var state in GetFacts<BFWaterProperty>(inDesc)) {
                        currentValMask[state.Property] = true;
                    }

                    AddWaterFactCreator(menu, BFTypeId.WaterProperty, "{0}", WaterPropertyId.Oxygen, currentValMask);
                    AddWaterFactCreator(menu, BFTypeId.WaterProperty, "{0}", WaterPropertyId.Temperature, currentValMask);
                    AddWaterFactCreator(menu, BFTypeId.WaterProperty, "{0}", WaterPropertyId.Light, currentValMask);
                    AddWaterFactCreator(menu, BFTypeId.WaterProperty, "{0}", WaterPropertyId.PH, currentValMask);
                    AddWaterFactCreator(menu, BFTypeId.WaterProperty, "{0}", WaterPropertyId.CarbonDioxide, currentValMask);

                    menu.AddSeparator("");

                    // water history

                    WaterPropertyMask historyValMask = default;
                    foreach(var state in GetFacts<BFWaterPropertyHistory>(inDesc)) {
                        historyValMask[state.Property] = true;
                    }

                    AddWaterFactCreator(menu, BFTypeId.WaterPropertyHistory, "{0} History", WaterPropertyId.Oxygen, historyValMask);
                    AddWaterFactCreator(menu, BFTypeId.WaterPropertyHistory, "{0} History", WaterPropertyId.Temperature, historyValMask);
                    AddWaterFactCreator(menu, BFTypeId.WaterPropertyHistory, "{0} History", WaterPropertyId.Light, historyValMask);
                    AddWaterFactCreator(menu, BFTypeId.WaterPropertyHistory, "{0} History", WaterPropertyId.PH, historyValMask);
                    AddWaterFactCreator(menu, BFTypeId.WaterPropertyHistory, "{0} History", WaterPropertyId.CarbonDioxide, historyValMask);

                    menu.AddSeparator("");

                    // population

                    menu.AddItem(new GUIContent("Population"), false, CreateAndAddFactCallback, new FactCreateParams(BFTypeId.Population));
                    menu.AddItem(new GUIContent("Population History"), false, CreateAndAddFactCallback, new FactCreateParams(BFTypeId.PopulationHistory));

                    menu.AddSeparator("");

                    // model

                    menu.AddItem(new GUIContent("Model"), false, CreateAndAddFactCallback, new FactCreateParams(BFTypeId.Model));

                    break;
                }
            }

            menu.ShowAsContext();
        }

        private void CreateAndAddFactCallback(object inParams) {
            FactCreateParams factParams = (FactCreateParams) inParams;
            CreateAndAddFact(factParams);
        }

        private void CreateAndAddFact(FactCreateParams inParams) {
            BestiaryDesc desc = (BestiaryDesc) target;

            BFBase createdFact = CreateFact(desc, BFType.ResolveSystemType(inParams.Type));

            BFBehavior createdBehavior = createdFact as BFBehavior;
            if (createdBehavior != null) {
                createdBehavior.OnlyWhenStressed = inParams.Stressed;
            }

            switch(inParams.Type) {
                case BFTypeId.State: {
                    BFState fact = (BFState) createdFact;
                    fact.Property = inParams.PropertyId;
                    break;
                }

                case BFTypeId.WaterProperty: {
                    BFWaterProperty fact = (BFWaterProperty) createdFact;
                    fact.Property = inParams.PropertyId;
                    fact.Value = Assets.Property(inParams.PropertyId).DefaultValue();
                    break;
                }

                case BFTypeId.WaterPropertyHistory: {
                    BFWaterPropertyHistory fact = (BFWaterPropertyHistory) createdFact;
                    fact.Property = inParams.PropertyId;
                    break;
                }

                case BFTypeId.Produce: {
                    BFProduce fact = (BFProduce) createdFact;
                    fact.Property = inParams.PropertyId;
                    break;
                }

                case BFTypeId.Consume: {
                    BFConsume fact = (BFConsume) createdFact;
                    fact.Property = inParams.PropertyId;
                    break;
                }
            }

            FinalizeFact(desc, createdFact);
            m_SelectedFactIdx = desc.m_Facts.Length - 1;
        }

        private void AddStateFactCreator(GenericMenu inMenu, WaterPropertyId inWaterPropertyId, WaterPropertyMask inMask) {
            GUIContent name = new GUIContent(string.Format("State/{0}", ObjectNames.NicifyVariableName(inWaterPropertyId.ToString())));
            if (inMask[inWaterPropertyId]) {
                inMenu.AddDisabledItem(name, true);
            } else {
                inMenu.AddItem(name, false, CreateAndAddFactCallback, new FactCreateParams(BFTypeId.State, inWaterPropertyId));
            }
        }

        private void AddWaterFactCreator(GenericMenu inMenu, BFTypeId inType, string inName,  WaterPropertyId inWaterPropertyId, WaterPropertyMask inMask) {
            GUIContent name = new GUIContent(string.Format(inName, ObjectNames.NicifyVariableName(inWaterPropertyId.ToString())));
            if (inMask[inWaterPropertyId]) {
                inMenu.AddDisabledItem(name, true);
            } else {
                inMenu.AddItem(name, false, CreateAndAddFactCallback, new FactCreateParams(inType, inWaterPropertyId));
            }
        }

        private void AddSingleBehaviorCreator<T>(BestiaryDesc inDesc, GenericMenu inMenu, string inName, bool inbStressed) where T : BFBehavior {
            if (HasBehavior<T>(inDesc, inbStressed)) {
                inMenu.AddDisabledItem(new GUIContent(inName), true);
            } else {
                inMenu.AddItem(new GUIContent(inName), false, CreateAndAddFactCallback, new FactCreateParams(BFType.ResolveFactType(typeof(T)), inbStressed));
            }
        }

        private void AddProduceConsumeCreator<T>(GenericMenu inMenu, string inName, WaterPropertyId inWaterPropertyId, WaterPropertyMask inMask, bool inbStressed) where T : BFBehavior {
            if (inMask[inWaterPropertyId]) {
                inMenu.AddDisabledItem(new GUIContent(inName), true);
            } else {
                inMenu.AddItem(new GUIContent(inName), false, CreateAndAddFactCallback, new FactCreateParams(BFType.ResolveFactType(typeof(T)), inWaterPropertyId, inbStressed));
            }
        }

        static private BFBase CreateFact(BestiaryDesc inParent, Type inFactType) {
            BFBase fact = (BFBase) ScriptableObject.CreateInstance(inFactType);
            fact.name = inParent.name + "." + inFactType.Name.Substring(2);
            fact.Parent = inParent;

            return fact;
        }

        static private void FinalizeFact(BestiaryDesc inParent, BFBase inFact) {
            string newName = BFType.AutoNameFact(inFact);
            if (newName != null) {
                inFact.name = newName;
            }

            string directory = Path.GetDirectoryName(AssetDatabase.GetAssetPath(inParent));
            string assetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(directory, inFact.name + ".asset"));
            AssetDatabase.CreateAsset(inFact, assetPath);
            AssetDatabase.ImportAsset(assetPath);

            ArrayUtility.Add(ref inParent.m_Facts, inFact);
            EditorUtility.SetDirty(inParent);
            EditorUtility.SetDirty(inFact);
        }

        static private bool TryRenameFact(BFBase inFact, string inNewName, bool inbBatch) {
            if (string.IsNullOrEmpty(inNewName) || inNewName == inFact.name) {
                return false;
            }

            string newPath = AssetDatabase.GetAssetPath(inFact);
            newPath = Path.Combine(Path.GetDirectoryName(newPath), inNewName + ".asset");
            if (File.Exists(newPath)) {
                if (!inbBatch) {
                    EditorUtility.DisplayDialog(
                        "File Conflict",
                        string.Format("Cannot rename '{0}' to '{1}'.\nThis would conflict with an existing file.", inFact.name, inNewName),
                        "Whoops", null);
                    }
                return false;
            } else {
                if (inbBatch || EditorUtility.DisplayDialog(
                        "Rename Fact?",
                        string.Format("Rename '{0}' to '{1}'?\nYou will need to update any references to this fact manually", inFact.name, inNewName),
                        "I understand", "Never mind")) {
                    string error = AssetDatabase.RenameAsset(AssetDatabase.GetAssetPath(inFact), inNewName + ".asset");
                    if (!string.IsNullOrEmpty(error)) {
                        Log.Error("[BestiaryDesc] Error when renaming file: {0}", error);
                        return false;
                    } else {
                        AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(inFact));
                        return true;
                    }
                } else {
                    return false;
                }
            }
        }

        #endregion // Fact Creation

        #region Fact Access

        static private bool HasFact<T>(BestiaryDesc inParent) where T : BFBase {
            foreach(var fact in inParent.m_Facts) {
                if (fact is T) {
                    return true;
                }
            }

            return false;
        }

        static private bool HasBehavior<T>(BestiaryDesc inParent, bool inbStressed) where T : BFBehavior {
            foreach(var fact in inParent.m_Facts) {
                T casted;
                if ((casted = fact as T) && casted.OnlyWhenStressed == inbStressed) {
                    return true;
                }
            }

            return false;
        }

        static private T GetFact<T>(BestiaryDesc inParent) where T : BFBase {
            T casted;
            foreach(var fact in inParent.m_Facts) {
                if (casted = fact as T) {
                    return casted;
                }
            }

            return null;
        }

        static private IEnumerable<T> GetFacts<T>(BestiaryDesc inParent) where T : BFBase {
            T casted;
            foreach(var fact in inParent.m_Facts) {
                if (casted = fact as T) {
                    yield return casted;
                }
            }
        }

        static private T GetFact<T>(BestiaryDesc inParent, Predicate<T> inPredicate) where T : BFBase {
            T casted;
            foreach(var fact in inParent.m_Facts) {
                if ((casted = fact as T) && inPredicate(casted)) {
                    return casted;
                }
            }

            return null;
        }

        #endregion // Fact Access

        static private void Header(string inHeader) {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(inHeader, EditorStyles.boldLabel);
        }

        static private bool Section(string inHeader, ref bool ioState) {
            EditorGUILayout.Space();
            ioState = EditorGUILayout.Foldout(ioState, inHeader, EditorStyles.foldoutHeader);
            if (ioState) {
                EditorGUILayout.Space();
            }
            return ioState;
        }

        static private GUIContent TempContent(string inText, string inTooltip = null) {
            GUIContent c = s_TempContent ?? (s_TempContent = new GUIContent());
            c.text = inText;
            c.tooltip = inTooltip;
            return c;
        }
    }
}