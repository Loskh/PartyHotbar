using Dalamud.Game.Command;
using Dalamud.Hooking;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using PartyHotbar.Node;
using PartyHotbar.Windows;
using System.Runtime.InteropServices;
using static PartyHotbar.ActionManager;

namespace PartyHotbar;

public unsafe class Plugin : IDalamudPlugin
{
    private Configuration configuration;
    private ConfigWindow configWindow;
    internal readonly WindowSystem WindowSystem = new("PartyHotbar");
    internal ActionManager ActionManager = null!;

    internal readonly PartyHotbars PartyHotbars = null!;

    public unsafe Plugin(IDalamudPluginInterface pluginInterface)
    {
        pluginInterface.Create<Service>();
        this.ActionManager = new ActionManager(this);
        this.configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        configWindow = new ConfigWindow(this, this.ActionManager, this.configuration);
        WindowSystem.AddWindow(configWindow);
        Service.PluginInterface.UiBuilder.Draw += DrawUI;
        Service.PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;
        Service.CommandManager.AddHandler("/phb", new CommandInfo(OnCommand)
        {
            HelpMessage = "Open Config Window for PartyHotbar"
        });
        this.PartyHotbars = new PartyHotbars(this.ActionManager, configuration);
    }
    public void ToggleConfigUI()
    {
        configWindow.Toggle();
    }

    private void DrawUI()
    {
        WindowSystem.Draw();

    }

    private void OnCommand(string command, string arguments)
    {
        ToggleConfigUI();
    }

    public void Dispose()
    {
        this.PartyHotbars.Dispose();
        Service.PluginInterface.UiBuilder.Draw -= DrawUI;
        Service.CommandManager.RemoveHandler("/phb");
        WindowSystem.RemoveAllWindows();
    }
}
