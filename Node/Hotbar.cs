using Dalamud.Game.Addon.Events;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Party;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Group;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Client.UI.Arrays;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.STD.ContainerInterface;
using Lumina.Excel.Sheets;
using Lumina.Excel.Sheets.Experimental;
using PartyHotbar.Extensions;
using PartyHotbar.Node.Component;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static Dalamud.Interface.Utility.Raii.ImRaii;
using static FFXIVClientStructs.FFXIV.Client.UI.ListPanel;
using static Lumina.Data.Parsing.Uld.NodeData;
using Action = Lumina.Excel.Sheets.Action;
namespace PartyHotbar.Node;

internal unsafe class Hotbar : IDisposable
{
    public static readonly uint MaxActionCount = 4;
    private List<DragDrop> actionButtons { get; set; } = [];
    //public IPartyMember PartyMember => Service.PartyList.
    public AtkComponentNode* Node { get; set; } = null!;
    public readonly int PartyListIndex;
    private readonly ActionManager actionManager;
    public uint NodeId { get => Node->NodeId; set => Node->NodeId = value; }
    public bool Visible
    {
        get => Node->IsVisible();
        set
        {
            if (Node->IsVisible() != value)
            {
                Node->ToggleVisibility(value);
            }
        }
    }
    public Hotbar(ActionManager actionManager, int partIndex)
    {
        Node = (AtkComponentNode*)IMemorySpace.GetUISpace()->Malloc((ulong)sizeof(AtkComponentNode), 8uL);
        Node->Ctor();
        Node->Type = (NodeType)1004;

        var componentBase = (AtkComponentBase*)IMemorySpace.GetUISpace()->Malloc((ulong)sizeof(AtkComponentBase), 8uL);
        componentBase->Ctor();
        componentBase->Initialize();
        Node->Component = componentBase;
        var collisionNode = (AtkCollisionNode*)IMemorySpace.GetUISpace()->Malloc((ulong)sizeof(AtkCollisionNode), 8uL);
        collisionNode->Ctor();
        collisionNode->Type = NodeType.Collision;
        collisionNode->ParentNode = (AtkResNode*)Node;
        collisionNode->ToggleVisibility(true);
        componentBase->OwnerNode = (AtkComponentNode*)Node;
        componentBase->ComponentFlags = 1;

        var resNode = (AtkResNode*)IMemorySpace.GetUISpace()->Malloc((ulong)sizeof(AtkResNode), 8uL);
        resNode->Ctor();
        resNode->Type = NodeType.Res;
        resNode->ParentNode = (AtkResNode*)Node;
        resNode->ToggleVisibility(true);

        ref var uldManager = ref componentBase->UldManager;
        uldManager.Objects = (AtkUldObjectInfo*)IMemorySpace.GetUISpace()->Malloc((ulong)sizeof(AtkUldComponentInfo), 8uL);

        var objects = (AtkUldComponentInfo*)uldManager.Objects;
        uldManager.ObjectCount = 1;

        uldManager.RootNode = (AtkResNode*)collisionNode;
        objects->NodeList = (AtkResNode**)IMemorySpace.GetUISpace()->Malloc(2 * (ulong)sizeof(AtkResNode**), 8uL);
        objects->NodeList[0] = (AtkResNode*)collisionNode;
        objects->NodeCount = 1;

        NodeLinker.AttachNode(resNode, Node->Component->UldManager.RootNode, NodePosition.AfterAllSiblings);

        objects->Id = 1001;
        objects->ComponentType = ComponentType.Base;
        uldManager.NodeListCount = 1;

        for (var i = 0; i < MaxActionCount; i++)
        {
            var button = new DragDrop() { NodeId = (uint)i };
            actionButtons.Add(button);
            button.AttachNode(resNode);
            button.Visible = true;
            var index = i;
            button.OnClick = (_,_,_) => OnClick(index);
        }

        uldManager.InitializeResourceRendererManager();

        Node->NodeId = 114514;
        Node->ToggleVisibility(true);
        Node->Width = 44;
        Node->Height = 44;
        Node->DrawFlags |= 1;
        //Node->NodeFlags = NodeFlags.Visible | NodeFlags.AnchorLeft | NodeFlags.AnchorTop;
        this.Node->Priority = 100;

        uldManager.UpdateDrawNodeList();
        uldManager.ResourceFlags = AtkUldManagerResourceFlag.Initialized | AtkUldManagerResourceFlag.ArraysAllocated;
        uldManager.LoadedState = AtkLoadState.Loaded;

        this.actionManager = actionManager;
        PartyListIndex = partIndex;
    }

    public Vector2 CenterPosition = new Vector2(0, 0);
    public Action[] Actions { get; private set; } = new Action[0];
    private float initY = 0;

    public void SetHotbarActions(Action[] actions, float initY, uint XPitch = 54, float scale = 1.0f)
    {
        this.Actions = actions;
        this.initY = initY;
        this.Node->Y = initY - 44 * scale / 2;
        this.Node->Height = 44;
        this.Node->X = 0;
        this.Node->SetScale(scale, scale);
        for (var i = 0; i < actions.Length; i++)
        {
            actionButtons[i].Visible = true;
            actionButtons[i].IconId = actions[i].Icon;
            actionButtons[i].X = XPitch * i;
            this.Node->Width = (ushort)(this.Node->X + actionButtons[i].X + actionButtons[i].Width);
            actionButtons[i].ChargeNum = actions[i].MaxCharges;
            actionButtons[i].Chargeable = actions[i].MaxCharges != 0;
            actionButtons[i].RecastPercent = 0;
            actionButtons[i].ChargePercent = 0;
            actionButtons[i].Reset();
            actionButtons[i].Node->DrawFlags |= 1;
        }
        for (var i = actions.Length; i < actionButtons.Count; i++)
        {
            actionButtons[i].Visible = false;
        }
    }

    private void OnClick(int index)
    {
        Service.PluginLog.Info($"Clicked");
        //Service.PluginLog.Info($"Clicked {Actions[index].Name.ToString()} {mainGroup.GetPartyMemberByIndex(PartyListIndex) == null}");
        //ref var target =ref partyListNumberArray->PartyMembers[PartyListIndex];
       //var partyListNumberArray = PartyListNumberArray.Instance();
       // var target = partyListNumberArray->PartyMembers[PartyListIndex];
       // Service.PluginLog.Info($"Clicked {Actions[index].Name.ToString()} {target.ContentId:X} {PartyListIndex} -> {target.ContentId:X}");
       // actionManager.Manager->UseAction(ActionType.Action, Actions[index].RowId, (ulong)target.ContentId);
    }

    public void Update(in HotbarActionData* pDataArray, bool visible)
    {
        if (!visible || pDataArray == null)
        {
            this.Visible = false;
            return;
        }
        this.Visible = true;
        //var partyMember = mainGroup->PartyMembers[PartyListIndex];
        for (int i = 0; i < Actions.Length; i++)
        {
            HotbarActionData* pData = pDataArray + i;
            if (pData->Type == 3)
            {
                this.actionButtons[i].ChargeNum = (ushort)pData->ChargeNum;
                if (pData->RecastPercent == 0)
                {
                    this.actionButtons[i].RecastPercent = 0;
                    this.actionButtons[i].ChargePercent = (ushort)pData->ChargePercent;
                }
                else
                {
                    this.actionButtons[i].RecastPercent = (ushort)pData->RecastPercent;
                }
                this.actionButtons[i].Enabled = !(pData->ChargeNum == 0);
            }
            else
            {
                this.actionButtons[i].RecastPercent = (ushort)pData->RecastPercent;
                this.actionButtons[i].ChargePercent = 100;
            }
            this.actionButtons[i].RecastTime = (ushort)pData->RecastTimeSeconds;
            this.actionButtons[i].Enabled = pData->IsEnabled;
            actionButtons[i].Node->DrawFlags |= 1;
        }
    }

    public bool Attached { get; private set; } = false;
    public void AttachNode(AtkResNode* attachTargetNode, NodePosition position = NodePosition.AsLastChild)
    {
        NodeLinker.AttachNode((AtkResNode*)Node, attachTargetNode, position);
        Attached = true;
    }
    private AtkComponentNode* AttachedComponentNode;
    public void AttachNode(AtkComponentNode* attachTargetNode, NodePosition position = NodePosition.AsLastChild)
    {
        NodeLinker.AttachNode((AtkResNode*)Node, attachTargetNode->Component->UldManager.RootNode, NodePosition.AfterAllSiblings);
        AttachedComponentNode = attachTargetNode;
        Attached = true;
    }
    public AtkUnitBase* Addon { get; private set; }
    public void BindEvents(AtkUnitBase* addon)
    {
        this.Addon = addon;
        foreach (var item in actionButtons)
        {
            item.BindEvents(addon);
        }
    }

    internal void DetachNode()
    {
        NodeLinker.DetachNode((AtkResNode*)Node);
        if (AttachedComponentNode != null)
        {
            AttachedComponentNode->Component->UldManager.UpdateDrawNodeList();
        }
        Attached = false;
    }

    public void Dispose()
    {
        if (Attached)
        {
            //foreach (var item in actionButtons)
            //{
            //    item.Dispose(false);
            //}
            DetachNode();
        }
    }
}
