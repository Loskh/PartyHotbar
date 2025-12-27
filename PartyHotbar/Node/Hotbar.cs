using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Arrays;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Nodes;
using PartyHotbar.Node.Component;
using System.Collections.Generic;
using System.Numerics;
using Action = Lumina.Excel.Sheets.Action;
namespace PartyHotbar.Node;

internal unsafe class Hotbar : ComponentNode<AtkComponentBase, AtkUldComponentInfo>
{
    //public static readonly uint MaxActionCount = 4;
    public const uint ButtonSize = 40;
    private List<DragDrop> actionButtons { get; set; } = [];
    public readonly int PartyListIndex;
    private readonly ActionManager actionManager;
    private ResNode resNode = null!;
    public Hotbar(ActionManager actionManager, int partIndex)
    {
        SetInternalComponentType(ComponentType.Base);
        this.resNode = new ResNode();
        //for (var i = 0; i < MaxActionCount; i++)
        //{
        //    var button = new DragDrop() { NodeId = (uint)i };
        //    actionButtons.Add(button);
        //    button.AttachNode(resNode);
        //    Service.PluginLog.Info($"Created button {i} for hotbar {partIndex}");
        //    //button.Visible = true;
        //    var index = i;
        //    button.OnClicked = (_) => OnClick(index);
        //}
        resNode.AttachNode(this);
        this.Node->Height = 44;
        resNode.Height = 44;
        this.PartyListIndex = partIndex;
        this.actionManager = actionManager;
    }

    public Vector2 CenterPosition = new Vector2(0, 0);
    public Action[] Actions { get; private set; } = new Action[0];

    public void SetHotbarActions(Action[] actions, uint xSpace, float scale, bool alignLeft)
    {
        this.Actions = actions;
        var newWidth = (ushort)((actions.Length * ButtonSize) * scale + xSpace * (actions.Length - 1));
        var direction = alignLeft ? 1 : -1;
        for (var i = 0; i < actions.Length; i++)
        {
            if (actionButtons.Count <= i)
            {
                var button = new DragDrop() { NodeId = (uint)i };
                actionButtons.Add(button);
                button.AttachNode(resNode);
                var index = i;
                button.OnClicked = (_) => OnClick(index);
            }
            actionButtons[i].IsVisible = true;
            actionButtons[i].IconId = actions[i].Icon;
            actionButtons[i].X = (xSpace + ButtonSize * scale) * i;
            if (!alignLeft)
            {
                actionButtons[i].X = newWidth - actionButtons[i].X - ButtonSize * scale;
            }
            actionButtons[i].ChargeNum = actions[i].MaxCharges;
            actionButtons[i].Chargeable = actions[i].MaxCharges != 0;
            actionButtons[i].RecastPercent = 0;
            actionButtons[i].ChargePercent = 0;
            actionButtons[i].Reset();
            actionButtons[i].Node->DrawFlags |= 1;
            actionButtons[i].Node->SetScale(scale, scale);
        }
        for (var i = actions.Length; i < actionButtons.Count; i++)
        {
            actionButtons[i].DetachNode();
            actionButtons.RemoveAt(i);
        }
        this.Node->Width = newWidth;
        this.Node->Height = (ushort)(ButtonSize * scale);
        this.resNode.Width = this.Node->Width;
        this.resNode.Height = this.Node->Height;
        //this.CollisionNode.Size = this.resNode.Size;
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
