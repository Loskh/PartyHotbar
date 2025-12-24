using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Arrays;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using static FFXIVClientStructs.FFXIV.Client.UI.Arrays.PartyListNumberArray;
using static FFXIVClientStructs.FFXIV.Client.UI.Misc.RaptureHotbarModule;
using static PartyHotbar.ActionManager;
using Action = Lumina.Excel.Sheets.Action;
namespace PartyHotbar.Node;

internal unsafe class PartyHotbars : IDisposable
{
    private readonly Configuration config;
    public readonly ActionManager actionManager;
    private readonly Hotbar[] Hotbars;
    private Action[] actions;
    private HotbarSlot[] hotbarSlots;
    private HotbarActionData[] hotbarActionsData;
    public bool TestMode = false;
    public PartyHotbars(ActionManager actionManager, Configuration configuration)
    {
        this.actionManager = actionManager;
        this.config = configuration;
        this.Hotbars = new Hotbar[8];
        this.actions = new Action[4];
        Array.Fill(this.actions, actionManager.GetAction(0));
        this.hotbarSlots = Array.Empty<HotbarSlot>();
        this.hotbarActionsData = Array.Empty<HotbarActionData>();

        // if hotbars are attached in predraw ,the game crashes when you exit a duty in a cross realm party.
        //Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "_PartyList", AttachHotBars);
        //Service.AddonLifecycle.RegisterListener(AddonEvent.PreDraw, "_PartyList", AttachHotBars);
        //Service.AddonLifecycle.RegisterListener(AddonEvent.PreDraw, "_PartyList", PreDraw);
        //Service.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, "_PartyList", PreFinalize);

        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "ConfigSystem", AttachTest);
    }

    private void AttachTest(AddonEvent type, AddonArgs args)
    {
        this.Hotbars[0] = new Hotbar(actionManager, 0);
        var addon = (AtkUnitBase*)args.Addon.Address;
        this.Hotbars[0].AttachNode(addon->RootNode, NodePosition.AsLastChild);
        this.Hotbars[0].SetHotbarActions(this.actions, 50, (uint)config.XPitch, config.Scale);
        //this.Hotbars[0].BindEvents((AtkUnitBase*)addon);
        addon->UpdateCollisionNodeList(false);
        addon->UldManager.UpdateDrawNodeList();
    }
    private void AttachHotBars(AddonEvent type, AddonArgs args)
    {
        if (this.Attached)
        {
            return; 
        }

        var addon = (AddonPartyList*)args.Addon.Address;

        for (var i = 0; i < 8; i++)
        {
            this.Hotbars[i] = new Hotbar(actionManager, i);
            ref var partyMember = ref addon->PartyMembers[i];
            var partyMemberNode = partyMember.PartyMemberComponent->OwnerNode->GetAsAtkComponentNode();
            this.Hotbars[i].AttachNode(partyMemberNode, NodePosition.AfterAllSiblings);
            Service.PluginLog.Info($"Attached {i}");
        }

        for (var i = 0; i < 8; i++)
        {
            var partyMember = addon->PartyMembers[i];
            var anchorNode = partyMember.ClassJobIcon;
            Service.PluginLog.Info($"anchorNode {(nint)anchorNode:X}");
            this.Hotbars[i].SetHotbarActions(this.actions, (anchorNode->Y + anchorNode->Y + anchorNode->Height) / 2, (uint)config.XPitch, config.Scale);
            this.Hotbars[i].Node->X = (anchorNode->X - this.Hotbars[i].Node->Width) * config.Scale - config.XOffset;
            //this.Hotbars[i].Node->DrawFlags = 8 | 1;

            Service.PluginLog.Info($"Rebind Events {i}");
            this.Hotbars[i].BindEvents((AtkUnitBase*)addon);
            partyMember.PartyMemberComponent->UldManager.UpdateDrawNodeList();

        }

        Service.PluginLog.Info("Update Collision");
        var collisionNode = addon->UldManager.SearchNodeById(22);
        collisionNode->Width = (ushort)(this.addonInitWidth + (this.Hotbars[0].Node->Width) * config.Scale);
        collisionNode->X = -(this.Hotbars[0].Node->Width) * config.Scale;
        collisionNode->DrawFlags |= 1;
        addon->UpdateCollisionNodeList(false);
        addon->UldManager.UpdateDrawNodeList();
        this.Attached = true;
    }

    private uint currentJobId = 0;

    private unsafe void ClassJobChanged(uint classJobId)
    {
        Service.PluginLog.Debug($"ClassJobChanged to {classJobId}");
        this.currentJobId = classJobId;
        if (config.JobActions.TryGetValue(classJobId, out var actions))
        {
            var actionIds = actions.Select(x => x.ID).ToArray();
            Service.PluginLog.Debug($"actionIds to {actionIds.Length}");
            this.actions = actionIds.Select(x => this.actionManager.GetAction(x)).ToArray();
            this.hotbarSlots = new HotbarSlot[this.actions.Length];
            this.hotbarActionsData = new HotbarActionData[this.actions.Length];
        }
        else
        {
            this.actions = Array.Empty<Action>();
            this.hotbarSlots = Array.Empty<HotbarSlot>();
            this.hotbarActionsData = Array.Empty<HotbarActionData>();
        }
        fixed (HotbarSlot* pSlots = hotbarSlots)
        {
            for (int i = 0; i < this.actions.Length; i++)
            {
                HotbarSlot* p = pSlots + i;
                var newAction = this.actionManager.GetAction(this.actionManager.Manager->GetAdjustedActionId(this.actions[i].RowId));
                if (newAction.RowId != this.actions[i].RowId)
                {
                    Service.PluginLog.Debug($"Replace {this.actions[i].Name} to {newAction}");
                    this.actions[i] = newAction;
                }

                p->ApparentActionId = this.actions[i].RowId;
                ((HotbarSlotExt*)p)->type = 1;
            }
        }
    }


    private void PreDraw(AddonEvent type, AddonArgs args)
    {
        if (!this.Attached)
            return;
        var addon = (AddonPartyList*)args.Addon.Address;
        if (addon == null)
            return;
        if (!addon->IsVisible)
            return;

        if (Service.ClientState.LocalPlayer == null)
        {
            return;
        }

        var needUpdateHotbars = false;
        var classJobId = Service.ClientState.LocalPlayer.ClassJob.Value.RowId;
        if (classJobId != this.currentJobId | this.TestMode)
        {
            ClassJobChanged(classJobId);
            needUpdateHotbars = true;
        }
        needUpdateHotbars |= this.TestMode;
        if (needUpdateHotbars)
        {
            RefreshParyList(addon);
        }
        UpdateActionState();
    }

    private void UpdateActionState()
    {
        fixed (HotbarSlot* pSlotArray = hotbarSlots)
        fixed (HotbarActionData* pDataArray = hotbarActionsData)
        {
            for (int i = 0; i < actions.Length; i++)
            {
                var action = actions[i];
                HotbarSlot* pSlot = pSlotArray + i;
                HotbarActionData* pData = pDataArray + i;
                pSlot->ApparentActionId = this.actionManager.Manager->GetAdjustedActionId(pSlot->ApparentActionId);
                var recast = this.actionManager.GetActionRecastData(pSlot, pData);
                pData->IsEnabled = this.actionManager.CanCast(FFXIVClientStructs.FFXIV.Client.Game.ActionType.Action, pSlot->ApparentActionId);
            }
            var partyListNumberArray = PartyListNumberArray.Instance();
            for (int i = 0; i < partyListNumberArray->PartyListCount; i++)
            {
                //var member = partyListNumberArray->PartyMembers[i];
                var pMember = (PartyListMemberNumberArray*)Unsafe.AsPointer(ref partyListNumberArray->PartyMembers[i]);
                var visible = pMember->Targetable;
                //uint* contentIdPtr = (uint*)(pMember + 164);
                var objectId = (uint)pMember->ContentId;
                visible = !(objectId == 0xE000_0000 && objectId == 0);
                if (this.config.HideSelf)
                {
                    if (objectId == (int)Service.ClientState.LocalPlayer!.GameObjectId)
                    {
                        visible = false;
                    }
                }
                //continue;
                this.Hotbars[i].Update(pDataArray, visible);
            }
        }
    }

    public bool Attached { get; private set; } = false;
    private uint addonInitWidth = 500;

    private void RefreshParyList(AddonPartyList* addon)
    {
        var needResizeCollision = (actions.Length != this.Hotbars[0].Actions.Length);
        for (var i = 0; i < 8; i++)
        { 
            var partyMember = addon->PartyMembers[i];
            var anchorNode = partyMember.ClassJobIcon;
            //Service.PluginLog.Info($"anchorNode {(nint)anchorNode:X}");
            this.Hotbars[i].SetHotbarActions(this.actions, (anchorNode->Y + anchorNode->Y + anchorNode->Height) / 2, (uint)config.XPitch, config.Scale);
            this.Hotbars[i].Node->X = (anchorNode->X - this.Hotbars[i].Node->Width) * config.Scale - config.XOffset;
            this.Hotbars[i].Node->DrawFlags |= 1;
        }
        if (needResizeCollision)
        {
            var collisionNode = addon->UldManager.SearchNodeById(22);
            collisionNode->Width = (ushort)(this.addonInitWidth + (this.Hotbars[0].Node->Width) * config.Scale);
            collisionNode->X = -(this.Hotbars[0].Node->Width) * config.Scale;
            collisionNode->DrawFlags |= 1;
        }
    }
    private void PreFinalize(AddonEvent type, AddonArgs args)
    {
        Attached = false;
        Service.PluginLog.Info("PreFinalize");
    }

    public void Deattach()
    {
        if (!Attached)
            return;
        for (var i = 0; i < 8; i++)
        {
            this.Hotbars[i]?.Dispose();
            Service.PluginLog.Info($"Hotbar {i} is diposed");
        }
        var addon = (AddonPartyList*)Service.GameGui.GetAddonByName("_PartyList").Address;
        if (addon != null)
        {
            var collisionNode = addon->UldManager.SearchNodeById(22);
            collisionNode->DrawFlags |= 1;
            addon->UpdateCollisionNodeList(false);
            addon->UldManager.UpdateDrawNodeList();
        }
        Attached = false;
    }

    public void Dispose()
    {
        Service.AddonLifecycle.UnregisterListener(AddonEvent.PreDraw, "_PartyList", PreDraw);
        if (Attached)
        {
            Service.Framework.RunOnFrameworkThread(() => Deattach());
        }
        Service.AddonLifecycle.UnregisterListener(AddonEvent.PreFinalize, "_PartyList", PreFinalize);
    }
}
