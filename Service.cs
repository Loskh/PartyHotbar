using Dalamud.Game;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PartyHotbar;

internal class Service
{
    [PluginService] public static IAddonLifecycle AddonLifecycle { get; private set; } = null!;
    [PluginService] public static IGameConfig GameConfig { get; private set; } = null!;
    [PluginService] public static ISigScanner SigScanner { get; private set; } = null!;
    [PluginService] public static IPluginLog PluginLog { get; private set; } = null!;
    [PluginService] public static IGameGui GameGui { get; private set; } = null!;
    [PluginService] public static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] public static IFramework Framework { get; private set; } = null!;
    [PluginService] public static IGameInteropProvider GameInteropProvider { get; private set; } = null!;
    [PluginService] public static ICommandManager CommandManager { get; set; } = null!;
    [PluginService] public static IAddonEventManager AddonEventManager { get; set; } = null!;
    [PluginService] public static IPartyList PartyList { get; set; } = null!;
    [PluginService] public static IDataManager DataManager { get; set; } = null!;
    [PluginService] public static IClientState ClientState { get; set; } = null!;
    [PluginService] public static ICondition Condition { get; set; } = null!;
}
