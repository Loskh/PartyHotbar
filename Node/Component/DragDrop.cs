using Dalamud.Game.Addon.Events;
using Dalamud.Interface.FontIdentifier;
using FFXIVClientStructs.FFXIV.Client.Graphics;
using FFXIVClientStructs.FFXIV.Common.Lua;
using FFXIVClientStructs.FFXIV.Common.Math;
using FFXIVClientStructs.FFXIV.Component.GUI;
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
        public readonly AtkComponentDragDrop* Component;

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
                    StateNode->Timeline->PlayAnimation(AtkTimelineJumpBehavior.PlayOnce, (byte)(value ? 19 : 22));
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
        private delegate void DragDropComponentPlayAnimationDelegate(AtkComponentDragDrop* comp, uint labelId);
        private DragDropComponentPlayAnimationDelegate dragDropComponentPlayAnimation = Marshal.GetDelegateForFunctionPointer<DragDropComponentPlayAnimationDelegate>(Service.SigScanner.ScanText("E8 ?? ?? ?? ?? 83 EB 01 74 7E"));

        private delegate void SetComboDelegate(AtkComponentIcon* comp, bool isCombo, bool force);
        private SetComboDelegate setCombo = Marshal.GetDelegateForFunctionPointer<SetComboDelegate>(Service.SigScanner.ScanText("48 83 EC 38 45 84 C0 44 0F B6 CA"));

        private delegate void SetFrameDelegate(AtkResNode* node, int frameId);
        private static SetFrameDelegate setFrame = Marshal.GetDelegateForFunctionPointer<SetFrameDelegate>(Service.SigScanner.ScanText("E8 ?? ?? ?? ?? 89 6B 30 "));

        private delegate uint getFrameByLabelIdDelegate(AtkResNode* node, ushort labelId);
        private static getFrameByLabelIdDelegate getFrameByLabelId = Marshal.GetDelegateForFunctionPointer<getFrameByLabelIdDelegate>(Service.SigScanner.ScanText("E8 ?? ?? ?? ?? 66 3B F0 75 77  "));
        public unsafe DragDrop() : base("ui/uld/ActionBarCustom.uld", 1002, ComponentType.DragDrop)
        {
            Component = (AtkComponentDragDrop*)Node->Component;
            Component->AtkComponentIcon = Component->UldManager.SearchNodeById(2)->GetAsAtkComponentIcon();
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

            this.Node->Priority = 100;
            Node->DrawFlags |= 1;
            Visible = false;

            CollisionNode = Component->UldManager.SearchNodeById(4)->GetAsAtkCollisionNode();
            Events[AddonEventType.MouseOver] = MouseOver;
            Events[AddonEventType.MouseOut] = MouseOut;
            Events[AddonEventType.MouseDown] = (atkEventType, sender, data) => dragDropComponentPlayAnimation(Component, 3);
            Events[AddonEventType.MouseUp] = (atkEventType, sender, data) => dragDropComponentPlayAnimation(Component, 6);
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
                StateNode->Timeline->PlayAnimation(AtkTimelineJumpBehavior.LoopForever, 25);
            }
            else
            {
                StateNode->Timeline->PlayAnimation(AtkTimelineJumpBehavior.Start, 19);
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
            ChargeNode->Timeline->PlayAnimation(AtkTimelineJumpBehavior.Initialize, 17);
        }

        public void Reset()
        {
            ChargeNode->Timeline->PlayAnimation(AtkTimelineJumpBehavior.Initialize, 17);
            StateNode->Timeline->PlayAnimation(AtkTimelineJumpBehavior.Start, 19);
        }

        private void MouseOut(AddonEventType atkEventType, Base senderComponent, AddonEventData data)
        {
            Service.AddonEventManager.ResetCursor();
            //Node->Timeline->PlayAnimation(AtkTimelineJumpBehavior.LoopForever, 4);
            dragDropComponentPlayAnimation(Component, 4);
        }

        private void MouseOver(AddonEventType atkEventType, Base senderComponent, AddonEventData data)
        {
            Service.AddonEventManager.SetCursor(AddonCursorType.Clickable);
            dragDropComponentPlayAnimation(Component, 2);
        }
    }
}
