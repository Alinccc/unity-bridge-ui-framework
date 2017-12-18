using UnityEngine;
using UnityEditor;

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Collections.Generic;
using System.Security.Cryptography;

using NodeGraph;
using NodeGraph.DataModel;
using BridgeUI.Model;

namespace BridgeUI
{
    public class BridgeUIGraphCtrl:NodeGraphController
    {
        /// <summary>
        /// 将信息到保存到PanelGroup
        /// </summary>
        /// <param name="group"></param>
        private void StoreInfoOfPanel(PanelGroup group)
        {
            InsertBridges(group.bridges, GetBridges());
            if (group.loadType == LoadType.Prefab)
            {
                InsertPrefabinfo(group.p_nodes, GetPrefabUIInfos(GetNodeInfos()));
            }
            else if (group.loadType == LoadType.Bundle)
            {
                InsertBundleinfo(group.b_nodes, GetBundleUIInfos(GetNodeInfos()));
            }
            TryRecoredGraphGUID(group);
            EditorUtility.SetDirty(group);
        }

        private void TryRecoredGraphGUID(PanelGroup group)
        {
            var path = AssetDatabase.GetAssetPath(TargetGraph);
            var guid = AssetDatabase.AssetPathToGUID(path);
            if (group is PanelGroup)
            {
                var panelGroup = group as PanelGroup;
                var record = panelGroup.graphList.Find(x => x.guid == guid);
                if (record == null)
                {
                    var item = new GraphWorp(TargetGraph.name, guid);
                    panelGroup.graphList.Add(item);
                }
                else
                {
                    record.graphName = TargetGraph.name;
                }
            }
        }

        private void InsertBridges(List<BridgeInfo> source, List<BridgeInfo> newBridges)
        {
            if (newBridges == null) return;
            foreach (var item in newBridges)
            {
                if (string.IsNullOrEmpty(item.outNode)) continue;
                var old = source.Find(x => (x.inNode == item.inNode || (string.IsNullOrEmpty(x.inNode) && string.IsNullOrEmpty(item.inNode))) && x.outNode == item.outNode);
                if (old != null)
                {
                    old.showModel = item.showModel;
                }
                else
                {
                    source.Add(item);
                }
            }
        }
        private void InsertPrefabinfo(List<PrefabUIInfo> source, List<PrefabUIInfo> newInfo)
        {
            if (newInfo == null) return;
            foreach (var item in newInfo)
            {
                var old = source.Find(x => x.panelName == item.panelName);
                if (old != null)
                {
                    old.prefab = item.prefab;
                    old.type = item.type;
                }
                else
                {
                    source.Add(item);
                }
            }
        }
        private void InsertBundleinfo(List<BundleUIInfo> source, List<BundleUIInfo> newInfo)
        {
            if (newInfo == null) return;
            foreach (var item in newInfo)
            {
                CompleteBundleUIInfo(item);

                var old = source.Find(x => x.panelName == item.panelName);

                if (old != null)
                {
                    old.guid = item.guid;
                    old.type = item.type;
                }
                else
                {
                    source.Add(item);
                }
            }
        }
        private List<BridgeInfo> GetBridges()
        {
            var nodes = TargetGraph.Nodes;
            var connectons = TargetGraph.Connections;
            var bridges = new List<BridgeInfo>();
            foreach (var item in connectons)
            {
                if (!(item.Operation.Object is BridgeConnection)) continue;
                var connection = item.Operation.Object as BridgeConnection;

                var bridge = new BridgeInfo();
                var innode = nodes.Find(x => x.OutputPoints != null && x.OutputPoints.Find(y => y.Id == item.FromNodeConnectionPointId) != null);
                var outnode = nodes.Find(x => x.InputPoints != null && x.InputPoints.Find(y => y.Id == item.ToNodeConnectionPointId) != null);
                if (innode != null)
                {
                    if (innode.Operation.Object is IPanelInfoHolder)
                    {
                        bridge.inNode = innode.Name;
                    }
                }

                if (outnode != null && outnode.Operation.Object is IPanelInfoHolder)
                {
                    bridge.outNode = outnode.Name;
                }

                bridge.showModel = connection.show;
                bridges.Add(bridge);
            }
            return bridges;
        }

        private List<NodeInfo> GetNodeInfos()
        {
            var nodeInfos = new List<NodeInfo>();
            var nodes = TargetGraph.Nodes;
            foreach (var item in nodes)
            {
                var nodeItem = item.Operation.Object as IPanelInfoHolder;
                if (nodeItem != null)
                {
                    nodeInfos.Add(nodeItem.Info);
                }
            }
            return nodeInfos;
        }

        private List<PrefabUIInfo> GetPrefabUIInfos(List<NodeInfo> infos)
        {
            var pinfos = new List<PrefabUIInfo>();
            foreach (var item in infos)
            {
                var p = new PrefabUIInfo();
                p.type = item.uiType;
                p.prefab = LoadPrefabFromGUID(item.prefabGuid);
                p.panelName = p.prefab.name;
                pinfos.Add(p);
            }
            return pinfos;
        }

        private GameObject LoadPrefabFromGUID(string guid)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrEmpty(path))
            {
                return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            else
            {
                return null;
            }
        }

        private List<BundleUIInfo> GetBundleUIInfos(List<NodeInfo> infos)
        {
            var binfo = new List<BundleUIInfo>();
            foreach (var item in infos)
            {
                var p = new BundleUIInfo();
                p.type = item.uiType;
                p.guid = item.prefabGuid;
                binfo.Add(p);
            }
            return binfo;
        }

        private void CompleteBundleUIInfo(BundleUIInfo binfo)
        {
            if (string.IsNullOrEmpty(binfo.guid))
            {
                return;
            }
            else
            {
                var path = AssetDatabase.GUIDToAssetPath(binfo.guid);
                var importer = AssetImporter.GetAtPath(path);
                var obj = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (importer)
                {
                    binfo.bundleName = importer.assetBundleName;
                    binfo.panelName = obj.name;
                    binfo.good = true;
                }
                else
                {
                    binfo.good = false;
                }
            }
        }

        internal override void Validate(NodeGUI node)
        {
            var changed = false;
            if (node.Data.Operation.Object is IPanelInfoHolder)
            {
                var nodeItem = node.Data.Operation.Object as IPanelInfoHolder;
                var guid = nodeItem.Info.prefabGuid;
                if (!string.IsNullOrEmpty(guid) && !string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(guid)))
                {
                    node.ResetErrorStatus();
                    changed = true;
                }
            }
            if(changed)
            {
                Perform();
            }
        }
        protected override void JudgeNodeExceptions(ConfigGraph m_targetGraph, List<NodeException> m_nodeExceptions)
        {
            foreach (var item in TargetGraph.Nodes)
            {
                if (item.Operation.Object is IPanelInfoHolder)
                {
                    var nodeItem = item.Operation.Object as IPanelInfoHolder;
                    var guid = nodeItem.Info.prefabGuid;
                    if (string.IsNullOrEmpty(guid) || string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(guid)))
                    {
                        m_nodeExceptions.Add(new NodeException("prefab is null", item.Id));
                    }
                }
            }
        }

        protected override void BuildFromGraph(ConfigGraph m_targetGraph)
        {
            if (Selection.activeGameObject != null)
            {
                var panelGroup = Selection.activeGameObject.GetComponent<PanelGroup>();
                if (panelGroup != null)
                {
                    StoreInfoOfPanel(panelGroup);
                }
            }
        }

        internal override List<KeyValuePair<string, Node>> OnDragAccept(UnityEngine.Object[] objectReferences)
        {
            var nodeList = new List<KeyValuePair<string,Node>>();
            foreach (UnityEngine.Object obj in DragAndDrop.objectReferences)
            {
                var path = AssetDatabase.GetAssetPath(obj);
                FileAttributes attr = File.GetAttributes(path);
                PanelNode panelNode = null;
                if ((attr & FileAttributes.Directory) == FileAttributes.Directory)
                {
                    var files = System.IO.Directory.GetFiles(path, "*.prefab", SearchOption.AllDirectories);
                    foreach (var item in files)
                    {
                        panelNode = new PanelNode(item);
                        nodeList.Add(new KeyValuePair<string, Node>(Path.GetFileNameWithoutExtension(item), panelNode));
                    }
                }
                else if (obj is GameObject)
                {
                    panelNode = new PanelNode(path);
                    nodeList.Add(new KeyValuePair<string, Node>(obj.name, panelNode));
                }
            }
            return nodeList;
        }

    }
}
