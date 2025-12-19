using System;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Statuses;
using Dalamud.Game.Network;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Network;
using Status = FFXIVClientStructs.FFXIV.Client.Game.Status;

namespace NoClippy
{
    public static unsafe class Game
    {
        public static Structures.ActionManager* actionManager;

        public const float DefaultClientAnimationLock = 0.5f;

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
        private delegate byte UseActionLocationDelegate(nint actionManager, uint actionType, uint actionID, ulong targetedActorID, nint vectorLocation, uint param, byte c);
        private static Hook<UseActionLocationDelegate> UseActionLocationHook;
        private static byte UseActionLocationDetour(nint actionManager, uint actionType, uint actionID, ulong targetedActorID, nint vectorLocation, uint param, byte c)
        {
            var ret =  UseActionLocationHook.Original(actionManager, actionType, actionID, targetedActorID, vectorLocation, param, c);
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
            if (objectID != DalamudApi.ObjectTable.LocalPlayer?.GameObjectId) return;
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
        private static Hook<UpdateStatusDelegate> UpdateStatusHook;

        private static byte UpdateStatusDetour(nint statusList, short slot, ushort statusID, float remainingTime, ushort stackParam, uint sourceID, bool individualUpdate)
        {
            var statusPtr = (Status*)(statusList + 0x8 + 0xC * slot);
            var oldStatusID = statusPtr->StatusId;
            var oldSourceID = statusPtr->SourceObject.ObjectId;
            var ret = UpdateStatusHook.Original(statusList, slot, statusID, remainingTime, stackParam, sourceID, individualUpdate);

            if (DalamudApi.ObjectTable.LocalPlayer is not { } p || statusList.ToInt64() != p.StatusList.Address.ToInt64()) return ret;

            //OnUpdateStatus?.Invoke(statusList, slot, statusID, remainingTime, stackParam, sourceID, individualUpdate);

            if (statusID != 0 && (oldStatusID != statusID || oldSourceID != sourceID))
                OnUpdateStatusList?.Invoke(p.StatusList, slot, statusID, remainingTime, stackParam, sourceID);

            if (!individualUpdate && slot == p.StatusList.Length - 1)
                OnUpdateStatusList?.Invoke(p.StatusList, -1, 0, 0, 0, 0);

            return ret;
        }

        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        private delegate byte ProcessZonePacketUpDelegate(IntPtr a1, IntPtr dataPtr, IntPtr a3, byte a4);
        private static Hook<ProcessZonePacketUpDelegate> ProcessZonePacketUpHook;

        public delegate void NetworkMessageEventDelegate(NetworkMessageDirection direction);
        public static event NetworkMessageEventDelegate OnNetworkMessageDelegate;


        private static byte NetworkMessageDetour(IntPtr a1, IntPtr dataPtr, IntPtr a3, byte a4)
        {
            OnNetworkMessageDelegate?.Invoke(NetworkMessageDirection.ZoneUp);

            return ProcessZonePacketUpHook.Original(a1, dataPtr, a3, a4);
        }

        public static void Initialize()
        {
            actionManager = (Structures.ActionManager*)ActionManager.Instance();

            UseActionHook = DalamudApi.GameInteropProvider.HookFromAddress<UseActionDelegate>((nint)ActionManager.MemberFunctionPointers.UseAction, UseActionDetour);
            UseActionLocationHook = DalamudApi.GameInteropProvider.HookFromAddress<UseActionLocationDelegate>((nint)ActionManager.MemberFunctionPointers.UseActionLocation, UseActionLocationDetour);
            CastBeginHook = DalamudApi.GameInteropProvider.HookFromAddress<CastBeginDelegate>(DalamudApi.SigScanner.ScanText("40 53 57 48 81 EC ?? ?? ?? ?? 48 8B FA 8B D1"), CastBeginDetour); // Bad sig, found within ActorCast packet
            CastInterruptHook = DalamudApi.GameInteropProvider.HookFromAddress<CastInterruptDelegate>(DalamudApi.SigScanner.ScanText("48 8B C4 48 83 EC 48 48 89 58 08"), CastInterruptDetour);
            ReceiveActionEffectHook = DalamudApi.GameInteropProvider.HookFromAddress<ReceiveActionEffectDelegate>(DalamudApi.SigScanner.ScanModule("40 55 56 57 41 54 41 55 41 56 41 57 48 8D AC 24"), ReceiveActionEffectDetour);
            UpdateStatusHook = DalamudApi.GameInteropProvider.HookFromAddress<UpdateStatusDelegate>(DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? 40 0A F0 48 8D 5B"), UpdateStatusDetour);
            ProcessZonePacketUpHook = DalamudApi.GameInteropProvider.HookFromAddress<ProcessZonePacketUpDelegate>(DalamudApi.SigScanner.ScanText("48 89 5C 24 ?? 48 89 74 24 ?? 4C 89 64 24 ?? 55 41 56 41 57 48 8B EC 48 83 EC 70"), NetworkMessageDetour);

            UseActionHook.Enable();
            UseActionLocationHook.Enable();
            CastBeginHook.Enable();
            CastInterruptHook.Enable();
            ReceiveActionEffectHook.Enable();
            ProcessZonePacketUpHook.Enable();
            //UpdateStatusHook.Enable();
        }

        public static event Action OnUpdate;
        public static void Update() => OnUpdate?.Invoke();

        public static void Dispose()
        {
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
            ProcessZonePacketUpHook?.Dispose();
            OnNetworkMessageDelegate = null;

            OnUpdate = null;

            //DefaultClientAnimationLock = 0.5f;
        }
    }
}
