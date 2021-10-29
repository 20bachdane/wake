using BeauUtil;
using BeauUtil.Variants;
using Aqua.Profile;
using Aqua.Scripting;
using Aqua.Debugging;
using BeauUtil.Debugger;
using Leaf.Runtime;
using System.Collections;

namespace Aqua
{
    public partial class DataService : ServiceBehaviour
    {
        public IVariantResolver VariableResolver { get { return m_VariableResolver; } }

        #region Variables

        /// <summary>
        /// Retrieves a variable with the given identifier and an optional context.
        /// </summary>
        public Variant GetVariable(StringSlice inId, object inContext = null)
        {
            TableKeyPair keyPair;
            Variant result = default(Variant);
            if (TableKeyPair.TryParse(inId, out keyPair))
            {
                VariableResolver.TryResolve(inContext, keyPair, out result);
            }
            return result;
        }

        /// <summary>
        /// Retrieves a variable with the given identifier and an optional context.
        /// </summary>
        public Variant GetVariable(TableKeyPair inId, object inContext = null)
        {
            Variant result = default(Variant);
            VariableResolver.TryResolve(inContext, inId, out result);
            return result;
        }

        /// <summary>
        /// Pops a variable with the given identifier.
        /// </summary>
        public Variant PopVariable(TableKeyPair inId, object inContext = null)
        {
            Variant result = GetVariable(inId, inContext);
            SetVariable(inId, null, inContext);
            return result;
        }

        /// <summary>
        /// Sets a variable with the given identifier and an optional context.
        /// </summary>
        public void SetVariable(StringSlice inId, Variant inValue, object inContext = null)
        {
            TableKeyPair keyPair;
            if (!TableKeyPair.TryParse(inId, out keyPair)
                || !VariableResolver.TryModify(inContext, keyPair, VariantModifyOperator.Set, inValue))
            {
                Log.Error("[DataService] Unable to set variable '{0}' to {1}", inId, inValue.ToDebugString());
            }
            else
            {
                Services.Events.QueueForDispatch(GameEvents.VariableSet, keyPair);
            }
        }

        /// <summary>
        /// Sets a variable with the given identifier and an optional context.
        /// </summary>
        public void SetVariable(TableKeyPair inId, Variant inValue, object inContext = null)
        {
            if (!VariableResolver.TryModify(inContext, inId, VariantModifyOperator.Set, inValue))
            {
                Log.Error("[DataService] Unable to set variable '{0}' to {1}", inId, inValue);
            }
            else
            {
                Services.Events.QueueForDispatch(GameEvents.VariableSet, inId);
            }
        }

        /// <summary>
        /// Adds to a variable with the given identifier and an optional context.
        /// </summary>
        public void AddVariable(StringSlice inId, Variant inValue, object inContext = null)
        {
            TableKeyPair keyPair;
            if (!TableKeyPair.TryParse(inId, out keyPair)
                || !VariableResolver.TryModify(inContext, keyPair, VariantModifyOperator.Add, inValue))
            {
                Log.Error("[DataService] Unable to add variable '{0}' to {1}", inId, inValue);
            }
            else
            {
                Services.Events.QueueForDispatch(GameEvents.VariableSet, keyPair);
            }
        }

        /// <summary>
        /// Adds to a variable with the given identifier and an optional context.
        /// </summary>
        public void AddVariable(TableKeyPair inId, Variant inValue, object inContext = null)
        {
            if (!VariableResolver.TryModify(inContext, inId, VariantModifyOperator.Add, inValue))
            {
                Log.Error("[DataService] Unable to add variable '{0}' to {1}", inId, inValue);
            }
            else
            {
                Services.Events.QueueForDispatch(GameEvents.VariableSet, inId);
            }
        }

        /// <summary>
        /// Retrieves a variable with the given id.
        /// If the value equals the old value, the variable is set to the new value.
        /// </summary>
        public bool CompareExchange(StringSlice inId, Variant inOldValue, Variant inNewValue, object inContext = null)
        {
            TableKeyPair keyPair;
            if (!TableKeyPair.TryParse(inId, out keyPair))
            {
                return false;
            }

            Variant result = default(Variant);
            VariableResolver.TryResolve(inContext, keyPair, out result);
            if (result == inOldValue)
            {
                SetVariable(keyPair, inNewValue, inContext);
                return true;
            }
            
            return false;
        }

        /// <summary>
        /// Retrieves a variable with the given id.
        /// If the value equals the old value, the variable is set to the new value.
        /// </summary>
        public bool CompareExchange(TableKeyPair inId, Variant inOldValue, Variant inNewValue, object inContext = null)
        {
            Variant result = default(Variant);
            VariableResolver.TryResolve(inContext, inId, out result);
            if (result == inOldValue)
            {
                SetVariable(inId, inNewValue, inContext);
                return true;
            }
            
            return false;
        }

        #endregion // Variables

        #region Tables

        /// <summary>
        /// Binds a table.
        /// </summary>
        public void BindTable(StringHash32 inId, VariantTable inTable)
        {
            m_VariableResolver.SetTable(inId, inTable);
            DebugService.Log(LogMask.Loading | LogMask.DataService, "[DataService] Bound table '{0}'", inId);
        }

        /// <summary>
        /// Unbinds a table.
        /// </summary>
        public void UnbindTable(StringHash32 inId)
        {
            m_VariableResolver.ClearTable(inId);
            DebugService.Log(LogMask.Loading | LogMask.DataService, "[DataService] Unbound table '{0}'", inId);
        }

        #endregion // Tables

        #region Conditions

        /// <summary>
        /// Checks if the given conditions are true.
        /// If empty, will also return true.
        /// </summary>
        public bool CheckConditions(StringSlice inConditions, object inContext = null)
        {
            return VariableResolver.TryEvaluate(inContext, inConditions, Services.Script.LeafInvoker);
        }

        #endregion // Conditions

        private void InitVariableResolver()
        {
            m_VariableResolver = new CustomVariantResolver();

            m_VariableResolver.SetVar(GameVars.SceneName, GetSceneName);
            m_VariableResolver.SetVar(GameVars.MapId, () => MapDB.LookupCurrentMap());
            m_VariableResolver.SetVar(GameVars.LastEntrance, () => Services.State.LastEntranceId);
            
            m_VariableResolver.SetVar(GameVars.DayName, GetDayOfWeek);
            m_VariableResolver.SetVar(GameVars.DayNumber, () => Services.Time.Current.Day);
            m_VariableResolver.SetVar(GameVars.Hour, () => Services.Time.Current.HourF);
            m_VariableResolver.SetVar(GameVars.DayPhase, GetDayPhase);
            m_VariableResolver.SetVar(GameVars.IsDay, () => Services.Time.Current.IsDay);
            m_VariableResolver.SetVar(GameVars.IsNight, () => Services.Time.Current.IsNight);

            m_VariableResolver.SetVar(GameVars.PlayerGender, GetPlayerPronouns);
            m_VariableResolver.SetVar(GameVars.CurrentJob, GetJobId);
            m_VariableResolver.SetVar(GameVars.CurrentStation, GetStationId);
            m_VariableResolver.SetVar(GameVars.ActNumber, GetActNumber);
        }

        private void HookSaveDataToVariableResolver(SaveData inData)
        {
            m_VariableResolver.SetTable("global", inData.Script.GlobalTable);
            m_VariableResolver.SetTable("jobs", inData.Script.JobsTable);
            m_VariableResolver.SetTable("world", inData.Script.PartnerTable);
            m_VariableResolver.SetTable("player", inData.Script.PlayerTable);
            m_VariableResolver.SetTable("kevin", inData.Script.PartnerTable);
        }

        #region Callbacks

        static private Variant GetDayOfWeek()
        {
            switch(Services.Time.Current.DayName)
            {
                case DayName.Sunday:
                    return GameConsts.DayName_Sunday;
                case DayName.Monday:
                    return GameConsts.DayName_Monday;
                case DayName.Tuesday:
                    return GameConsts.DayName_Tuesday;
                case DayName.Wednesday:
                    return GameConsts.DayName_Wednesday;
                case DayName.Thursday:
                    return GameConsts.DayName_Thursday;
                case DayName.Friday:
                    return GameConsts.DayName_Friday;
                case DayName.Saturday:
                    return GameConsts.DayName_Saturday;
                default:
                    return Variant.Null;
            }
        }

        static private Variant GetDayPhase()
        {
            switch(Services.Time.Current.Phase)
            {
                case DayPhase.Morning:
                    return GameConsts.DayPhase_Morning;
                case DayPhase.Day:
                    return GameConsts.DayPhase_Day;
                case DayPhase.Evening:
                    return GameConsts.DayPhase_Evening;
                case DayPhase.Night:
                    return GameConsts.DayPhase_Night;
                default:
                    return Variant.Null;
            }
        }

        private Variant GetPlayerPronouns()
        {
            switch(CurrentCharacterPronouns())
            {
                case Pronouns.Masculine:
                    return "m";
                case Pronouns.Feminine:
                    return "f";
                default:
                    return "x";
            }
        }

        static private Variant GetSceneName()
        {
            return SceneHelper.ActiveScene().Name;
        }

        private Variant GetJobId()
        {
            return Profile.Jobs.CurrentJobId;
        }

        private Variant GetStationId()
        {
            return Profile.Map.CurrentStationId();
        }

        private Variant GetActNumber()
        {
            return Profile.Script.ActIndex;
        }

        #endregion // Callbacks

        #region Leaf

        static private class LeafIntegration
        {
            static private readonly RingBuffer<BFBase> s_BatchedFacts = new RingBuffer<BFBase>(8, RingBufferMode.Expand);
            static private readonly RingBuffer<BFDiscoveredFlags> s_BatchedFactFlags = new RingBuffer<BFDiscoveredFlags>(8, RingBufferMode.Expand);

            static public void ClearBatches()
            {
                s_BatchedFacts.Clear();
                s_BatchedFacts.Clear();
            }

            private enum PopupMode
            {
                Silent,
                Popup,
                Batch
            }

            #region Bestiary/Inventory

            [LeafMember("HasEntity"), UnityEngine.Scripting.Preserve]
            static private Variant HasEntity(StringHash32 inEntityId)
            {
                return Services.Data.Profile.Bestiary.HasEntity(inEntityId);
            }

            [LeafMember("GiveEntity"), UnityEngine.Scripting.Preserve]
            static private IEnumerator GiveEntity([BindContext] ScriptThread inThread, StringHash32 inEntityId, PopupMode inMode = PopupMode.Popup)
            {
                if (Services.Data.Profile.Bestiary.RegisterEntity(inEntityId) && inMode == PopupMode.Popup)
                {
                    inThread.Dialog = null;

                    if (Services.UI.IsSkippingCutscene())
                        return null;

                    BestiaryDesc bestiary = Assets.Bestiary(inEntityId);
                    Services.Audio.PostEvent("item.popup.new");
                    if (bestiary.Category() == BestiaryDescCategory.Critter)
                    {
                        return Services.UI.Popup.Display(
                            Loc.Format("ui.popup.newBestiary.critter.header",
                                bestiary.CommonName()), null,
                                new StreamedImageSet(bestiary.SketchPath(), bestiary.Icon())).Wait();
                    }
                    else
                    {
                        return Services.UI.Popup.Display(
                            Loc.Format("ui.popup.newBestiary.env.header", bestiary.CommonName()), null,
                                new StreamedImageSet(bestiary.SketchPath(), bestiary.Icon())).Wait();
                    }
                }

                return null;
            }

            [LeafMember("HasFact"), UnityEngine.Scripting.Preserve]
            static private Variant HasFact(StringHash32 inFactId)
            {
                return Services.Data.Profile.Bestiary.HasFact(inFactId);
            }

            [LeafMember("IsFactGraphed"), UnityEngine.Scripting.Preserve]
            static private Variant IsFactGraphed(StringHash32 inFactId)
            {
                return Services.Data.Profile.Bestiary.IsFactGraphed(inFactId);
            }

            [LeafMember("GiveFact"), UnityEngine.Scripting.Preserve]
            static private IEnumerator GiveFact([BindContext] ScriptThread inThread, StringHash32 inFactId, PopupMode inMode = PopupMode.Popup)
            {
                BFBase fact = Assets.Fact(inFactId);
                if (Services.Data.Profile.Bestiary.RegisterFact(inFactId, fact.Type == BFTypeId.Model) && inMode != PopupMode.Silent)
                {
                    BFDiscoveredFlags flags = Services.Data.Profile.Bestiary.GetDiscoveredFlags(inFactId);

                    if (inMode == PopupMode.Batch)
                    {
                        if (Services.UI.IsSkippingCutscene())
                            return null;

                        s_BatchedFacts.PushBack(fact);
                        s_BatchedFactFlags.PushBack(flags);
                    }
                    else
                    {
                        inThread.Dialog = null;

                        if (Services.UI.IsSkippingCutscene())
                        {
                            s_BatchedFactFlags.Clear();
                            s_BatchedFacts.Clear();
                            return null;
                        }

                        Services.Audio.PostEvent("item.popup.new");

                        IEnumerator popup;

                        if (s_BatchedFacts.Count > 0)
                        {
                            s_BatchedFacts.PushBack(fact);
                            s_BatchedFactFlags.PushBack(flags);

                            popup = Services.UI.Popup.PresentFacts(Loc.Find("ui.popup.factsUpdated.header"), null, s_BatchedFacts, s_BatchedFactFlags).Wait();
                            s_BatchedFactFlags.Clear();
                            s_BatchedFacts.Clear();
                        }
                        else
                        {
                            popup = Services.UI.Popup.PresentFact(Loc.Find("ui.popup.newFact.header"), null, fact, Services.Data.Profile.Bestiary.GetDiscoveredFlags(inFactId)).Wait();
                        }

                        return popup;
                    }
                }
                return null;
            }

            [LeafMember("UpgradeFact"), UnityEngine.Scripting.Preserve]
            static private IEnumerator UpgradeFact([BindContext] ScriptThread inThread, StringHash32 inFactId, BFDiscoveredFlags inFlags = BFDiscoveredFlags.Rate, PopupMode inMode = PopupMode.Popup)
            {
                BFBase fact = Assets.Fact(inFactId);
                if (Services.Data.Profile.Bestiary.AddDiscoveredFlags(inFactId, inFlags) && inMode != PopupMode.Silent)
                {
                    BFDiscoveredFlags flags = Services.Data.Profile.Bestiary.GetDiscoveredFlags(inFactId);

                    if (inMode == PopupMode.Batch)
                    {
                        if (Services.UI.IsSkippingCutscene())
                            return null;

                        s_BatchedFacts.PushBack(fact);
                        s_BatchedFactFlags.PushBack(flags);
                    }
                    else
                    {
                        inThread.Dialog = null;

                        if (Services.UI.IsSkippingCutscene())
                        {
                            s_BatchedFactFlags.Clear();
                            s_BatchedFacts.Clear();
                            return null;
                        }

                        Services.Audio.PostEvent("item.popup.new");

                        IEnumerator popup;

                        if (s_BatchedFacts.Count > 0)
                        {
                            s_BatchedFacts.PushBack(fact);
                            s_BatchedFactFlags.PushBack(flags);

                            popup = Services.UI.Popup.PresentFacts(Loc.Find("ui.popup.factsUpdated.header"), null, s_BatchedFacts, s_BatchedFactFlags).Wait();
                            s_BatchedFactFlags.Clear();
                            s_BatchedFacts.Clear();
                        }
                        else
                        {
                            popup = Services.UI.Popup.PresentFact(Loc.Find("ui.popup.upgradedFact.header"), null, fact, Services.Data.Profile.Bestiary.GetDiscoveredFlags(inFactId)).Wait();
                        }

                        return popup;
                    }
                }
                return null;
            }

            [LeafMember("FinishFactBatch"), UnityEngine.Scripting.Preserve]
            static private IEnumerator CompleteFactBatch([BindContext] ScriptThread inThread)
            {
                if (s_BatchedFacts.Count <= 0)
                    return null;
                
                if (Services.UI.IsSkippingCutscene())
                {
                    s_BatchedFactFlags.Clear();
                    s_BatchedFacts.Clear();
                    return null;
                }

                inThread.Dialog = null;
                Services.Audio.PostEvent("item.popup.new");

                IEnumerator popup;

                popup = Services.UI.Popup.PresentFacts(Loc.Find("ui.popup.factsUpdated.header"), null, s_BatchedFacts, s_BatchedFactFlags).Wait();
                s_BatchedFactFlags.Clear();
                s_BatchedFacts.Clear();

                return popup;
            }

            [LeafMember("HasItem"), UnityEngine.Scripting.Preserve]
            static private Variant HasItem(StringHash32 inItemId)
            {
                return Services.Data.Profile.Inventory.HasItem(inItemId);
            }

            [LeafMember("ItemCount"), UnityEngine.Scripting.Preserve]
            static private Variant ItemCount(StringHash32 inItemId)
            {
                return Services.Data.Profile.Inventory.ItemCount(inItemId);
            }

            [LeafMember("HasItemCount"), UnityEngine.Scripting.Preserve]
            static private bool HasItemCount(StringHash32 inItemId, int inCount)
            {
                return Services.Data.Profile.Inventory.ItemCount(inItemId) >= inCount;
            }

            [LeafMember("CanAfford"), UnityEngine.Scripting.Preserve]
            static private bool CanAfford(StringHash32 inItemId)
            {
                var itemDesc = Assets.Item(inItemId);
                var invData = Services.Data.Profile.Inventory;
                return invData.ItemCount(ItemIds.Cash) >= itemDesc.BuyCoinsValue() && invData.ItemCount(ItemIds.Gear) >= itemDesc.BuyGearsValue();
            }

            [LeafMember("PurchaseItem"), UnityEngine.Scripting.Preserve]
            static private IEnumerator PurchaseItem([BindContext] ScriptThread inThread, StringHash32 inItemId)
            {
                var itemDesc = Assets.Item(inItemId);
                var invData = Services.Data.Profile.Inventory;
                invData.AdjustItem(ItemIds.Cash, -itemDesc.BuyCoinsValue());
                invData.AdjustItem(ItemIds.Gear, -itemDesc.BuyGearsValue());

                if (itemDesc.Category() == InvItemCategory.Upgrade) {
                    invData.AddUpgrade(inItemId);
                } else {
                    invData.AdjustItem(inItemId, 1);
                }
                
                inThread.Dialog = null;

                if (Services.UI.IsSkippingCutscene())
                    return null;
                
                Services.Audio.PostEvent("item.popup.new");
                return Services.UI.Popup.Display(
                    Loc.Format("ui.popup.newItem.header", itemDesc.NameTextId()),
                    Loc.Find(itemDesc.DescriptionTextId()),
                    new StreamedImageSet(itemDesc.SketchPath(), itemDesc.Icon())
                ).Wait();
            }

            [LeafMember("GiveItem"), UnityEngine.Scripting.Preserve]
            static private void GiveItem(StringHash32 inItemId, int inCount = 1)
            {
                Assert.True(inCount >= 0, "GiveItem must be passed a non-negative number");
                Services.Data.Profile.Inventory.AdjustItem(inItemId, inCount);
            }

            [LeafMember("TakeItem"), UnityEngine.Scripting.Preserve]
            static private bool TakeItem(StringHash32 inItemId, int inCount = 1)
            {
                Assert.True(inCount >= 0, "TakeItem must be passed a non-negative number");
                return Services.Data.Profile.Inventory.AdjustItem(inItemId, -inCount);
            }

            [LeafMember("SetItem"), UnityEngine.Scripting.Preserve]
            static private void SetItem(StringHash32 inItemId, int inCount)
            {
                Assert.True(inCount >= 0, "SetItem must be passed a non-negative number");
                Services.Data.Profile.Inventory.SetItem(inItemId, inCount);
            }

            [LeafMember("HasUpgrade"), UnityEngine.Scripting.Preserve]
            static private Variant HasUpgrade(StringHash32 inUpgradeId)
            {
                return Services.Data.Profile.Inventory.HasUpgrade(inUpgradeId);
            }

            [LeafMember("GiveUpgrade"), UnityEngine.Scripting.Preserve]
            static private IEnumerator GiveUpgrade([BindContext] ScriptThread inThread, StringHash32 inUpgradeId, PopupMode inMode = PopupMode.Popup)
            {
                if (Services.Data.Profile.Inventory.AddUpgrade(inUpgradeId) && inMode != PopupMode.Silent)
                {
                    inThread.Dialog = null;

                    if (Services.UI.IsSkippingCutscene())
                        return null;
                    
                    InvItem item = Assets.Item(inUpgradeId);
                    Services.Audio.PostEvent("item.popup.new");
                    return Services.UI.Popup.Display(
                        Loc.Format("ui.popup.newUpgrade.header", item.NameTextId()),
                        Loc.Find(item.DescriptionTextId()),
                        new StreamedImageSet(item.SketchPath(), item.Icon())
                    ).Wait();
                }

                return null;
            }

            [LeafMember("HasScanned"), UnityEngine.Scripting.Preserve]
            static private Variant HasScanned(StringHash32 inNodeId)
            {
                return Services.Data.Profile.Inventory.WasScanned(inNodeId);
            }

            [LeafMember("HasWaterProperty"), UnityEngine.Scripting.Preserve]
            static private bool HasProperty(WaterPropertyId inProperty)
            {
                return Services.Data.Profile.Inventory.IsPropertyUnlocked(inProperty);
            }

            [LeafMember("GiveWaterProperty"), UnityEngine.Scripting.Preserve]
            static private bool GiveWaterProperty(WaterPropertyId inProperty)
            {
                return Services.Data.Profile.Inventory.UnlockProperty(inProperty);
            }

            #endregion // Bestiary/Inventory

            #region Shop

            #endregion // Shop

            #region Jobs

            [LeafMember("JobStartedOrComplete"), UnityEngine.Scripting.Preserve]
            static private Variant JobStartedOrComplete(StringHash32 inId)
            {
                return Services.Data.Profile.Jobs.IsStartedOrComplete(inId);
            }

            [LeafMember("JobInProgress"), UnityEngine.Scripting.Preserve]
            static private Variant JobInProgress(StringHash32 inId)
            {
                return Services.Data.Profile.Jobs.IsInProgress(inId);
            }

            [LeafMember("JobCompleted"), UnityEngine.Scripting.Preserve]
            static private Variant JobCompleted(StringHash32 inId)
            {
                return Services.Data.Profile.Jobs.IsComplete(inId);
            }

            [LeafMember("JobAvailable"), UnityEngine.Scripting.Preserve]
            static private Variant JobAvailable(StringHash32 inId)
            {
                return Services.Assets.Jobs.IsAvailableAndUnstarted(inId);
            }

            [LeafMember("JobTaskActive"), UnityEngine.Scripting.Preserve]
            static private Variant JobTaskActive(StringHash32 inId)
            {
                return Services.Data.Profile.Jobs.IsTaskActive(inId);
            }

            [LeafMember("JobTaskCompleted"), UnityEngine.Scripting.Preserve]
            static private Variant JobTaskCompleted(StringHash32 inId)
            {
                return Services.Data.Profile.Jobs.IsTaskComplete(inId);
            }

            [LeafMember("JobTaskTop"), UnityEngine.Scripting.Preserve]
            static public Variant JobTaskTop(StringHash32 inId)
            {
                return Services.Data.Profile.Jobs.IsTaskTop(inId);
            }

            [LeafMember("AnyJobsAvailable"), UnityEngine.Scripting.Preserve]
            static private Variant AnyJobsAvailable()
            {
                var unstarted = Services.Assets.Jobs.UnstartedJobs().GetEnumerator();
                int count = 0;
                
                while(unstarted.MoveNext())
                    ++count;

                return count;
            }

            [LeafMember("AnyJobsInProgress"), UnityEngine.Scripting.Preserve]
            static private Variant AnyJobsInProgress()
            {
                return Services.Data.Profile.Jobs.InProgressJobs().Length;
            }

            [LeafMember("AnyJobsCompleted"), UnityEngine.Scripting.Preserve]
            static private Variant AnyJobsCompleted()
            {
                return Services.Data.Profile.Jobs.CompletedJobIds().Count;
            }

            [LeafMember("UnlockJob"), UnityEngine.Scripting.Preserve]
            static private bool UnlockJob(StringHash32 inJobId)
            {
                return Services.Data.Profile.Jobs.UnlockHiddenJob(inJobId);
            }

            [LeafMember("SetJob"), UnityEngine.Scripting.Preserve]
            static private bool SetJob(StringHash32 inJobId)
            {
                return Services.Data.Profile.Jobs.SetCurrentJob(inJobId);
            }

            [LeafMember("CompleteJob"), UnityEngine.Scripting.Preserve]
            static private bool CompleteJob(StringHash32 inJobId = default(StringHash32))
            {
                if (inJobId.IsEmpty)
                {
                    inJobId = Services.Data.Profile.Jobs.CurrentJobId;
                    if (inJobId.IsEmpty)
                    {
                        Log.Error("[ScriptingService] Attempting to complete job, but no job specified and no job active");
                        return false;
                    }
                }
                
                return Services.Data.Profile.Jobs.MarkComplete(Services.Data.Profile.Jobs.GetProgress(inJobId));
            }

            #endregion // Jobs

            #region World

            [LeafMember("StationUnlocked"), UnityEngine.Scripting.Preserve]
            static private Variant StationUnlocked(StringHash32 inStationId)
            {
                return Services.Data.Profile.Map.IsStationUnlocked(inStationId);
            }

            [LeafMember("UnlockStation"), UnityEngine.Scripting.Preserve]
            static private bool UnlockStation(StringHash32 inStationId)
            {
                return Services.Data.Profile.Map.UnlockStation(inStationId);
            }

            [LeafMember("LockStation"), UnityEngine.Scripting.Preserve]
            static private bool LockStation(StringHash32 inStationId)
            {
                return Services.Data.Profile.Map.LockStation(inStationId);
            }

            [LeafMember("SiteUnlocked"), UnityEngine.Scripting.Preserve]
            static private Variant SiteUnlocked(StringHash32 inSiteId)
            {
                return Services.Data.Profile.Map.IsSiteUnlocked(inSiteId);
            }

            [LeafMember("UnlockSite"), UnityEngine.Scripting.Preserve]
            static private bool UnlockSite(StringHash32 inSiteId)
            {
                return Services.Data.Profile.Map.UnlockSite(inSiteId);
            }

            [LeafMember("LockSite"), UnityEngine.Scripting.Preserve]
            static private bool LockSite(StringHash32 inSiteId)
            {
                return Services.Data.Profile.Map.LockSite(inSiteId);
            }

            [LeafMember("RoomUnlocked"), UnityEngine.Scripting.Preserve]
            static private bool RoomUnlocked(StringHash32 inRoomId)
            {
                return Services.Data.Profile.Map.IsRoomUnlocked(inRoomId);
            }

            [LeafMember("UnlockRoom"), UnityEngine.Scripting.Preserve]
            static private bool UnlockRoom(StringHash32 inRoomId)
            {
                return Services.Data.Profile.Map.UnlockRoom(inRoomId);
            }

            [LeafMember("LockRoom"), UnityEngine.Scripting.Preserve]
            static private bool LockRoom(StringHash32 inRoomId)
            {
                return Services.Data.Profile.Map.LockRoom(inRoomId);
            }

            #endregion // World

            #region Scheduled Events

            [LeafMember("IsEventScheduled"), UnityEngine.Scripting.Preserve]
            static private Variant IsEventScheduled(StringHash32 inEventId)
            {
                return Services.Data.Profile.Script.IsEventScheduled(inEventId);
            }

            [LeafMember("HoursUntilEvent"), UnityEngine.Scripting.Preserve]
            static private Variant HoursUntilEvent(StringHash32 inEventId)
            {
                return Services.Data.Profile.Script.TimeUntilScheduled(inEventId).TotalHours;
            }

            [LeafMember("IsEventReady"), UnityEngine.Scripting.Preserve]
            static private Variant IsEventReady(StringHash32 inEventId)
            {
                return Services.Data.Profile.Script.TimeUntilScheduled(inEventId).Ticks <= 0;
            }

            [LeafMember("GetEventData"), UnityEngine.Scripting.Preserve]
            static private Variant ScheduledEventData(StringHash32 inEventId)
            {
                Variant eventData;
                Services.Data.Profile.Script.TryGetScheduledEventData(inEventId, out eventData);
                return eventData;
            }

            #endregion // Scheduled Events

            [LeafMember("Seen"), UnityEngine.Scripting.Preserve]
            static private Variant Seen(StringHash32 inNodeId)
            {
                return Services.Data.Profile.Script.HasSeen(inNodeId, PersistenceLevel.Profile);
            }

            [LeafMember("Random"), UnityEngine.Scripting.Preserve]
            static private Variant GetRandomByRarity(StringSlice inData)
            {
                float floatVal;
                if (StringParser.TryParseFloat(inData, out floatVal))
                {
                    return RNG.Instance.Chance(floatVal);
                }

                StringHash32 id = inData.Hash32();
                if (inData == RandomRare)
                    return RNG.Instance.Chance(Services.Data.m_RareChance);
                if (inData == RandomUncommon)
                    return RNG.Instance.Chance(Services.Data.m_UncommonChance);
                if (inData == RandomCommon)
                    return RNG.Instance.Chance(Services.Data.m_CommonChance);
                
                Log.Error("[DataService] Unknown rarity '{0}'", inData);
                return false;
            }

            static private readonly StringHash32 RandomRare = "rare";
            static private readonly StringHash32 RandomUncommon = "uncommon";
            static private readonly StringHash32 RandomCommon = "common";
        }

        #endregion // Leaf
    }
}