using Dalamud.Game.Addon.Events;
using Dalamud.Game.Addon.Events.EventDataTypes;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Client.System.Resource;
using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Serilog;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using static FFXIVClientStructs.FFXIV.Component.GUI.AtkResNode.Delegates;
using ComponentType = FFXIVClientStructs.FFXIV.Component.GUI.ComponentType;
namespace PartyHotbar.Node.Component;

internal unsafe class Base : IDisposable
{
    internal class EventHandlerInfo
    {
        public AtkEventListener.Delegates.ReceiveEvent? OnReceiveEventDelegate;
        public Action? OnActionDelegate;
    }


    private delegate ResourceHandle* GetResourceSyncDelegate(ResourceCategory* category, uint* type, byte* path, nint para);
    private delegate void BuildComponentDelegate(AtkUldManager* AtkUldManager, nint res, uint componentId, ushort* timeline, AtkUldAsset* uldAsset, AtkUldPartsList* uldPartList, ushort assetNum, ushort partsNum, AtkResourceRendererManager* renderManager, bool a, bool b);
    private delegate void BuildComponentTimelineDelegate(AtkUldManager* AtkUldManager, nint res, uint componentId, AtkTimelineManager* timelineManager, AtkResNode* resNode);
    private delegate void BuildWidgetDelegate(AtkUldManager* AtkUldManager, byte* a1, byte* a2);

    private static GetResourceSyncDelegate getResourceSync = Marshal.GetDelegateForFunctionPointer<GetResourceSyncDelegate>(Service.SigScanner.ScanText("E8 ?? ?? ?? ?? 48 85 C0 40 0F B6 CF"));
    private static BuildComponentDelegate buildComponent = Marshal.GetDelegateForFunctionPointer<BuildComponentDelegate>(Service.SigScanner.ScanText("E8 ?? ?? ?? ?? 49 8B 86 ?? ?? ?? ?? 48 85 C0 74 21"));
    private static BuildComponentTimelineDelegate buildComponentTimeline = Marshal.GetDelegateForFunctionPointer<BuildComponentTimelineDelegate>(Service.SigScanner.ScanText("48 89 6C 24 ?? 48 89 74 24 ?? 41 56 48 83 EC 30 4C 89 49"));
    private static BuildWidgetDelegate buildWidget = Marshal.GetDelegateForFunctionPointer<BuildWidgetDelegate>(Service.SigScanner.ScanText("E8 ?? ?? ?? ?? F6 83 ?? ?? ?? ?? ?? 75 17"));

    private static readonly Dictionary<string, nint> UldManagerCache = new();

    public delegate void EventDelegate(AddonEventType atkEventType, Base senderComponent, AddonEventData data);
    public Dictionary<AddonEventType, EventDelegate> Events { get; init; } = new();
    internal Dictionary<AddonEventType, IAddonEventHandle> EventHandles = new();
    public EventDelegate? OnClick;
    public AtkUnitBase* Addon { get; private set; } = null;
    internal AtkCollisionNode* CollisionNode { get; init; } = null;

    public bool Visible
    {
        get => Node->IsVisible();
        set
        {
            if (value)
            {
                Node->NodeFlags |= NodeFlags.Visible;
            }
            else
            {
                Node->NodeFlags &= ~NodeFlags.Visible;
            }
        }
    }

    public uint NodeId
    {
        get => Node->NodeId;
        set => Node->NodeId = value;
    }

    public float X
    {
        get => Node->X;
        set => Node->X = value;
    }

    public float Y
    {
        get => Node->Y;
        set => Node->Y = value;
    }

    public ushort Width
    {
        get => Node->Width;
        set => Node->Width = value;
    }

    public ushort Height
    {
        get => Node->Height;
        set => Node->Height = value;
    }

    private static unsafe ResourceHandle* GetResourceSyncWrapper(ResourceCategory category, uint type, string path, nint para)
    {
        var catagoryPtr = stackalloc uint[1];
        var typePtr = stackalloc uint[1];
        var strPtr = stackalloc byte[path.Length];
        Marshal.WriteInt32((nint)catagoryPtr, (int)category);
        Marshal.WriteInt32((nint)typePtr, (int)type);

        int utf8StringLengthname = Encoding.UTF8.GetByteCount(path);
        Span<byte> nameBytes = utf8StringLengthname <= 512 ? stackalloc byte[utf8StringLengthname + 1] : new byte[utf8StringLengthname + 1];
        Encoding.UTF8.GetBytes(path, nameBytes);
        nameBytes[utf8StringLengthname] = 0;
        fixed (byte* namePtr = nameBytes)
        {
            return getResourceSync((ResourceCategory*)catagoryPtr, typePtr, namePtr, para);
        }
    }
    private static AtkComponentNode* BuildComponentNode(string uldPath, uint componentId, ComponentType componentType)
    {
        Service.PluginLog.Debug($"Building ComponentNode:{uldPath} {componentId} {componentType}");
        var uldManager = GetUldManager(uldPath);
        var componentNode = (AtkComponentNode*)uldManager->CreateAtkNode(unchecked((NodeType)componentId));
        componentNode->AtkResNode.Ctor();
        //componentNode->NodeId = nodeId;
        componentNode->Type = unchecked((NodeType)componentId);
        componentNode->Component = uldManager->CreateAtkComponent(componentType);
        componentNode->Component->Initialize();
        componentNode->Component->OwnerNode = componentNode;
        componentNode->Component->ComponentFlags = 1;
        componentNode->Component->UldManager.UldResourceHandle = uldManager->UldResourceHandle;
        componentNode->Component->UldManager.ResourceFlags = AtkUldManagerResourceFlag.Initialized;
        componentNode->NodeFlags = NodeFlags.Visible | NodeFlags.Enabled | NodeFlags.EmitsEvents;

        var resourcePtr = uldManager->UldResourceHandle->GetData();
        var componentResourcePtr = (byte*)(char*)&resourcePtr[*((uint*)resourcePtr + 2)];
        var tinelineNum = stackalloc ushort[1];
        tinelineNum[0] = 223;

        buildComponent((AtkUldManager*)Unsafe.AsPointer(ref componentNode->Component->UldManager), (nint)componentResourcePtr, componentId, tinelineNum, uldManager->Assets, uldManager->PartsList, uldManager->AssetCount, uldManager->PartsListCount, uldManager->ResourceRendererManager, true, true);
        buildComponentTimeline((AtkUldManager*)Unsafe.AsPointer(ref componentNode->Component->UldManager), (nint)componentResourcePtr, componentId, uldManager->TimelineManager, (AtkResNode*)componentNode);
        //uldManager->UpdateDrawNodeList();
        uldManager->InitializeResourceRendererManager();
        uldManager->UpdateDrawNodeList();
        uldManager->ResourceFlags = AtkUldManagerResourceFlag.Initialized | AtkUldManagerResourceFlag.ArraysAllocated;
        uldManager->LoadedState = AtkLoadState.Loaded;
        return componentNode;
    }
    private static AtkUldManager* GetUldManager(string uldPath)
    {
        if (UldManagerCache.TryGetValue(uldPath, out var uldPtr))
        {
            Service.PluginLog.Debug($"Got UldManager in cache:{uldPath}");
            return (AtkUldManager*)uldPtr;
        }
        Service.PluginLog.Debug($"Building UldManager:{uldPath}");
        var uldResourceHandle = GetResourceSyncWrapper(ResourceCategory.Ui, 0x756C64, uldPath, nint.Zero);
        var resourcePtr = uldResourceHandle->GetData();
        var newUldManager = (AtkUldManager*)IMemorySpace.GetUISpace()->Malloc((ulong)sizeof(AtkUldManager), 8uL);
        IMemorySpace.Memset(newUldManager, 0, (ulong)sizeof(AtkUldManager));
        newUldManager->UldResourceHandle = uldResourceHandle;
        newUldManager->ResourceFlags = AtkUldManagerResourceFlag.Initialized;
        newUldManager->BaseType = AtkUldManagerBaseType.Widget;
        buildWidget(newUldManager, (byte*)(char*)&resourcePtr[*((uint*)resourcePtr + 2)], (byte*)(char*)&resourcePtr[*((uint*)resourcePtr + 3)]);
        UldManagerCache[uldPath] = (nint)newUldManager;
        return newUldManager;
    }
    public void AttachNode(AtkResNode* attachTargetNode)
    {
        if (Addon != null)
            throw new Exception($"{(nint)Node:X} {Node->NodeId} has attached to {Addon->NameString}");
        NodeLinker.AttachNode((AtkResNode*)Node, attachTargetNode, NodePosition.AsLastChild);
    }

    public void DetachNode()
    {
        NodeLinker.DetachNode((AtkResNode*)Node);
    }

    public void BindEvents(AtkUnitBase* addon)
    {
        Addon = addon;
        foreach (var item in Events)
        {
            Service.PluginLog.Debug($"Adding Event: {item.Key} to {Node->NodeId}");
            EventHandles[item.Key] = Service.AddonEventManager.AddEvent((nint)Addon, (nint)CollisionNode, item.Key, (atkEventType, data) => item.Value.Invoke(atkEventType, this, data))!;
            Events.Remove(item.Key);
        }
    }

    public void AddEvent(AddonEventType eventType, EventDelegate eventDelegate)
    {
        if (Addon != null)
        {
            if (EventHandles.TryGetValue(eventType, out var eventHandle))
            {
                Service.AddonEventManager.RemoveEvent(eventHandle);
            }
            EventHandles[eventType] = Service.AddonEventManager.AddEvent((nint)Addon, (nint)CollisionNode, eventType, (atkEventType, data) => eventDelegate.Invoke(atkEventType, this, data))!;
        }
        else
        {
            Events.Add(eventType, eventDelegate);
        }

    }

    public readonly AtkComponentNode* Node;
    public Base(string uldPath, uint componentId, ComponentType componentType)
    {
        Node = BuildComponentNode(uldPath, componentId, componentType);
    }
    public void RemoveEvents()
    {
        foreach (var item in EventHandles)
        {
            Service.AddonEventManager.RemoveEvent(item.Value);
            EventHandles.Remove(item.Key);
        }
    }

    public virtual void Dispose(bool removeEvents = true)
    {
        if (!removeEvents)
            RemoveEvents();
        if (Node != null)
        {
            DetachNode();
        }
        GC.SuppressFinalize(this);
    }

    public void Dispose()
    {
        Dispose(true);
    }
}