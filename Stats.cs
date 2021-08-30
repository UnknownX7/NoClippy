using System;
using Dalamud;
using Dalamud.Game;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Hooking;
using Dalamud.Logging;
using Dalamud.Plugin;
using ImGuiNET;
using Reloaded.Hooks.Definitions.Enums;

namespace NoClippy
{
    public static class Stats
    {
        private static DateTime begunEncounter = DateTime.MinValue;
        private static ushort lastDetectedClip = 0;
        private static float currentWastedGCD = 0;
        private static float encounterTotalClip = 0;
        private static float encounterTotalWaste = 0;

        private static void BeginEncounter()
        {
            begunEncounter = DateTime.Now;
            encounterTotalClip = 0;
            encounterTotalWaste = 0;
            currentWastedGCD = 0;
        }

        private static void EndEncounter()
        {
            var span = DateTime.Now - begunEncounter;
            var formattedTime = $"{Math.Floor(span.TotalMinutes):00}:{span.Seconds:00}";
            NoClippy.PrintLog($"[{formattedTime}] Encounter stats: {encounterTotalClip:0.00} seconds of clipping, {encounterTotalWaste:0.00} seconds of wasted GCD.");
            begunEncounter = DateTime.MinValue;
        }

        private static void DetectClipping()
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

        private static void DetectWastedGCD()
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

        private static void UpdateEncounter()
        {
            if (!NoClippy.Config.EnableEncounterStats) return;

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

        public static void Update() => UpdateEncounter();
    }
}
