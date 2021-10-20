using System;
using System.Linq;
using Dalamud.Game.Network;
using ImGuiNET;
using static NoClippy.NoClippy;

namespace NoClippy
{
    public partial class Configuration
    {
        public bool EnableAnimLockComp = true;
        public bool EnableLogging = false;
        public bool EnableDryRun = false;
    }
}

namespace NoClippy.Modules
{
    public class AnimationLock : INoClippyModule
    {
        // ALL INFO BELOW IS BASED ON MY FINDINGS AND I RESERVE THE RIGHT TO HAVE MISINTERPRETED SOMETHING, THANKS
        // The typical time range that passes for the client is never equal to ping, it always seems to be at least ping + server delay
        // The server delay is usually around 40-60 ms in the overworld, but falls to 30-40 ms inside of instances
        // Additionally, your FPS will add more time because one frame MUST pass for you to receive the new animation lock
        // Therefore, most players will never receive a response within 40 ms at any ping
        // Another interesting fact is that the delay from the server will spike if you send multiple packets at the same time
        // This seems to imply that the server will not process more than one packet from you per tick
        // You can see this if you sheathe your weapon before using an ability, you will notice delays that are around 50 ms higher than usual
        // This explains the phenomenon where moving seems to make it harder to weave

        // For these reasons, I do not believe it is possible to triple weave on any ping without clipping even the slightest amount as that would require 25 ms response times for a 2.5 GCD triple

        // Simulates around 10 ms ping (spiking makes this look closer to 15-20 ms)
        private const float MinSimDelay = 0.04f;
        private const float MaxSimDelay = 0.05f;


        private float delay = -1;
        private float simDelay = (MaxSimDelay - MinSimDelay) / 2f + MinSimDelay;
        private readonly Random rand = new();

        private int packetsSent = 0;
        private float intervalPacketsTimer = 0;
        private int intervalPacketsIndex = 0;
        private readonly int[] intervalPackets = new int[5]; // Record the last 50 ms of packets

        public bool IsEnabled
        {
            get => Config.EnableAnimLockComp;
            set => Config.EnableAnimLockComp = value;
        }

        public int DrawOrder => 1;

        private float AverageDelay(float currentDelay, float weight) =>
            delay > 0
                ? delay = delay * (1 - weight) + currentDelay * weight
                : delay = weight > 0.3f ? currentDelay : currentDelay * 0.75f; // Initial starting delay

        private float SimulateDelay() => simDelay = Math.Min(Math.Max(simDelay + (float)(rand.NextDouble() - 0.5) * 0.016f, MinSimDelay), MaxSimDelay);

        private void UseAction(IntPtr actionManager, uint actionType, uint actionID, long targetedActorID, uint param, uint useType, int pvp, ref byte ret)
        {
            if (ret == 0 || useType != 1 && Game.IsQueued) return;
            packetsSent = intervalPackets.Sum();
        }

        private void UseActionLocation(IntPtr actionManager, uint actionType, uint actionID, long targetedActorID, IntPtr vectorLocation, uint param, ref byte ret)
        {
            if (ret == 0) return;
            packetsSent = intervalPackets.Sum();
        }

        private void ReceiveActionEffect(int sourceActorID, IntPtr sourceActor, IntPtr vectorPosition, IntPtr effectHeader, IntPtr effectArray, IntPtr effectTrail, float oldLock, float newLock)
        {
            if (oldLock == newLock) return;

            // Ignore cast locks (caster tax, teleport, lb)
            if (Game.IsCasting || newLock <= 0.11f) // Unfortunately this isn't always true for casting if the user is above 500 ms ping
            {
                if (Config.EnableLogging)
                    PrintLog($"Ignored reducing server cast lock of {F2MS(newLock)} ms");
                return;
            }

            // Special case to (mostly) prevent accidentally using XivAlexander at the same time
            if (!Config.EnableDryRun && newLock % 0.01 is >= 0.0005f and <= 0.0095f)
            {
                Config.EnableDryRun = true;
                PrintError($"Unexpected lock of {F2MS(newLock)} ms, dry run has been enabled");
            }

            var responseTime = Game.DefaultClientAnimationLock - oldLock;

            var prevAverage = delay;
            var newAverage = AverageDelay(responseTime, packetsSent > 1 ? 0.1f : 0.5f);
            var average = prevAverage > 0 ? prevAverage : newAverage;

            var spikeMult = 1 + Math.Max(responseTime - average, 0) / newAverage;
            var addedDelay = SimulateDelay() * spikeMult;

            var delayOverride = Math.Min(Math.Max(newLock - responseTime + addedDelay, 0), newLock);

            if (!Config.EnableDryRun)
                Game.AnimationLock = delayOverride;

            if (!Config.EnableLogging && oldLock != 0) return;

            PrintLog($"{(Config.EnableDryRun ? "[DRY] " : string.Empty)}" +
                $"Response: {F2MS(responseTime)} ({F2MS(average)}) > {F2MS(addedDelay)} ({F2MS(simDelay)}) (+{(spikeMult - 1):P0}) ms" +
                $"{(Config.EnableDryRun && newLock <= 0.6f && newLock % 0.01 is >= 0.0005f and <= 0.0095f ? $" [Alexander: {F2MS(responseTime - (0.6f - newLock))} ms]" : string.Empty)}" +
                $" || Lock: {F2MS(newLock)} > {F2MS(delayOverride)} ({F2MS(delayOverride - newLock)}) ms" +
                $" || Packets: {packetsSent}");
        }

        private void NetworkMessage(IntPtr dataPtr, ushort opCode, uint sourceActorId, uint targetActorId, NetworkMessageDirection direction)
        {
            if (direction != NetworkMessageDirection.ZoneUp) return;
            intervalPackets[intervalPacketsIndex]++;
        }

        private void Update()
        {
            intervalPacketsTimer += (float)DalamudApi.Framework.UpdateDelta.TotalSeconds;
            while (intervalPacketsTimer >= 0.01f)
            {
                intervalPacketsTimer -= 0.01f;
                intervalPacketsIndex = (intervalPacketsIndex + 1) % intervalPackets.Length;
                intervalPackets[intervalPacketsIndex] = 0;
            }
        }

        public void DrawConfig()
        {
            ImGui.Columns(2, null, false);

            if (ImGui.Checkbox("Enable Anim. Lock Comp.", ref Config.EnableAnimLockComp))
                Config.Save();
            PluginUI.SetItemTooltip("Reduces the animation lock to simulate about 10 ms ping," +
                "\nplease enable dry run if you just want logging with XivAlexander.");

            ImGui.NextColumn();

            if (Config.EnableAnimLockComp)
            {
                ImGui.NextColumn();

                if (ImGui.Checkbox("Enable Logging", ref Config.EnableLogging))
                    Config.Save();
                //PluginUI.SetItemTooltip("Logs information.");

                ImGui.NextColumn();

                if (ImGui.Checkbox("Dry Run", ref Config.EnableDryRun))
                    Config.Save();
                PluginUI.SetItemTooltip("The plugin will still log and perform calculations, but no in-game values will be overwritten.");
            }

            ImGui.Columns(1);
        }

        public void Enable()
        {
            Game.OnUseAction += UseAction;
            Game.OnUseActionLocation += UseActionLocation;
            Game.OnReceiveActionEffect += ReceiveActionEffect;
            Game.OnNetworkMessage += NetworkMessage;
            Game.OnUpdate += Update;
        }

        public void Disable()
        {
            Game.OnUseAction -= UseAction;
            Game.OnUseActionLocation -= UseActionLocation;
            Game.OnReceiveActionEffect -= ReceiveActionEffect;
            Game.OnNetworkMessage -= NetworkMessage;
            Game.OnUpdate -= Update;
        }
    }
}
