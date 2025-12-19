using Dalamud.Game.ClientState.Fates;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using InteropGenerator.Runtime;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static PartyHotbar.Windows.ConfigWindow;
using Action = Lumina.Excel.Sheets.Action;
using ActionManagerNative = FFXIVClientStructs.FFXIV.Client.Game.ActionManager;
namespace PartyHotbar;

[StructLayout(LayoutKind.Explicit, Size = 0x48)]
public unsafe struct HotbarActionData
{
    // 3 Charge Skill
    // 0 Non charge
    [FieldOffset(0x1C)] public uint Type;
    [FieldOffset(0x20)] public uint RecastTimeSeconds;
    [FieldOffset(0x24)] public uint RecastPercent;
    [FieldOffset(0x2C)] public uint ChargePercent;
    [FieldOffset(0x34)] public uint ChargeNum;
    [FieldOffset(0x3E)] public bool IsEnabled;
}
internal unsafe class ActionManager
{
    private ExcelSheet<Action> actionSheet = null!;
    private ExcelSheet<ClassJob> classjobSheet = null!;
    private Dictionary<uint, List<Action>> jobActions = new();
    private List<ClassJob> classJobs = null!;
    private bool initialized = false;
    public ActionManagerNative* Manager = null!;
    public IEnumerable<ClassJob> GetClassJobs() => initialized ? classJobs : new List<ClassJob>();
    public Action GetAction(uint id) => actionSheet.GetRow(id);
    public ClassJob GetClassJob(uint id) => classjobSheet.GetRow(id);
    private delegate uint canCastDelegate(ActionManagerNative* ActionManager, ActionType actionType, uint id);
    private canCastDelegate canCast = null!;

    //private delegate void getActionRecastDataDelegate(ActionManagerNative* ActionManager, ActionRecastData* actionRecastData, ActionType actionType, uint id);
    private delegate int getHotbarActionRecastDataDelegate(RaptureHotbarModule.HotbarSlot* hotbarSlot, HotbarActionData* data);
    private getHotbarActionRecastDataDelegate getHotbarActionRecastData;
    private Plugin plugin = null!;

    public ActionManager(Plugin plugin)
    {
        this.plugin = plugin;
        this.Initialize();
        this.Manager = ActionManagerNative.Instance();
        this.canCast = Marshal.GetDelegateForFunctionPointer<canCastDelegate>(Service.SigScanner.ScanText("48 83 EC 48 48 C7 44 24 ?? ?? ?? ?? ?? 41 B9"));
        //this.getActionRecastData = Marshal.GetDelegateForFunctionPointer<getActionRecastDataDelegate>(Service.SigScanner.ScanText("E8 ?? ?? ?? ?? 8B 4C 24 50 89 4F 1C "));
        //this.getActionRecastData = Marshal.GetDelegateForFunctionPointer<getActionRecastDataDelegate>(Service.SigScanner.ScanText("E8 ?? ?? ?? ?? 0F B6 44 24 ?? 44 8B C6 F3 0F 10 44 24 "));
        this.getHotbarActionRecastData = Marshal.GetDelegateForFunctionPointer<getHotbarActionRecastDataDelegate>(Service.SigScanner.ScanText("48 89 74 24 10 57 48 83 EC 60 0F 28 05"));
        //Task.Factory.StartNew(this.Initialize);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool CanCast(ActionType actionType, uint id)
    {
        return this.canCast(this.Manager, actionType, id) == 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool GetActionInRangeOrLoS(uint actionId, GameObject* sourceObject, GameObject* targetObject) => ActionManagerNative.GetActionInRangeOrLoS(actionId, sourceObject, targetObject) == 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool GetActionInRangeOrLoS(uint actionId, GameObject* targetObject)
    {
        var localPlayer = Service.ClientState.LocalPlayer;
        if (localPlayer == null)
        {
            return false;
        }
        return GetActionInRangeOrLoS(actionId, (GameObject*)localPlayer.Address, targetObject);
    }
    [StructLayout(LayoutKind.Explicit, Size = 224)]
    public struct HotbarSlotExt
    {
        [FieldOffset(0xC9)] public byte type;
    }
    public int GetActionRecastData(uint id, HotbarActionData* data)
    {
        var hotbar = (RaptureHotbarModule.HotbarSlot*)Marshal.AllocHGlobal(sizeof(RaptureHotbarModule.HotbarSlot));
        Unsafe.InitBlock(hotbar, 0, (uint)sizeof(RaptureHotbarModule.HotbarSlot));
        ((HotbarSlotExt*)hotbar)->type = 1;
        hotbar->ApparentActionId = id;
        var ret = this.getHotbarActionRecastData(hotbar, data);
        Marshal.FreeHGlobal((nint)hotbar);
        return ret;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetActionRecastData(RaptureHotbarModule.HotbarSlot* hotbar, HotbarActionData* data)
    {
        var ret = this.getHotbarActionRecastData(hotbar, data);
        return ret;
    }

    public IEnumerable<Action> GetJobActions(uint jobId)
    {
        if (!initialized)
        {
            return Enumerable.Empty<Action>();
        }
        if (jobActions.TryGetValue(jobId, out var actions))
        {
            return actions;
        }
        else
        {
            return Enumerable.Empty<Action>();
        }

    }
    private void Initialize()
    {
        this.actionSheet = Service.DataManager.GetExcelSheet<Action>();
        this.classjobSheet = Service.DataManager.GetExcelSheet<ClassJob>();

        this.classJobs = classjobSheet.Where(x => x.JobIndex > 0).OrderBy(x => x.Role).ThenBy(x => x.JobIndex).ToList();
        var classJobCategorySheet = Service.DataManager.GetExcelSheet<RawRow>(name: "ClassJobCategory");
        this.classJobs.ForEach(x =>
        {
            this.jobActions[x.RowId] = new List<Action>();
        });

        foreach (var job in classJobs)
        {
            var jobId = job.RowId;
            this.jobActions[jobId] = actionSheet.Where(a =>
            {

                if (!a.CanTargetParty || a.IsPvP || !a.IsPlayerAction)
                {
                    return false;
                }

                var id = a.ClassJobCategory.RowId;
                var jobCategory = classJobCategorySheet.GetRow(id);
                return jobCategory.ReadBoolColumn((int)jobId + 1);

            }).ToList();
        }
        initialized = true;
        Service.PluginLog.Info("Aciton data load completed");
    }
}
