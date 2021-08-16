using System.Numerics;
using Dalamud.Interface;
using ImGuiNET;

namespace NoClippy
{
    public static class ConfigUI
    {
        public static bool isVisible = false;

        public static void Draw()
        {
            if (!isVisible) return;

            //ImGui.SetNextWindowSizeConstraints(new Vector2(400, 200) * ImGuiHelpers.GlobalScale, new Vector2(10000));
            ImGui.SetNextWindowSize(new Vector2(400, 160) * ImGuiHelpers.GlobalScale);
            ImGui.Begin("NoClippy Configuration", ref isVisible, ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse);
            ImGui.Columns(2, "NoClippyConfigOptions", false);

            if (ImGui.Checkbox("Enable Plugin", ref NoClippy.Config.Enable))
            {
                NoClippy.Plugin.TogglePlugin(NoClippy.Config.Enable);
                NoClippy.Config.Save();
            }
            ImGui.Spacing();
            ImGui.Spacing();

            ImGui.NextColumn();
            ImGui.NextColumn();

            if (ImGui.Checkbox("Enable Encounter Stats", ref NoClippy.Config.EnableEncounterStats))
                NoClippy.Config.Save();
            PluginUI.SetItemTooltip("Tracks clips and wasted GCD time while in combat, and logs the total afterwards.");

            ImGui.NextColumn();

            if (NoClippy.Config.EnableEncounterStats)
            {
                if (ImGui.Checkbox("Enable Stats Logging", ref NoClippy.Config.EnableEncounterStatsLogging))
                    NoClippy.Config.Save();
                PluginUI.SetItemTooltip("Logs individual encounter clips and wasted GCD time.");
            }

            ImGui.NextColumn();

            if (ImGui.Checkbox("Dry Run", ref NoClippy.Config.EnableDryRun))
                NoClippy.Config.Save();
            PluginUI.SetItemTooltip("The plugin will still log and perform calculations, but no in-game values will be overwritten.");

            ImGui.NextColumn();

            if (ImGui.Checkbox("Enable Logging", ref NoClippy.Config.EnableLogging))
                NoClippy.Config.Save();
            //PluginUI.SetItemTooltip("Logs information.");

            ImGui.NextColumn();

            if (ImGui.Checkbox("Output to Chat Log", ref NoClippy.Config.LogToChat))
                NoClippy.Config.Save();
            PluginUI.SetItemTooltip("Sends logging to the chat log instead.");

            ImGui.Columns(1);
            ImGui.End();
        }
    }
}
