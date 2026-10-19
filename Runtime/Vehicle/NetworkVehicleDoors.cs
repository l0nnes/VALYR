using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using PurrNet;

namespace VALYR.Multiplayer
{
    /// <summary>
    /// Synchronizes vehicle interactive doors, hood, and tailgate over the network.
    /// Manages client-side authority for opening/closing, sound effects, and input isolation.
    /// </summary>
    [RequireComponent(typeof(NetworkVehicle))]
    public class NetworkVehicleDoors : NetworkBehaviour
    {
        [Header("PurrNet State")]
        [Tooltip("Bitmask of open door states (bit 0=Left, bit 1=Right, bit 2=Hood, bit 3=Tailgate).")]
        [SerializeField]
        private SyncVar<byte> _doorFlags = new(0, 0.05f, ownerAuth: true);

        private NetworkVehicle _netVehicle;
        private Component _doorController;
        private byte _prevDoorFlags;

        // Reflection cache for VALYR.Runtime.Visual.VehicleDoorController (Assembly-CSharp)
        private static readonly Type DoorControllerType =
            Type.GetType("VALYR.Runtime.Visual.VehicleDoorController, Assembly-CSharp");
        private static readonly FieldInfo LeftDoorField = DoorControllerType?.GetField("LeftDoor");
        private static readonly FieldInfo RightDoorField = DoorControllerType?.GetField("RightDoor");
        private static readonly FieldInfo HoodField = DoorControllerType?.GetField("Hood");
        private static readonly FieldInfo TailgateField = DoorControllerType?.GetField("Tailgate");
        private static readonly FieldInfo IsOpenField = LeftDoorField?.FieldType.GetField("IsOpen");
        private static readonly MethodInfo ToggleLeftDoorMethod = DoorControllerType?.GetMethod("ToggleLeftDoor");
        private static readonly MethodInfo ToggleRightDoorMethod = DoorControllerType?.GetMethod("ToggleRightDoor");
        private static readonly MethodInfo ToggleHoodMethod = DoorControllerType?.GetMethod("ToggleHood");
        private static readonly MethodInfo ToggleTailgateMethod = DoorControllerType?.GetMethod("ToggleTailgate");
        private static readonly MethodInfo PlayDoorSoundMethod =
            DoorControllerType?.GetMethod("PlayDoorSound", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo UnsubscribeInputActionsMethod =
            DoorControllerType?.GetMethod("UnsubscribeInputActions", BindingFlags.NonPublic | BindingFlags.Instance);

        private void Awake()
        {
            _netVehicle = GetComponent<NetworkVehicle>();
            if (DoorControllerType != null)
            {
                _doorController = GetComponent(DoorControllerType);
            }
        }

        private void Start()
        {
            IsolateDoorControllerInput();
        }

        protected override void OnSpawned()
        {
            base.OnSpawned();
            IsolateDoorControllerInput();
            _doorFlags.onChanged += HandleDoorFlagsChanged;

            // Apply initial door states on spawn (important for late-joining players)
            if (_netVehicle != null && !_netVehicle.IsLocallyControlled && _doorFlags.value != 0)
            {
                HandleDoorFlagsChanged(_doorFlags.value);
            }
        }

        protected override void OnDespawned()
        {
            base.OnDespawned();
            _doorFlags.onChanged -= HandleDoorFlagsChanged;
        }

        public void UpdateAuthorityState()
        {
            IsolateDoorControllerInput();
        }

        /// <summary>
        /// Prevents VehicleDoorController from double-handling keys or reacting to other players' input.
        /// NetworkVehicleDoors acts as the single authoritative input handler.
        /// </summary>
        public void IsolateDoorControllerInput()
        {
            if (_doorController == null || DoorControllerType == null) return;

            UnsubscribeInputActionsMethod?.Invoke(_doorController, null);

            // Setting dummy action prevents ProcessFallbackKeys() from running inside VehicleDoorController
            var dummyAction = new InputAction("NetworkDoorDummy");
            SetPrivateField(_doorController, "_leftDoorActionRef", dummyAction);
            SetPrivateField(_doorController, "_rightDoorActionRef", dummyAction);
            SetPrivateField(_doorController, "_hoodActionRef", dummyAction);
            SetPrivateField(_doorController, "_tailgateActionRef", dummyAction);
        }

        private void Update()
        {
            if (_doorController == null) return;

            bool isLocal = _netVehicle != null && _netVehicle.IsLocallyControlled;

            if (isLocal)
            {
                // Local Owner processes keyboard input hotkeys (1=Left, 2=Right, 3=Hood, 4=Tailgate)
                var kb = Keyboard.current;
                if (kb != null)
                {
                    if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame)
                        ToggleLeftDoorMethod?.Invoke(_doorController, null);

                    if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame)
                        ToggleRightDoorMethod?.Invoke(_doorController, null);

                    if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame)
                        ToggleHoodMethod?.Invoke(_doorController, null);

                    if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame)
                        ToggleTailgateMethod?.Invoke(_doorController, null);
                }

                // Read current local door states and replicate via SyncVar
                byte currentFlags = ReadCurrentFlags();
                if (currentFlags != _doorFlags.value)
                {
                    _doorFlags.value = currentFlags;
                }
            }
            else
            {
                // Remote proxy: ensure door visual states stay aligned with received network flags
                byte flags = _doorFlags.value;
                if (flags != _prevDoorFlags)
                {
                    HandleDoorFlagsChanged(flags);
                }
            }
        }

        private void HandleDoorFlagsChanged(byte newFlags)
        {
            if (_doorController == null) return;
            if (_netVehicle != null && _netVehicle.IsLocallyControlled) return;

            byte oldFlags = _prevDoorFlags;
            // Apply received door states to remote proxy
            ApplyDoorState(LeftDoorField, (newFlags & (1 << 0)) != 0, (oldFlags & (1 << 0)) != (newFlags & (1 << 0)));
            ApplyDoorState(RightDoorField, (newFlags & (1 << 1)) != 0, (oldFlags & (1 << 1)) != (newFlags & (1 << 1)));
            ApplyDoorState(HoodField, (newFlags & (1 << 2)) != 0, (oldFlags & (1 << 2)) != (newFlags & (1 << 2)));
            ApplyDoorState(TailgateField, (newFlags & (1 << 3)) != 0, (oldFlags & (1 << 3)) != (newFlags & (1 << 3)));
            _prevDoorFlags = newFlags;
        }

        private void ApplyDoorState(FieldInfo doorField, bool isOpen, bool stateChanged)
        {
            if (doorField == null || IsOpenField == null) return;
            var slot = doorField.GetValue(_doorController);
            if (slot == null) return;

            IsOpenField.SetValue(slot, isOpen);

            if (stateChanged && PlayDoorSoundMethod != null)
            {
                PlayDoorSoundMethod.Invoke(_doorController, new object[] { isOpen });
            }
        }

        private byte ReadCurrentFlags()
        {
            if (_doorController == null || IsOpenField == null) return 0;

            byte flags = 0;
            if (GetDoorIsOpen(LeftDoorField)) flags |= 1 << 0;
            if (GetDoorIsOpen(RightDoorField)) flags |= 1 << 1;
            if (GetDoorIsOpen(HoodField)) flags |= 1 << 2;
            if (GetDoorIsOpen(TailgateField)) flags |= 1 << 3;
            return flags;
        }

        private bool GetDoorIsOpen(FieldInfo doorField)
        {
            if (doorField == null || IsOpenField == null) return false;
            var slot = doorField.GetValue(_doorController);
            if (slot == null) return false;
            return (bool)IsOpenField.GetValue(slot);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            field?.SetValue(target, value);
        }
    }
}
