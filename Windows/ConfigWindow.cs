using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using PartyHotbar.ImGuiEx;
using PartyHotbar.Node;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Action = Lumina.Excel.Sheets.Action;
namespace PartyHotbar.Windows;

internal unsafe class ConfigWindow : Window, IDisposable
{

    private const string WindowTitle = "PartyHotbar Configuration";
    private Plugin plugin;
    private ActionManager actions { get; } = null!;
    private Configuration config { get; } = null!;
    public HotbarActionData* data;
    public ConfigWindow(Plugin plugin,ActionManager actions, Configuration config) : base(WindowTitle)
    {
        this.plugin = plugin;
        this.actions = actions;
        this.config = config;
    }
    public override void Draw()
    {
        if (!this.plugin.PartyHotbars.Attached)
        {
            //return;
            ImGui.Text("Not Attached");
            ImGui.Text("Back to title and re-login to attach");
        }
        if (ImGui.BeginTabBar("PartyHotbarTabs"))
        {
            if (ImGui.BeginTabItem("Layout"))
            {
                ImGui.InputInt("X Offset", ref this.config.XOffset);
                ImGui.InputInt("X Pitch", ref this.config.XPitch);
                ImGui.SliderFloat("Scale", ref this.config.Scale, 0.1f, 2);
                if (this.config.XPitch <= 0)
                {
                    this.config.XPitch = 0;
                }
                ImGui.Checkbox("Hide Self", ref this.config.HideSelf);
                if (ImGui.Button("Save"))
                {
                    this.config.Save();
                }
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Job Setting"))
            {
                if (ImGui.BeginTable($"Table", 2, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.Resizable))
                {
                    ImGui.TableSetupColumn("##SelectionColumn", ImGuiTableColumnFlags.WidthFixed, 200.0f * ImGuiHelpers.GlobalScale);
                    ImGui.TableSetupColumn("##ContentsColumn", ImGuiTableColumnFlags.WidthStretch);

                    ImGui.TableNextColumn();
                    this.DrawClassJobs();

                    ImGui.TableNextColumn();
                    this.DrawContents();
                    ImGui.EndTable();
                }
            }
        }
    }

    public override void OnOpen()
    {
        base.OnOpen();
        this.plugin.PartyHotbars.TestMode = true;
    }
    public override void OnClose()
    {
        base.OnClose();
        this.plugin.PartyHotbars.TestMode = false;
    }
    private unsafe void DrawContents()
    {
        if (currentClassJobId == 0)
            return;
        var contentRegion = ImGui.GetContentRegionAvail();
        ImGui.BeginChild($"{WindowTitle}_ContentsPane", ImGui.GetContentRegionAvail());

        var buttonWidth = ImGui.GetContentRegionAvail().X / 2;
        var buttonIndent = 0f;
        for (int i = 0; i < currentActions.Count; i++)
        {
            using var _ = ImGuiEx.ImGuiEx.IDBlock.Begin(i);
            var action = currentActions[i];

            ImGui.Button("≡");
            if (ImGuiEx.ImGuiEx.IsItemDraggedDelta(action, ImGuiMouseButton.Left, ImGui.GetFrameHeightWithSpacing(), false, out var dt) && dt.Y != 0)
            {
                currentActions.Shift(i, dt.Y);
                this.config.Save();
            }

            if (i == 0)
                buttonIndent = ImGui.GetItemRectSize().X + ImGui.GetStyle().ItemSpacing.X;

            ImGui.SameLine();

            ImGui.SetNextItemWidth(buttonWidth);
            if (ImGui.BeginCombo($"##Action_{i}", this.actions.GetAction(action.ID).Name.ToString()))
            {
                foreach (var item in this.availableActions)
                {
                    if (ImGui.Selectable(item.Name.ToString(), item.RowId == action.ID))
                    {
                        currentActions[i].ID = item.RowId;
                        this.config.Save();
                    }
                }
                ImGui.EndCombo();
            }
            ImGui.SameLine();

            if (ImGuiEx.ImGuiEx.DeleteConfirmationButton())
            {
                currentActions.RemoveAt(i);
                this.config.Save();
            }

        }

        using (ImGuiEx.ImGuiEx.IndentBlock.Begin(buttonIndent))
        {

            using (ImRaii.Disabled(availableActions.Count() == 0 || currentActions.Count >= Hotbar.MaxActionCount))
            {
                if (ImGuiEx.ImGuiEx.FontButton(FontAwesomeIcon.Plus.ToIconString(), UiBuilder.IconFont, new Vector2(buttonWidth, 0)))
                {
                    currentActions.Add(new() { ID = availableActions.First().RowId });
                    this.config.Save();
                }
            }

        }
        ImGui.EndChild();
    }

    private uint currentClassJobId = 0;
    private List<Configuration.Action> currentActions = new();
    private IEnumerable<Action> availableActions = new Action[0];
    public void DrawClassJobs()
    {
        if (ImGui.BeginChild($"{WindowTitle}_SelectionPane", ImGui.GetContentRegionAvail()))
        {
            if (ImGui.BeginListBox("ClassJobSelectionListbox", ImGui.GetContentRegionAvail()))
            {
                foreach (var classJob in this.actions.GetClassJobs())
                {
                    if (ImGui.Selectable(classJob.Name.ToString(), this.currentClassJobId == classJob.RowId))
                    {
                        this.currentClassJobId = classJob.RowId;
                        this.currentActions = this.config.JobActions.GetValueOrDefault(classJob.RowId) ?? new List<Configuration.Action>();
                        if (!this.config.JobActions.ContainsKey(classJob.RowId))
                        {
                            this.config.JobActions[classJob.RowId] = new List<Configuration.Action>();
                        }
                        this.currentActions = this.config.JobActions[classJob.RowId];
                        this.availableActions = this.actions.GetJobActions(classJob.RowId);
                        Service.PluginLog.Debug($"Class Job Changed: {classJob.RowId} {availableActions.Count()}");
                    }
                }
                ImGui.EndListBox();
            }
        }
        ImGui.EndChild();
    }
    public void Dispose()
    {
    }
}

