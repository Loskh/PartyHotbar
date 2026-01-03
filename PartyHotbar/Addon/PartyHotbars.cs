using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Arrays;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;
using static FFXIVClientStructs.FFXIV.Client.UI.Arrays.PartyListNumberArray;
using static FFXIVClientStructs.FFXIV.Client.UI.Misc.GroupPoseModule;
using static FFXIVClientStructs.FFXIV.Client.UI.Misc.RaptureHotbarModule;
using static PartyHotbar.ActionManager;
using Action = Lumina.Excel.Sheets.Action;
using Hotbar = PartyHotbar.Node.Hotbar;
namespace PartyHotbar.Addon;

internal unsafe class PartyHotbars : NativeAddon
{
    private const uint AddonInitWidth = 206;
    private const uint AddonInitHeight = 324;
    private const uint MaxPartyMemberCount = 8;
    private readonly Configuration config;
    public readonly ActionManager actionManager;
    private readonly Hotbar[] Hotbars;
    private Action[] actions;
    private HotbarSlot[] hotbarSlots;
    private HotbarActionData[] hotbarActionsData;
    private AddonPartyList* addonPartyList;
    private bool isOpen = false;
    public bool ForceUpdateHotbar = false;

    //public bool IsVisible
    //{
    //    get => this.RootNode.IsVisible;
    //    set => this.RootNode.IsVisible = value;
    //}
    public bool IsVisible = false;
    public PartyHotbars(ActionManager actionManager, Configuration configuration)
    {
        this.Size = new Vector2(AddonInitWidth, AddonInitHeight);
        this.IsOverlayAddon = true;
        this.actionManager = actionManager;
        this.config = configuration;
        this.Hotbars = new Hotbar[MaxPartyMemberCount];
        this.actions = new Action[0];
        Array.Fill(this.actions, actionManager.GetAction(0));
        this.hotbarSlots = Array.Empty<HotbarSlot>();
        this.hotbarActionsData = Array.Empty<HotbarActionData>();
        this.SetWindowSize(AddonInitWidth, AddonInitHeight);
        AttachToPartyList();
    }
    private List<AddonEvent> subscribedEvents = new() {
        AddonEvent.PreReceiveEvent,
        AddonEvent.PreMove,
        //AddonEvent.PreUpdate,
        AddonEvent.PreRequestedUpdate,
        AddonEvent.PreRefresh
    };
    private void AttachToPartyList()
    {
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostDraw, "_PartyList", OnPartyListDraw);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostHide, "_PartyList", OnPartyListHide);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostShow, "_PartyList", OnPartyListShow);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostMove, "_PartyList", OnPartyListMove);

        //foreach (var item in subscribedEvents)
        //{
        //    Service.AddonLifecycle.RegisterListener(item, "_PartyList", ShowEvents);
        //}
    }

    private void ShowEvents(AddonEvent type, AddonArgs args)
    {
        Service.PluginLog.Info($"{type}");
    }

    private bool needResize = false;
    private void OnPartyListMove(AddonEvent type, AddonArgs args)
    {
        needResize = true;
    }

    private void OnPartyListShow(AddonEvent type, AddonArgs args)
    {
        this.IsVisible = true;
    }

    private void OnPartyListHide(AddonEvent type, AddonArgs args)
    {
        this.IsVisible = false;
    }

    private void OnPartyListEvents(AddonEvent type, AddonArgs args)
    {
        Service.PluginLog.Info($"PartyList {type}");
    }

    private void DettachToPartyList()
    {
        Service.AddonLifecycle.UnregisterListener(AddonEvent.PostDraw, "_PartyList", OnPartyListDraw);
        Service.AddonLifecycle.UnregisterListener(AddonEvent.PostHide, "_PartyList", OnPartyListHide);
        Service.AddonLifecycle.UnregisterListener(AddonEvent.PostShow, "_PartyList", OnPartyListShow);
        Service.AddonLifecycle.UnregisterListener(AddonEvent.PostMove, "_PartyList", OnPartyListMove);
    }

    private void OnPartyListDraw(AddonEvent type, AddonArgs args)
    {
        this.addonPartyList = (AddonPartyList*)args.Addon.Address;
        if (!isOpen)
        {
            this.Open();
            isOpen = true;
            needResize = true;
            this.IsVisible = true;
        }
    }

    protected override unsafe void OnFinalize(AtkUnitBase* addon)
    {
        isOpen = false;
        base.OnFinalize(addon);
    }

    protected override void OnSetup(AtkUnitBase* addon)
    {
        this.SetWindowSize(AddonInitWidth, AddonInitHeight);
        for (var i = 0; i < MaxPartyMemberCount; i++)
        {
            this.Hotbars[i] = new Hotbar(actionManager, i);
            this.Hotbars[i].SetHotbarActions(this.actions, (uint)config.XSpace, 1, this.config.AlignLeft);
            this.Hotbars[i].Node->X = 0;
            this.Hotbars[i].Node->Y = 40 * i;
            this.Hotbars[i].AttachNode(this);
        }
    }

    protected override void OnDraw(AtkUnitBase* addon)
    {
        if (this.actionManager == null)
            return;
        if (addonPartyList == null)
            return;
        if (Service.PlayerState == null)
            return;
        if (this.IsVisible)
        {
            this.RootNode.IsVisible = true;
        }else
        {
            this.RootNode.IsVisible = false;
            return;
        }
        var classJobId = Service.PlayerState!.ClassJob.Value.RowId;
        if (classJobId != this.currentJobId || ForceUpdateHotbar)
        {
            ClassJobChanged(classJobId);
            this.needUpdateHotbar = true;
            this.needResize = true;
        }
        if (this.needUpdateHotbar)
        {
            UpdateHotbarActions();
            this.needUpdateHotbar = false;
            this.needResize = true;
        }
        UpdateActionState();
        this.needResize = true;
        if (this.needResize) {
            UpdateHotbarPosition(addon);
            needResize = false;
        }
    }

    private uint currentJobId = 0;
    private bool needUpdateHotbar = false;
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
                var pMember = (PartyListMemberNumberArray*)Unsafe.AsPointer(ref partyListNumberArray->PartyMembers[i]);
                var visible = pMember->Targetable;
                var objectId = (uint)pMember->ContentId;
                visible = !(objectId == 0xE000_0000 || objectId == 0);
                if (this.config.HideSelf)
                {
                    if (objectId == (int)Service.PlayerState!.EntityId)
                    {
                        visible = false;
                    }
                }
                this.Hotbars[i].Update(pDataArray, visible);
            }
            for (int i = partyListNumberArray->PartyListCount; i < MaxPartyMemberCount; i++)
            {
                this.Hotbars[i].Update(pDataArray, false);
            }
        }
    }

    private void UpdateHotbarPosition(AtkUnitBase* addon)
    {
        var scale = this.addonPartyList->Scale;
        if (addon->Scale != scale)
        {
            addon->Scale = scale;
            addon->RootNode->ScaleX = scale;
            addon->RootNode->ScaleY = scale;
        }
        var x = this.addonPartyList->X + this.config.XOffset;
        if (!this.config.AlignLeft)
        {
            x -= (int)(this.RootNode.Width * scale);
        }
        else
        {
            x += (int)this.addonPartyList->GetScaledWidth(true);
        }
        this.SetWindowPosition(new(x, this.addonPartyList->Y + 32 * scale));
        //this.RootNode.Size = this.Size;
        addon->RootNode->DrawFlags |= 0xD;
    }
    private void UpdateHotbarActions()
    {
        for (var i = 0; i < MaxPartyMemberCount; i++)
        {
            this.Hotbars[i].SetHotbarActions(this.actions, (uint)config.XSpace, config.Scale, this.config.AlignLeft);
            this.Hotbars[i].Node->Y = i * 40 + Hotbar.ButtonSize * (1 - config.Scale) / 2;
            this.Hotbars[i].Node->DrawFlags |= 1;
        }
        this.RootNode.Width = this.Hotbars[0].Width;
    }
    public override void Dispose()
    {
        //foreach (var item in subscribedEvents)
        //{
        //    Service.AddonLifecycle.UnregisterListener(item, "_PartyList", ShowEvents);
        //}

        this.IsVisible = false;
        Service.Framework.RunOnFrameworkThread(() =>
        {
            DettachToPartyList();
            Service.PluginLog.Info("Disposing PartyHotbars");
            for (var i = 0; i < MaxPartyMemberCount; i++)
            {
                Service.PluginLog.Info($"Disposing PartyHotbar[{i}]");
                //this.Hotbars[i]?.DetachNode();
                this.Hotbars[i]?.DetachNode();
            }
            base.Dispose();
        });
    }
}
