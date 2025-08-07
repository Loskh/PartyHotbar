using Dalamud.Configuration;

namespace PartyHotbar;


[Serializable]
internal class Configuration : IPluginConfiguration
{
    public class Action
    {
        public uint ID = 0;
    }

    public int Version { get; set; } = 0;
    public Dictionary<uint, List<Action>> JobActions { get; set; } = new();
    public int XPitch = 54;
    public int XOffset = 0;
    public float Scale = 1.0f;
    public bool HideSelf = true;

    public void Save()
    {
        Service.PluginInterface.SavePluginConfig(this);
    }
}
