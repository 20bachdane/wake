using System.Collections.Generic;
using UnityEngine;
using BeauUtil;
using Aqua.Character;

namespace Aqua.StationMap
{
    public class DiveSiteMap : MonoBehaviour, ISceneLoadHandler, ISceneOptimizable
    {
        [SerializeField, HideInInspector] private PlayerController m_Player = null;
        [SerializeField, HideInInspector] private DiveSite[] m_DiveSites;
        [SerializeField, HideInInspector] private SpawnLocationMap m_Spawns;

        public void OnSceneLoad(SceneBinding inScene, object inContext)
        {
            StringHash32 entrance = Services.State.LastEntranceId;

            var job = Save.Jobs.CurrentJob.Job;
            var mapData = Save.Map;

            foreach(var site in m_DiveSites)
            {
                site.Initialize(mapData, job);
            }

            if (entrance.IsEmpty)
                entrance = "Ship";
            
            SpawnLocation location = m_Spawns.FindLocation(entrance);
            if (location != null)
                m_Player.TeleportTo(location);

            StringHash32 currentMap = MapDB.LookupCurrentMap();
            mapData.SetCurrentStationId(currentMap);
        }

        #if UNITY_EDITOR

        void ISceneOptimizable.Optimize()
        {
            List<DiveSite> diveSites = new List<DiveSite>(8);
            SceneHelper.ActiveScene().Scene.GetAllComponents<DiveSite>(true, diveSites);
            m_DiveSites = diveSites.ToArray();
            m_Player = FindObjectOfType<PlayerController>();
            m_Spawns = FindObjectOfType<SpawnLocationMap>();
        }

        #endif // UNITY_EDITOR
    }
}