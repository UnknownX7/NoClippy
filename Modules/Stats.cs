using System;
using Dalamud.Game.ClientState.Conditions;
using ImGuiNET;

namespace NoClippy
{
    public partial class Configuration
    {
        public bool EnableEncounterStats = false;
        public bool EnableEncounterStatsLogging = false;
    }
}

namespace NoClippy.Modules
{
    public class Stats : INoClippyModule
    {
        private DateTime begunEncounter = DateTime.MinValue;
        private ushort lastDetectedClip = 0;
        private float currentWastedGCD = 0;
        private float encounterTotalClip = 0;
        private float encounterTotalWaste = 0;

        public bool IsEnabled
        {
            get => NoClippy.Config.EnableEncounterStats;
            set => NoClippy.Config.EnableEncounterStats = value;
        }

        public int DrawOrder => 5;

        private void BeginEncounter()
        {
            begunEncounter = DateTime.Now;
            encounterTotalClip = 0;
            encounterTotalWaste = 0;
            currentWastedGCD = 0;
        }

        private void EndEncounter()
        {
            var span = DateTime.Now - begunEncounter;
            var formattedTime = $"{Math.Floor(span.TotalMinutes):00}:{span.Seconds:00}";
            NoClippy.PrintLog($"[{formattedTime}] Encounter stats: {encounterTotalClip:0.00} seconds of clipping, {encounterTotalWaste:0.00} seconds of wasted GCD.");
            begunEncounter = DateTime.MinValue;
        }

        private void DetectClipping()
        {
            if (lastDetectedClip == Game.ActionCount || Game.IsGCDRecastActive || Game.AnimationLock <= 0) return;

            if (Game.AnimationLock != 0.1f) // TODO need better way of detecting cast tax, IsCasting is not reliable here, additionally, this will detect LB
            {
                encounterTotalClip += Game.AnimationLock;
                if (NoClippy.Config.EnableEncounterStatsLogging)
                    NoClippy.PrintLog($"GCD Clip: {NoClippy.F2MS(Game.AnimationLock)} ms");
            }

            lastDetectedClip = Game.ActionCount;
        }

        private void DetectWastedGCD()
        {
            if (!Game.IsGCDRecastActive && !Game.IsQueued)
            {
                if (Game.AnimationLock > 0) return;
                currentWastedGCD += ImGui.GetIO().DeltaTime;
            }
            else if (currentWastedGCD > 0)
            {
                encounterTotalWaste += currentWastedGCD;
                if (NoClippy.Config.EnableEncounterStatsLogging)
                    NoClippy.PrintLog($"Wasted GCD: {NoClippy.F2MS(currentWastedGCD)} ms");
                currentWastedGCD = 0;
            }
        }

        private void Update()
        {
            if (DalamudApi.Condition[ConditionFlag.InCombat])
            {
                if (begunEncounter == DateTime.MinValue)
                    BeginEncounter();

                DetectClipping();
                DetectWastedGCD();
            }
            else if (begunEncounter != DateTime.MinValue)
            {
                EndEncounter();
            }
        }

        public void DrawConfig()
        {
            ImGui.Columns(2, null, false);

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

            ImGui.Columns(1);
        }

        public void Enable() => Game.OnUpdate += Update;
        public void Disable() => Game.OnUpdate -= Update;
    }
}
