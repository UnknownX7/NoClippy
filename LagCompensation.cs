using System;
using static NoClippy.NoClippy;

namespace NoClippy
{
    public static class LagCompensation
    {
        // This is the typical time range that passes between the time when the client sets a lock and then receives the new lock from the server on a low ping environment
        // This data is an estimate of what near 0 ping would be, based on 20 ms ping logs (feel free to show me logs if you actually have near 0 ms ping)
        private const float MinSimDelay = 0.04f;
        private const float MaxSimDelay = 0.06f;

        // This will allow a portion of actual spikes (either from your internet or the server) to bleed into the simulated delay
        // This makes your delay look natural to other people since networks aren't perfect (notably, sending multiple packets at the same time can add 50-100 ms)
        private const bool AllowSpikes = true;


        private static byte ignoreNext = 0;
        private static float delay = -1;
        private static float simDelay = (MaxSimDelay - MinSimDelay) / 2f + MinSimDelay;
        private static readonly Random rand = new();

        private static float AverageDelay(float currentDelay) => delay > 0 ? delay = delay * 0.5f + currentDelay * 0.5f : delay = currentDelay * 0.75f;
        private static float SimulateDelay() => simDelay = Math.Min(Math.Max(simDelay + (float)(rand.NextDouble() - 0.5) * 0.016f, MinSimDelay), MaxSimDelay);
        public static void CompensateAnimationLock(float oldLock, float newLock)
        {
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
                ignoreNext = 2;
                PrintError($"Unexpected lock of {F2MS(newLock)} ms");
                return;
            }

            if (ignoreNext > 0)
            {
                ignoreNext--;
                PrintError("Detected possible use of XivAlexander");
                return;
            }

            var responseTime = Game.DefaultClientAnimationLock - oldLock;
            var reduction = AllowSpikes ? Math.Min(AverageDelay(responseTime), responseTime) : responseTime;
            var delayOverride = Math.Min(Math.Max(newLock - reduction + SimulateDelay(), 0), newLock);

            if (!Config.EnableDryRun)
                Game.AnimationLock = delayOverride;

            if (!Config.EnableLogging && oldLock != 0) return;

            var spikeDelay = responseTime - reduction;
            PrintLog($"{(Config.EnableDryRun ? "[DRY] " : string.Empty)}" +
                $"Response: {F2MS(responseTime)} ({F2MS(delay)}) > {F2MS(simDelay + spikeDelay)} ({F2MS(simDelay)} + {F2MS(spikeDelay)}) ms" +
                $"{(Config.EnableDryRun && newLock <= 0.6f && newLock % 0.01 is >= 0.0005f and <= 0.0095f ? $" [Alexander: {F2MS(responseTime - (0.6f - newLock))} ms]" : string.Empty)}" +
                $" || Lock: {F2MS(newLock)} > {F2MS(delayOverride)} ({F2MS(delayOverride - newLock) - 1}) ms");
        }
    }
}
