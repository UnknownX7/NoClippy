using System.Numerics;
using Dalamud.Interface;
using ImGuiNET;
using static NoClippy.NoClippy;

namespace NoClippy
{
    public static class ConfigUI
    {
        public static bool isVisible = false;

        public static void Draw()
        {
            if (!isVisible) return;

            //ImGui.SetNextWindowSizeConstraints(new Vector2(400, 200) * ImGuiHelpers.GlobalScale, new Vector2(10000));
            ImGui.SetNextWindowSize(new Vector2(400, 0) * ImGuiHelpers.GlobalScale);
            ImGui.Begin("NoClippy Configuration", ref isVisible, ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse);
            ImGui.Columns(2, "NoClippyConfigOptions", false);

            if (ImGui.Checkbox("Enable Plugin", ref Config.Enable))
            {
                TogglePlugin(Config.Enable);
                Config.Save();
            }
            ImGui.Spacing();
            ImGui.Spacing();

            ImGui.NextColumn();
            ImGui.NextColumn();

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

            if (ImGui.Checkbox("Dry Run", ref Config.EnableDryRun))
                Config.Save();
            PluginUI.SetItemTooltip("The plugin will still log and perform calculations, but no in-game values will be overwritten.");

            ImGui.NextColumn();

            if (ImGui.Checkbox("Enable Logging", ref Config.EnableLogging))
                Config.Save();
            //PluginUI.SetItemTooltip("Logs information.");

            ImGui.NextColumn();

            if (ImGui.Checkbox("Output to Chat Log", ref Config.LogToChat))
                Config.Save();
            PluginUI.SetItemTooltip("Sends logging to the chat log instead.");

            ImGui.Columns(1);

            ImGui.Spacing();
            ImGui.Spacing();

            ImGui.SliderFloat("Queue Threshold", ref Config.QueueThreshold, 0, 5, "%.1f");
            if (ImGui.IsItemDeactivated())
            {
                Game.SetupQueueThreshold();
                Config.Save();
            }
            PluginUI.SetItemTooltip("Max time left on the GCD before you can queue another GCD. Default is 0.5.");

            ImGui.End();
        }
    }
}
