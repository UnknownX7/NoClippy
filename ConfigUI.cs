using System.Numerics;
using Dalamud.Interface;
using ImGuiNET;
using static NoClippy.NoClippy;

namespace NoClippy
{
    public static class ConfigUI
    {
        public static bool isVisible = false;
        public static void ToggleVisible() => isVisible ^= true;

        public static void Draw()
        {
            if (!isVisible) return;

            //ImGui.SetNextWindowSizeConstraints(new Vector2(400, 200) * ImGuiHelpers.GlobalScale, new Vector2(10000));
            ImGui.SetNextWindowSize(new Vector2(400, 0) * ImGuiHelpers.GlobalScale);
            ImGui.Begin("NoClippy Configuration", ref isVisible, ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse);

            Modules.Modules.Draw();

            ImGui.Columns(2, "NoClippyConfigOptions", false);

            //ImGui.NextColumn();
            ImGui.Separator();

            if (ImGui.Checkbox("Enable Encounter Stats", ref Config.EnableEncounterStats))
                Config.Save();
            PluginUI.SetItemTooltip("Tracks clips and wasted GCD time while in combat, and logs the total afterwards.");

            ImGui.NextColumn();

            if (Config.EnableEncounterStats)
            {
                if (ImGui.Checkbox("Enable Stats Logging", ref Config.EnableEncounterStatsLogging))
                    Config.Save();
                PluginUI.SetItemTooltip("Logs individual encounter clips and wasted GCD time.");
            }

            ImGui.NextColumn();
            ImGui.Separator();

            if (ImGui.Checkbox("Output to Chat Log", ref Config.LogToChat))
                Config.Save();
            PluginUI.SetItemTooltip("Sends logging to the chat log instead.");

            ImGui.Columns(1);

            ImGui.Separator();
            ImGui.Spacing();
            ImGui.Spacing();

            if (ImGui.SliderFloat("Queue Threshold", ref Config.QueueThreshold, 0, 2.5f, "%.1f"))
            {
                Game.QueueThreshold = Config.QueueThreshold;
                Config.Save();
            }
            PluginUI.SetItemTooltip("Max time left on the GCD before you can queue another GCD." +
                "\nDefault is 0.5, set it to 2.5 to always allow queuing.");

            ImGui.End();
        }
    }
}
