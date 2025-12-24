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
using KamiToolKit.Nodes;
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

internal unsafe class Hotbar : ComponentNode<AtkComponentBase, AtkUldComponentInfo>
{
    public static readonly uint MaxActionCount = 4; 
    private List<DragDrop> actionButtons { get; set; } = [];
    public readonly int PartyListIndex;
    private readonly ActionManager actionManager;
    public Hotbar(ActionManager actionManager, int partIndex)
    {
        SetInternalComponentType(ComponentType.Base);
        var resNode = new ResNode();
        resNode.AttachNode(this);
        for (var i = 0; i < MaxActionCount; i++)
        {
            var button = new DragDrop() { NodeId = (uint)i };
            actionButtons.Add(button);
            button.AttachNode(resNode);
            //button.Visible = true;
            var index = i;
            button.OnClicked = (_) => OnClick(index);
        }
        this.actionManager = actionManager;
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
            actionButtons[i].IsVisible = true;
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
            actionButtons[i].IsVisible = false;
        }
    }

    private void OnClick(int index)
    {
        var partyListNumberArray = PartyListNumberArray.Instance();
        var target = partyListNumberArray->PartyMembers[PartyListIndex];
        Service.PluginLog.Info($"Clicked {Actions[index].Name.ToString()} {target.ContentId:X} {PartyListIndex} -> {target.ContentId:X}");
        actionManager.Manager->UseAction(ActionType.Action, Actions[index].RowId, (ulong)target.ContentId);
    }

    public void Update(in HotbarActionData* pDataArray, bool visible)
    {
        if (!visible || pDataArray == null)
        {
            this.IsVisible = false;
            return;
        }
        this.IsVisible = true;
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
    //public void AttachNode(AtkResNode* attachTargetNode, NodePosition position = NodePosition.AsLastChild)
    //{
    //    NodeLinker.AttachNode((AtkResNode*)Node, attachTargetNode, position);
    //    Attached = true;
    //}
    private AtkComponentNode* AttachedComponentNode;
    //public void AttachNode(AtkComponentNode* attachTargetNode, NodePosition position = NodePosition.AsLastChild)
    //{
    //    NodeLinker.AttachNode((AtkResNode*)Node, attachTargetNode->Component->UldManager.RootNode, NodePosition.AfterAllSiblings);
    //    AttachedComponentNode = attachTargetNode;
    //    Attached = true;
    //}
    public AtkUnitBase* Addon { get; private set; }

    //internal void DetachNode()
    //{
    //    NodeLinker.DetachNode((AtkResNode*)Node);
    //    if (AttachedComponentNode != null)
    //    {
    //        AttachedComponentNode->Component->UldManager.UpdateDrawNodeList();
    //    }
    //    Attached = false;
    //}

    //public void Dispose()
    //{
    //    if (Attached)
    //    {
    //        //foreach (var item in actionButtons)
    //        //{
    //        //    item.Dispose(false);
    //        //}
    //        DetachNode();
    //    }
    //}
}
