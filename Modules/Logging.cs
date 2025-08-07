using System;
using Dalamud.Game.Text;
using Dalamud.Bindings.ImGui;

namespace NoClippy
{
    public partial class Configuration
    {
        public bool LogToChat = false;
        public XivChatType LogChatType = XivChatType.None;
    }
}

// This is a module because why not
namespace NoClippy.Modules
{
    public class Logging : Module
    {
        public override int DrawOrder => 8;

        public override void DrawConfig()
        {
            if (ImGui.Checkbox("Output to Chat Log", ref NoClippy.Config.LogToChat))
                NoClippy.Config.Save();
            PluginUI.SetItemTooltip("Sends logging to the chat log instead.");

            if (!NoClippy.Config.LogToChat) return;

            if (ImGui.BeginCombo("Log Chat Type", NoClippy.Config.LogChatType.ToString()))
            {
                foreach (var chatType in Enum.GetValues<XivChatType>())
                {
                    if (!ImGui.Selectable(chatType.ToString())) continue;

                    NoClippy.Config.LogChatType = chatType;
                    NoClippy.Config.Save();
                }

                ImGui.EndCombo();
            }

            PluginUI.SetItemTooltip("Overrides the default Dalamud chat channel.");
        }
    }
}
