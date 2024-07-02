using System;
using Dalamud.Game.ClientState.Statuses;
using Dalamud.Game.Network;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using Status = FFXIVClientStructs.FFXIV.Client.Game.Status;

namespace NoClippy
{
    public static unsafe class Game
    {
        public static Structures.ActionManager* actionManager;

        private static nint defaultClientAnimationLockPtr;
        public static float DefaultClientAnimationLock
        {
            get => 0.5f;
            set
            {
                if (defaultClientAnimationLockPtr != nint.Zero)
                    SafeMemory.WriteBytes(defaultClientAnimationLockPtr, BitConverter.GetBytes(value));
            }
        }

        public delegate void UseActionEventDelegate(nint actionManager, uint actionType, uint actionID, ulong targetedActorID, uint param, uint useType, int pvp, nint a8, byte ret);
        public static event UseActionEventDelegate OnUseAction;
        private delegate byte UseActionDelegate(nint actionManager, uint actionType, uint actionID, ulong targetedActorID, uint param, uint useType, int pvp, nint a8);
        private static Hook<UseActionDelegate> UseActionHook;
        private static byte UseActionDetour(nint actionManager, uint actionType, uint actionID, ulong targetedActorID, uint param, uint useType, int pvp, nint a8)
        {
            var ret = UseActionHook.Original(actionManager, actionType, actionID, targetedActorID, param, useType, pvp, a8);
            OnUseAction?.Invoke(actionManager, actionType, actionID, targetedActorID, param, useType, pvp, a8, ret);
            return ret;
        }

        public delegate void UseActionLocationEventDelegate(nint actionManager, uint actionType, uint actionID, ulong targetedActorID, nint vectorLocation, uint param, byte ret);
        public static event UseActionLocationEventDelegate OnUseActionLocation;
        private delegate byte UseActionLocationDelegate(nint actionManager, uint actionType, uint actionID, ulong targetedActorID, nint vectorLocation, uint param);
        private static Hook<UseActionLocationDelegate> UseActionLocationHook;
        private static byte UseActionLocationDetour(nint actionManager, uint actionType, uint actionID, ulong targetedActorID, nint vectorLocation, uint param)
        {
            var ret =  UseActionLocationHook.Original(actionManager, actionType, actionID, targetedActorID, vectorLocation, param);
            OnUseActionLocation?.Invoke(actionManager, actionType, actionID, targetedActorID, vectorLocation, param, ret);
            return ret;
        }

        private static bool invokeCastInterrupt = false;
        public delegate void CastBeginDelegate(ulong objectID, nint packetData);
        public static event CastBeginDelegate OnCastBegin;
        private static Hook<CastBeginDelegate> CastBeginHook;
        private static void CastBeginDetour(ulong objectID, nint packetData)
        {
            CastBeginHook.Original(objectID, packetData);
            if (objectID != DalamudApi.ClientState.LocalPlayer?.GameObjectId) return;
            OnCastBegin?.Invoke(objectID, packetData);
            invokeCastInterrupt = true;
        }

        // Seems to always be called twice?
        public delegate void CastInterruptDelegate(nint actionManager);
        public static event CastInterruptDelegate OnCastInterrupt;
        private static Hook<CastInterruptDelegate> CastInterruptHook;
        private static void CastInterruptDetour(nint actionManager)
        {
            CastInterruptHook.Original(actionManager);
            if (!invokeCastInterrupt) return;
            OnCastInterrupt?.Invoke(actionManager);
            invokeCastInterrupt = false;
        }

        public delegate void ReceiveActionEffectEventDelegate(int sourceActorID, nint sourceActor, nint vectorPosition, nint effectHeader, nint effectArray, nint effectTrail, float oldLock, float newLock);
        public static event ReceiveActionEffectEventDelegate OnReceiveActionEffect;
        private delegate void ReceiveActionEffectDelegate(int sourceActorID, nint sourceActor, nint vectorPosition, nint effectHeader, nint effectArray, nint effectTrail);
        private static Hook<ReceiveActionEffectDelegate> ReceiveActionEffectHook;
        private static void ReceiveActionEffectDetour(int sourceActorID, nint sourceActor, nint vectorPosition, nint effectHeader, nint effectArray, nint effectTrail)
        {
            var oldLock = actionManager->animationLock;
            ReceiveActionEffectHook.Original(sourceActorID, sourceActor, vectorPosition, effectHeader, effectArray, effectTrail);
            OnReceiveActionEffect?.Invoke(sourceActorID, sourceActor, vectorPosition, effectHeader, effectArray, effectTrail, oldLock, actionManager->animationLock);
        }

        public delegate void UpdateStatusListEventDelegate(StatusList statusList, short slot, ushort statusID, float remainingTime, ushort stackParam, uint sourceID);
        public static event UpdateStatusListEventDelegate OnUpdateStatusList;
        private delegate byte UpdateStatusDelegate(nint status, short slot, ushort statusID, float remainingTime, ushort stackParam, uint sourceID, bool individualUpdate);
        //public static event UpdateStatusDelegate OnUpdateStatus;
        private static Hook<UpdateStatusDelegate> UpdateStatusHook;
        private static byte UpdateStatusDetour(nint statusList, short slot, ushort statusID, float remainingTime, ushort stackParam, uint sourceID, bool individualUpdate)
        {
            var statusPtr = (Status*)(statusList + 0x8 + 0xC * slot);
            var oldStatusID = statusPtr->StatusId;
            var oldSourceID = statusPtr->SourceId;
            var ret = UpdateStatusHook.Original(statusList, slot, statusID, remainingTime, stackParam, sourceID, individualUpdate);

            if (DalamudApi.ClientState.LocalPlayer is not { } p || statusList.ToInt64() != p.StatusList.Address.ToInt64()) return ret;

            //OnUpdateStatus?.Invoke(statusList, slot, statusID, remainingTime, stackParam, sourceID, individualUpdate);

            if (statusID != 0 && (oldStatusID != statusID || oldSourceID != sourceID))
                OnUpdateStatusList?.Invoke(p.StatusList, slot, statusID, remainingTime, stackParam, sourceID);

            if (!individualUpdate && slot == p.StatusList.Length - 1)
                OnUpdateStatusList?.Invoke(p.StatusList, -1, 0, 0, 0, 0);

            return ret;
        }

        public static event IGameNetwork.OnNetworkMessageDelegate OnNetworkMessage;
        private static void NetworkMessage(nint dataPtr, ushort opCode, uint sourceActorId, uint targetActorId, NetworkMessageDirection direction) =>
            OnNetworkMessage?.Invoke(dataPtr, opCode, sourceActorId, targetActorId, direction);

        public static void Initialize()
        {
            actionManager = (Structures.ActionManager*)ActionManager.Instance();

            UseActionHook = DalamudApi.GameInteropProvider.HookFromAddress<UseActionDelegate>((nint)ActionManager.MemberFunctionPointers.UseAction, UseActionDetour);
            UseActionLocationHook = DalamudApi.GameInteropProvider.HookFromAddress<UseActionLocationDelegate>((nint)ActionManager.MemberFunctionPointers.UseActionLocation, UseActionLocationDetour);
            CastBeginHook = DalamudApi.GameInteropProvider.HookFromAddress<CastBeginDelegate>(DalamudApi.SigScanner.ScanText("40 56 41 56 48 81 EC ?? ?? ?? ?? 48 8B F2"), CastBeginDetour); // Bad sig, found within ActorCast packet
            CastInterruptHook = DalamudApi.GameInteropProvider.HookFromAddress<CastInterruptDelegate>(DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? EB 6A 41 8B D6"), CastInterruptDetour);
            ReceiveActionEffectHook = DalamudApi.GameInteropProvider.HookFromAddress<ReceiveActionEffectDelegate>(DalamudApi.SigScanner.ScanModule("40 55 56 57 41 54 41 55 41 56 48 8D AC 24"), ReceiveActionEffectDetour); // 4C 89 44 24 18 53 56 57 41 54 41 57 48 81 EC ?? 00 00 00 8B F9
            UpdateStatusHook = DalamudApi.GameInteropProvider.HookFromAddress<UpdateStatusDelegate>(DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? FF C6 48 8D 5B 0C"), UpdateStatusDetour);
            //defaultClientAnimationLockPtr = DalamudApi.SigScanner.ScanModule("F3 0F 10 05 ?? ?? ?? ?? 41 8B D5"); // TODO: Changed to static address

            // This is normally 0.5f, but I'm increasing it to prevent any weird discrepancies on high ping
            //DefaultClientAnimationLock = 0.6f;

            DalamudApi.GameNetwork.NetworkMessage += NetworkMessage;

            UseActionHook.Enable();
            UseActionLocationHook.Enable();
            CastBeginHook.Enable();
            CastInterruptHook.Enable();
            ReceiveActionEffectHook.Enable();
            //UpdateStatusHook.Enable();
        }

        public static event Action OnUpdate;
        public static void Update() => OnUpdate?.Invoke();

        public static void Dispose()
        {
            DalamudApi.GameNetwork.NetworkMessage -= NetworkMessage;

            UseActionHook?.Dispose();
            OnUseAction = null;
            UseActionLocationHook?.Dispose();
            OnUseActionLocation = null;
            CastBeginHook?.Dispose();
            OnCastBegin = null;
            CastInterruptHook?.Dispose();
            OnCastInterrupt = null;
            ReceiveActionEffectHook?.Dispose();
            OnReceiveActionEffect = null;
            UpdateStatusHook?.Dispose();
            OnUpdateStatusList = null;
            //OnUpdateStatus = null;

            OnUpdate = null;

            //DefaultClientAnimationLock = 0.5f;
        }
    }
}
