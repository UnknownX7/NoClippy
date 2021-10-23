using System;
using System.Linq;
using Dalamud.Game.ClientState.Statuses;
using ImGuiNET;

namespace NoClippy
{
    public partial class Configuration
    {
        public bool PredictInstantCasts = false;
        public bool PredictDualcast = false;
    }
}

namespace NoClippy.Modules
{
    public class StatusPrediction : Module
    {
        public override int DrawOrder => 15;

        private ushort predictedStatus = 0;
        private byte predictedStatusStacks = 0;
        private byte predictedStatusParam = 0;
        private float predictionTimer = 0;
        private short currentSlot = -1;

        private void SetPredictedStatus(ushort status = 0, byte stacks = 0, byte param = 0, float time = 0.75f)
        {
            if (status != 0)
            {
                var p = DalamudApi.ClientState.LocalPlayer;
                if (currentSlot >= 0 && p != null)
                {
                    predictionTimer = -1;
                    UpdateStatusList(p.StatusList, -1, 0, 0, 0, 0);
                }

                predictedStatus = status;
                predictedStatusStacks = stacks;
                predictedStatusParam = param;
                predictionTimer = time;
                currentSlot = -1;

                Game.OnUpdate += Update;
                Game.OnUpdateStatusList += UpdateStatusList;

                if (p != null)
                    UpdateStatusList(p.StatusList, -1, 0, 0, 0, 0);
            }
            else
            {
                Game.OnUpdate -= Update;
                Game.OnUpdateStatusList -= UpdateStatusList;
            }
        }

        private static bool TryGetFreeStatus(StatusList statuses, out short slot, out IntPtr statusPtr)
        {
            for (short i = 0; i < statuses.Length; i++)
            {
                var status = statuses[i];
                if (status != null && IsStatusValid(status.Address)) continue;
                statusPtr = statuses.GetStatusAddress(i);
                slot = i;
                return true;
            }

            statusPtr = IntPtr.Zero;
            slot = -1;
            return false;
        }

        private static unsafe void ApplyStatus(IntPtr statusPtr, ushort status, byte stacks, byte param)
        {
            *(ushort*)statusPtr = status;
            *(byte*)(statusPtr + 0x2) = stacks;
            *(byte*)(statusPtr + 0x3) = param;
        }

        private static unsafe bool IsStatusValid(IntPtr statusPtr)
        {
            var time = *(float*)(statusPtr + 0x4);
            var objectID = *(uint*)(statusPtr + 0x8);
            return time > 0 || objectID is not (0 or 0xE0000000); // Length - 7 seems to be the last one with objectID 0xE0000000?
        }

        private void UseActionLocation(IntPtr actionManager, uint actionType, uint actionID, long targetedActorID, IntPtr vectorLocation, uint param)
        {
            if (actionType != 1) return;

            switch (actionID)
            {
                case 7421 when NoClippy.Config.PredictInstantCasts: // Triplecast
                    SetPredictedStatus(1211, 3);
                    break;
                case 7561 when NoClippy.Config.PredictInstantCasts: // Swiftcast
                    SetPredictedStatus(167);
                    break;
                //case 7383 when NoClippy.Config.PredictInstantCasts && DalamudApi.ClientState.LocalPlayer?.Level >= 78: // Requiescat
                //    SetPredictedStatus(1369);
                //    break;
                //case 23913 when NoClippy.Config.PredictInstantCasts: // Lost Chainspell
                //    SetPredictedStatus(2560);
                //    break;
                // Firestarter?
            }
        }

        public void Update()
        {
            if ((predictionTimer -= (float)DalamudApi.Framework.UpdateDelta.TotalSeconds) <= 0 && DalamudApi.ClientState.LocalPlayer is { } p)
                UpdateStatusList(p.StatusList, -1, 0, 0, 0, 0);
        }

        public void UpdateStatusList(StatusList statusList, short slot, ushort statusID, float remainingTime, ushort stackParam, uint sourceID)
        {
            if (slot > 0 && slot != currentSlot) return;

            var overwritten = slot > 0 && slot == currentSlot;
            if ((overwritten ? statusID == predictedStatus : statusList.Any(s => s.StatusId == predictedStatus && IsStatusValid(s.Address)))
                || !TryGetFreeStatus(statusList, out var freeSlot, out var statusPtr))
            {
                SetPredictedStatus();
                return;
            }

            if (predictionTimer > 0)
            {
                ApplyStatus(statusPtr, predictedStatus, predictedStatusStacks, predictedStatusParam);
                currentSlot = freeSlot;
            }
            else
            {
                if (!overwritten)
                    ApplyStatus(statusPtr, 0, 0, 0);
                currentSlot = -1;
                SetPredictedStatus();
            }
        }

        public override void DrawConfig()
        {
            ImGui.Columns(2, null, false);

            if (ImGui.Checkbox("Predict Instant Casts", ref NoClippy.Config.PredictInstantCasts))
                NoClippy.Config.Save();
            PluginUI.SetItemTooltip("Removes the effects of lag on Swiftcast and Triplecast.");

            ImGui.Columns(1);
        }

        public override void Enable() => Game.OnUseActionLocation += UseActionLocation;

        public override void Disable()
        {
            Game.OnUseActionLocation -= UseActionLocation;
            Game.OnUpdate -= Update;
            Game.OnUpdateStatusList -= UpdateStatusList;
        }
    }
}
