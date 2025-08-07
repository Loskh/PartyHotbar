using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PartyHotbar.Extensions;

public static unsafe class AtkUldManagerExtensions
{

    // WARNING, volatile if called on a component that has nodes from other addons. 
    public static void ResyncData(ref this AtkUldManager parentUldManager)
    {
        parentUldManager.UpdateDrawNodeList();

        // Process ObjectListAdditions
        foreach (var index in Enumerable.Range(0, parentUldManager.NodeListCount))
        {
            var nodePointer = parentUldManager.NodeList[index];

            // If the object list doesn't have this node, then it is supposed to, add it.
            if (!parentUldManager.IsNodeInObjectList(nodePointer))
            {
                parentUldManager.AddNodeToObjectList(nodePointer);
            }
        }

        // Process ObjectListRemovals
        foreach (var index in Enumerable.Range(0, parentUldManager.Objects->NodeCount))
        {
            var nodePointer = parentUldManager.Objects->NodeList[index];

            // If the DrawList doesn't have this node, then we need to remove it from objects list
            if (!parentUldManager.IsNodeInDrawList(nodePointer))
            {
                parentUldManager.RemoveNodeFromObjectList(nodePointer);
            }
        }
    }

    private static bool IsNodeInObjectList(ref this AtkUldManager uldManager, AtkResNode* node)
    {
        foreach (var objectNode in uldManager.GetObjectsNodeSpan())
        {
            Service.PluginLog.Debug($"{(nint)objectNode.Value:X}:{objectNode.Value->NodeId} {(nint)node:X}:{node->NodeId} {(objectNode.Value == node)}");
            if (objectNode.Value == node) return true;
        }

        return false;
    }

    private static bool IsNodeInDrawList(ref this AtkUldManager uldManager, AtkResNode* node)
    {
        foreach (var drawNode in uldManager.Nodes)
        {
            if (drawNode.Value == node) return true;
        }

        return false;
    }

    public static void AddNodeToObjectList(ref this AtkUldManager uldManager, AtkResNode* newNode)
    {
        // If the node is already in the object list, skip.
        if (uldManager.IsNodeInObjectList(newNode)) return;

        var oldSize = uldManager.Objects->NodeCount;
        var newSize = oldSize + 1;
        var newBuffer = (AtkResNode**)IMemorySpace.GetUISpace()->Malloc((ulong)(newSize * 8), 8uL);

        if (oldSize > 0)
        {
            foreach (var index in Enumerable.Range(0, oldSize))
            {
                newBuffer[index] = uldManager.Objects->NodeList[index];
            }

            IMemorySpace.Free(uldManager.Objects->NodeList, (ulong)(oldSize * 8));
        }

        newBuffer[newSize - 1] = newNode;

        uldManager.Objects->NodeList = newBuffer;
        uldManager.Objects->NodeCount = newSize;
    }

    public static void RemoveNodeFromObjectList(ref this AtkUldManager uldManager, AtkResNode* node)
    {
        Service.PluginLog.Debug($"Removing {(nint)node:X} {node->NodeId} from {(nint)uldManager.RootNode->ParentNode:X} {uldManager.RootNode->ParentNode->NodeId}");
        // If the node isn't in the object list, skip.
        if (!uldManager.IsNodeInObjectList(node)) return;

        var oldSize = uldManager.Objects->NodeCount;
        var newSize = oldSize - 1;
        var newBuffer = (AtkResNode**)IMemorySpace.GetUISpace()->Malloc((ulong)(newSize * 8), 8uL);

        var newIndex = 0;
        foreach (var index in Enumerable.Range(0, oldSize))
        {
            if (uldManager.Objects->NodeList[index] != node)
            {
                newBuffer[newIndex] = uldManager.Objects->NodeList[index];
                newIndex++;
            }
        }

        IMemorySpace.Free(uldManager.Objects->NodeList, (ulong)(oldSize * 8));
        uldManager.Objects->NodeList = newBuffer;
        uldManager.Objects->NodeCount = newSize;
        Service.PluginLog.Debug($"Removed {(nint)node:X} {node->NodeId} from {uldManager.RootNode->ParentNode->NodeId}");
    }

    public static Span<Pointer<AtkResNode>> GetObjectsNodeSpan(ref this AtkUldManager uldManager)
        => new(uldManager.Objects->NodeList, uldManager.Objects->NodeCount);
}
