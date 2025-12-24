using Dalamud.Bindings.ImGui;
using Dalamud.Game.Addon.Events;
using Dalamud.Game.Addon.Events.EventDataTypes;
using Dalamud.Interface.FontIdentifier;
using FFXIVClientStructs.FFXIV.Client.Graphics;
using FFXIVClientStructs.FFXIV.Common.Lua;
using FFXIVClientStructs.FFXIV.Common.Math;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static FFXIVClientStructs.FFXIV.Client.Game.Fate.FateManager.Delegates;
using static Lumina.Data.Parsing.Uld.NodeData;
using FontType = FFXIVClientStructs.FFXIV.Component.GUI.FontType;

namespace PartyHotbar.Node.Component
{
    internal unsafe class DragDrop : Base
    {
        public uint IconId
        {
            get => ComponentIcon->IconId;
            set => LoadIcon(value);
        }

        private readonly AtkImageNode* ChargeIndicatorNode;
        public bool Chargeable
        {
            get => field;
            set
            {
                if (value != field)
                {
                    field = value;
                    ChargeIndicatorNode->ToggleVisibility(value);
                }
            }
        }
        public ushort ChargeNum
        {
            get => ChargeIndicatorNode->PartId;
            set => ChargeIndicatorNode->PartId = value;
        }

        //private readonly AtkResNode* ComboFrameNode;
        public bool InCombo
        {
            get => field;
            set
            {
                if (value != field)
                {
                    field = value;
                    setCombo(Component->AtkComponentIcon, value, true);
                }
            }
        }
        private readonly AtkResNode* StateNode;

        public bool Enabled
        {
            get => field;
            set
            {
                field = value;
                Component->SetIconDisableState(value);
                Component->SetEnabledState(value);
            }
        }

        public bool CanCast
        {
            get => field;
            set
            {
                if (value != field)
                {
                    field = value;
                    StateNode->Timeline->PlayAnimation(AtkTimelineJumpBehavior.PlayOnce, (ushort)(value ? 19 : 22));
                }
            }
        }

        private readonly AtkResNode* ChargeNode;
        public ushort ChargePercent
        {
            get => field;
            set
            {
                if (value != field)
                {
                    field = value;
                    SetCharge(value);
                }
            }
        }

        private AtkTextNode* RecastTextNode;

        public uint RecastTime
        {
            get => field;
            set
            {
                if (field == value)
                    return;
                field = value;
                if (value == 0)
                {
                    RecastTextNode->ToggleVisibility(false);
                    return;
                }
                RecastTextNode->SetNumber((int)value);
            }
        }

        public ushort RecastPercent
        {
            get => field;
            set
            {
                if (field == value)
                    return;
                field = value;
                SetRecast(value, InCombo);
            }
        }

        private AtkComponentIcon* ComponentIcon;
        public readonly AtkComponentDragDrop* Component;

        private delegate void DragDropComponentPlayAnimationDelegate(AtkComponentDragDrop* comp, uint labelId);
        private DragDropComponentPlayAnimationDelegate dragDropComponentPlayAnimation = Marshal.GetDelegateForFunctionPointer<DragDropComponentPlayAnimationDelegate>(Service.SigScanner.ScanText("E8 ?? ?? ?? ?? 83 EB 01 74 7E"));

        private delegate void SetComboDelegate(AtkComponentIcon* comp, bool isCombo, bool force);
        private SetComboDelegate setCombo = Marshal.GetDelegateForFunctionPointer<SetComboDelegate>(Service.SigScanner.ScanText("48 83 EC 38 45 84 C0 44 0F B6 CA"));

        private delegate void SetFrameDelegate(AtkResNode* node, int frameId);
        private static SetFrameDelegate setFrame = Marshal.GetDelegateForFunctionPointer<SetFrameDelegate>(Service.SigScanner.ScanText("E8 ?? ?? ?? ?? 89 6B 30 "));

        private delegate uint getFrameByLabelIdDelegate(AtkResNode* node, ushort labelId);
        private static getFrameByLabelIdDelegate getFrameByLabelId = Marshal.GetDelegateForFunctionPointer<getFrameByLabelIdDelegate>(Service.SigScanner.ScanText("44 0F B7 CA 48 85 C9 74 60"));
        public unsafe DragDrop() : base("ui/uld/ActionBarCustom.uld", 1002, ComponentType.DragDrop)
        {
            Node->NodeFlags = NodeFlags.Visible | NodeFlags.Enabled | NodeFlags.EmitsEvents;
            Node->Type = NodeType.Component;
            Component = (AtkComponentDragDrop*)Node->Component;
            Component->AtkComponentIcon = Component->UldManager.SearchNodeById(2)->GetAsAtkComponentIcon();
            Component->Flags = DragDropFlag.Locked;
            Component->AtkDragDropInterface.DragDropType = DragDropType.ActionBar_Action;
            Component->AtkDragDropInterface.ActiveNode = Component->UldManager.SearchNodeById(4);
            Component->AtkDragDropInterface.ComponentNode = Component->UldManager.SearchNodeById(2)->GetAsAtkComponentNode();

            ComponentIcon = Component->AtkComponentIcon;
            ChargeIndicatorNode = ComponentIcon->UldManager.SearchNodeById(10)->GetAsAtkImageNode();
            StateNode = ComponentIcon->UldManager.SearchNodeById(16);
            ChargeNode = ComponentIcon->UldManager.SearchNodeById(14);

            RecastTextNode = ComponentIcon->UldManager.SearchNodeById(8)->GetAsAtkTextNode();
            RecastTextNode->Transform = new Matrix2x2 { M11 = 1.2f, M12 = 0, M21 = 1.2f, M22 = 0 };
            RecastTextNode->X = 4;
            RecastTextNode->Y = 7;
            RecastTextNode->Width = 40;
            RecastTextNode->Height = 35;
            RecastTextNode->FontSize = 24;
            RecastTextNode->EdgeColor = new ByteColor { R = 0x00, G = 0x00, B = 0x00, A = 0xFF };
            RecastTextNode->AlignmentType = AlignmentType.Center;
            RecastTextNode->FontType = FontType.TrumpGothic;

            Node->Width = 44;
            Node->Height = 44;

            this.Node->Priority = 0;
            Node->DrawFlags = 0x8;
            Visible = false;

            CollisionNode = Component->UldManager.SearchNodeById(4)->GetAsAtkCollisionNode();
            CollisionNode->NodeFlags = NodeFlags.Visible | NodeFlags.Enabled | NodeFlags.HasCollision | NodeFlags.RespondToMouse | NodeFlags.Focusable | NodeFlags.EmitsEvents | NodeFlags.Fill;
            CollisionNode->DrawFlags = 0x8;
            CollisionNode->ParentNode = (AtkResNode*)Node;
            CollisionNode->LinkedComponent = (AtkComponentBase*)Component;
            CollisionNode->DrawFlags |= (1 << 21);

            Component->AtkDragDropInterface.DragDropType = DragDropType.Nothing;
            Component->AtkDragDropInterface.DragDropReferenceIndex = 0;
            var data = (AtkUldComponentDataDragDrop*)Component->UldManager.ComponentData;
            data->Nodes[0] = 2;

            Component->Flags |= DragDropFlag.Clickable;
            var uldManager = &this.Component->UldManager;

            ((AtkUldComponentInfo*)uldManager->Objects)->ComponentType = ComponentType.DragDrop;

            Component->InitializeFromComponentData(Component->UldManager.ComponentData);
            Component->Setup();
            Component->SetEnabledState(true);

            //Events[AddonEventType.MouseOver] = ProcessEvents;
            //Events[AddonEventType.MouseOut] = ProcessEvents;
            //Events[AddonEventType.MouseDown] = ProcessEvents;
            //Events[AddonEventType.MouseUp] = ProcessEvents;
            //Events[AddonEventType.MouseClick] = ProcessEvents;
        }
        private void LoadIcon(uint iconId)
        {
            Component->LoadIcon(iconId);
            ComponentIcon->IconImage->MultiplyBlue = 100;
            ComponentIcon->IconImage->MultiplyRed = 100;
            ComponentIcon->IconImage->MultiplyGreen = 100;
        }
        private void SetRecast(ushort percent, bool isCombo)
        {
            // the native function in game is missing because of inline.
            setCombo(ComponentIcon, isCombo, false);
            if ((uint)percent - 1 < 98)
            {
                ComponentIcon->SetCooldownProgress(percent * 0.0099999998f);
                return;
            }

            if (percent == 100)
            {
                StateNode->Timeline->PlayAnimation(AtkTimelineJumpBehavior.LoopForever, (ushort)25);
            }
            else
            {
                StateNode->Timeline->PlayAnimation(AtkTimelineJumpBehavior.Start, (ushort)19);
            }
        }
        private void SetCharge(ushort percent)
        {
            if ((uint)percent - 1 < 98)
            {
                var frame = getFrameByLabelId(ChargeNode, 102);
                setFrame(ChargeNode, (int)((int)(percent * 0.0099999998f * 81) + frame));
                return;
            }
            ChargeNode->Timeline->PlayAnimation(AtkTimelineJumpBehavior.Initialize, (ushort)17);
        }

        public void Reset()
        {
            ChargeNode->Timeline->PlayAnimation(AtkTimelineJumpBehavior.Initialize, (ushort)17);
            StateNode->Timeline->PlayAnimation(AtkTimelineJumpBehavior.Start, (ushort)19);
        }

        private void ProcessEvents(AddonEventType atkEventType, Base senderComponent, AddonEventData data)
        {
            if (!this.Visible)
            {
                return;
            }
            switch (atkEventType)
            {
                case AddonEventType.MouseClick:
                    this.OnClick?.Invoke(AddonEventType.MouseClick, senderComponent, data);
                    break;
                case AddonEventType.MouseOut:
                    Service.AddonEventManager.ResetCursor();
                    //Node->Timeline->PlayAnimation(AtkTimelineJumpBehavior.LoopForever, 4);
                    dragDropComponentPlayAnimation(Component, 4);
                    break;
                case AddonEventType.MouseOver:
                    Service.AddonEventManager.SetCursor(AddonCursorType.Clickable);
                    dragDropComponentPlayAnimation(Component, 2);
                    break;
                case AddonEventType.MouseDown:
                    dragDropComponentPlayAnimation(Component, 3);
                    break;
                case AddonEventType.MouseUp:
                    dragDropComponentPlayAnimation(Component, 6);
                    break;
                default:
                    Log.Information("Unhandled event type: {EventType}", atkEventType);
                    return;
            }
        }
    }
}
