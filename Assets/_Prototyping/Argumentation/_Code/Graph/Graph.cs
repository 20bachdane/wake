﻿using System;
using System.Collections.Generic;
using Aqua;
using BeauUtil;
using UnityEngine;

namespace ProtoAqua.Argumentation
{
    public class Graph : MonoBehaviour, ISceneLoadHandler, ISceneUnloadHandler
    {
        [Header("Graph Dependencies")]
        [SerializeField] private GraphDataManager m_GraphDataManager = null;

        #pragma warning disable CS0414

        [Header("-- DEBUG --")]
        [SerializeField] private GraphDataPackage m_DebugPackage = null;

        #pragma warning restore CS0414

        private Dictionary<StringHash32, Node> nodeDictionary = new Dictionary<StringHash32, Node>();
        private Dictionary<StringHash32, Link> linkDictionary = new Dictionary<StringHash32, Link>();

        private ConditionsData conditions;
        private Node rootNode;
        private Node currentNode;
        private string endNodeId;
        private string defaultInvalidNodeId;

        public event Action OnGraphLoaded;
        public event Action OnGraphNotAvailable;

        #region Accessors

        public Dictionary<StringHash32, Link> LinkDictionary
        {
            get { return linkDictionary; }
        }

        public Node RootNode
        {
            get { return rootNode; }
        }

        public string EndNodeId
        {
            get { return endNodeId; }
        }

        public ConditionsData Conditions
        {
            get { return conditions; }
        }

        #endregion // Accessors

        void ISceneUnloadHandler.OnSceneUnload(SceneBinding inScene, object inContext)
        {
            Services.Tweaks?.Unload(m_GraphDataManager);
        }

        void ISceneLoadHandler.OnSceneLoad(SceneBinding inScene, object inContext)
        {
            Services.Tweaks.Load(m_GraphDataManager);

            JobDesc currentJob = Services.Data.CurrentJob()?.Job;
            GraphDataPackage script = currentJob?.FindAsset<GraphDataPackage>();
            
            #if UNITY_EDITOR
            if (!script && BootParams.BootedFromCurrentScene)
                script = m_DebugPackage;
            #else
            m_DebugPackage = null;
            #endif // UNITY_EDITOR

            if (script != null)
            {
                LoadGraph(script);
            }
            else
            {
                if (OnGraphNotAvailable != null)
                    OnGraphNotAvailable();
            }
        }

        // Given a link id, check if that link is a valid response for the current node.
        // If valid, find that link and the id of the next node based on the current node.
        // Then check if conditions for traversing to that next node are met.
        public Node NextNode(StringHash32 id)
        {
            if (currentNode.CheckResponse(id))
            {
                StringHash32 nextNodeId = currentNode.GetNextNodeId(id);
                Node nextNode = FindNode(nextNodeId);

                if (nextNode != null)
                {
                    if (conditions.CheckConditions(nextNode, id))
                    {
                        currentNode = nextNode;
                        return currentNode;
                    }
                    else
                    {
                        return FindNode(currentNode.InvalidNodeId);
                    }
                }
                else
                {
                    // If no nextNodeId, go to default node
                    // TODO: Find better implementation for default node
                    return FindNode(currentNode.DefaultNodeId);
                }
            }
            else
            {
                // If id isn't valid, display invalid fact node
                if (currentNode.InvalidNodeId != null)
                {
                    return FindNode(currentNode.InvalidNodeId);
                }

                return FindNode(defaultInvalidNodeId);
            }
        }

        // Helper method for finding a node given its id
        public Node FindNode(StringHash32 id)
        {
            if (nodeDictionary.TryGetValue(id, out Node node))
            {
                return node;
            }

            return null;
        }

        // Helper method for finding a link given its id
        public Link FindLink(StringHash32 id)
        {
            if (linkDictionary.TryGetValue(id, out Link link))
            {
                return link;
            }

            return null;
        }

        private void ResetGraph()
        {
            nodeDictionary = new Dictionary<StringHash32, Node>();
            linkDictionary = new Dictionary<StringHash32, Link>();
            rootNode = null;
            currentNode = null;
            endNodeId = null;
            conditions = null;
        }

        private void LoadGraph(GraphDataPackage inPackage)
        {
            ResetGraph();

            inPackage.Parse(Parsing.Block, new GraphDataPackage.Generator());

            foreach (KeyValuePair<string, Node> kvp in inPackage.Nodes)
            {
                Node node = kvp.Value;
                node.InitializeNode();
                nodeDictionary.Add(node.Id, node);
            }

            rootNode = FindNode(inPackage.RootNodeId);

            // Checks if no root node was specified
            if (rootNode == null)
            {
                throw new System.ArgumentNullException("No root node specified");
            }

            currentNode = rootNode;
            conditions = new ConditionsData(currentNode.Id);

            endNodeId = inPackage.EndNodeId;

            if (endNodeId == null)
            {
                throw new System.ArgumentNullException("No end node specified");
            }


            defaultInvalidNodeId = inPackage.DefaultInvalidNodeId;

            if (defaultInvalidNodeId == null)
            {
                throw new System.ArgumentNullException("No default invalid node specified");
            }

            if (!string.IsNullOrEmpty(inPackage.LinksFile))
            {
                LoadLinks(m_GraphDataManager.GetPackage(inPackage.LinksFile));
            }

            LoadLinks(inPackage);

            if (OnGraphLoaded != null)
                OnGraphLoaded();
        }

        private void LoadLinks(GraphDataPackage inPackage)
        {
            DataService dataService = Services.Data;

            foreach (KeyValuePair<string, Link> kvp in inPackage.Links)
            {
                Link link = kvp.Value;
                link.InitializeLink();
                linkDictionary.Add(link.Id, link);

            }

        }
    }
}
