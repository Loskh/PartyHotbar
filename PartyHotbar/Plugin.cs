using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Client.UI;
using KamiToolKit;
using PartyHotbar.Addon;
using PartyHotbar.Node.Component;
using PartyHotbar.Windows;
using System;

namespace PartyHotbar;

public unsafe class Plugin : IDalamudPlugin
{
    private Configuration configuration;
    private ConfigWindow configWindow;
    internal readonly WindowSystem WindowSystem = new("PartyHotbar");
    internal ActionManager ActionManager = null!;
    private const string AddonName = "_Dalamud_PartyHotbarsAddon";
    internal static PartyHotbars PartyHotbars = null!;

    public unsafe Plugin(IDalamudPluginInterface pluginInterface)
    {
        pluginInterface.Create<Service>();
        DragDrop.ResolveAddress();
        KamiToolKitLibrary.Initialize(pluginInterface);
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
        PartyHotbars ??= new PartyHotbars(this.ActionManager, configuration) { Title = AddonName, InternalName = AddonName };
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
        PartyHotbars?.Dispose();
        //DettachToPartyList();
        //PartyHotbars?.Dispose();
        KamiToolKitLibrary.Dispose();
        Service.PluginInterface.UiBuilder.Draw -= DrawUI;
        Service.CommandManager.RemoveHandler("/phb");
        WindowSystem.RemoveAllWindows();
    }
}
