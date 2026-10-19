using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace VALYR.Runtime.Visual
{
    [Serializable]
    public class VehicleDoorSlot
    {
        public string Name = "Door";
        [Tooltip("Visual transform inside Graphics.")]
        public Transform Transform;
        [Tooltip("Physical collider transform outside Graphics (under Body Colliders).")]
        public Transform ColliderTransform;
        public Vector3 ClosedEuler = Vector3.zero;
        public Vector3 OpenEuler = new Vector3(0f, -60f, 0f);
        public bool IsOpen;

        [HideInInspector]
        public Quaternion CurrentRot;

        public void Initialize()
        {
            if (Transform != null)
            {
                CurrentRot = Transform.localRotation;
            }
            if (ColliderTransform != null)
            {
                ColliderTransform.localRotation = CurrentRot;
            }
        }

        public void Toggle()
        {
            IsOpen = !IsOpen;
        }

        public void UpdateMotion(float speed, float dt)
        {
            if (Transform == null && ColliderTransform == null) return;

            Quaternion target = Quaternion.Euler(IsOpen ? OpenEuler : ClosedEuler);
            CurrentRot = Quaternion.Slerp(CurrentRot, target, 1f - Mathf.Exp(-speed * dt));

            if (Transform != null)
                Transform.localRotation = CurrentRot;

            if (ColliderTransform != null)
                ColliderTransform.localRotation = CurrentRot;
        }
    }

    /// <summary>
    /// Interactive vehicle doors, hood, and tailgate controller.
    /// Provides smooth procedural opening and closing with spring-damped interpolation,
    /// hotkey bindings, New Input System actions, and public API methods.
    /// </summary>
    public class VehicleDoorController : MonoBehaviour
    {
        [Header("Door Elements")]
        public VehicleDoorSlot LeftDoor = new()
        {
            Name = "Left Door",
            ClosedEuler = Vector3.zero,
            OpenEuler = new Vector3(0f, -60f, 0f)
        };

        public VehicleDoorSlot RightDoor = new()
        {
            Name = "Right Door",
            ClosedEuler = Vector3.zero,
            OpenEuler = new Vector3(0f, 60f, 0f)
        };

        public VehicleDoorSlot Hood = new()
        {
            Name = "Hood",
            ClosedEuler = Vector3.zero,
            OpenEuler = new Vector3(-50f, 0f, 0f)
        };

        public VehicleDoorSlot Tailgate = new()
        {
            Name = "Tailgate",
            ClosedEuler = Vector3.zero,
            OpenEuler = new Vector3(90f, 0f, 0f)
        };

        [Header("Animation Tuning")]
        [Tooltip("Opening and closing smoothing speed (1/s).")]
        [Range(1f, 15f)]
        public float AnimationSpeed = 5.0f;

        [Header("Input Actions Asset")]
        [SerializeField] private InputActionAsset _inputActions;
        [SerializeField] private string _actionMapName = "Driving";
        [SerializeField] private string _leftDoorAction = "Toggle Left Door";
        [SerializeField] private string _rightDoorAction = "Toggle Right Door";
        [SerializeField] private string _hoodAction = "Toggle Hood";
        [SerializeField] private string _tailgateAction = "Toggle Tailgate";

        [Header("Audio (Optional)")]
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _doorOpenSound;
        [SerializeField] private AudioClip _doorCloseSound;

        private InputAction _leftDoorActionRef;
        private InputAction _rightDoorActionRef;
        private InputAction _hoodActionRef;
        private InputAction _tailgateActionRef;

        private void Awake()
        {
            AutoLinkColliders();

            LeftDoor.Initialize();
            RightDoor.Initialize();
            Hood.Initialize();
            Tailgate.Initialize();

            ResolveInputActions();
        }

        private void OnValidate()
        {
            AutoLinkColliders();
        }

        /// <summary>
        /// Automatically discovers and binds physical door colliders under Body Colliders.
        /// </summary>
        [ContextMenu("Auto-Link Colliders")]
        public void AutoLinkColliders()
        {
            var collidersRoot = transform.Find("Body Colliders");
            if (collidersRoot == null) return;

            if (LeftDoor.ColliderTransform == null)
                LeftDoor.ColliderTransform = collidersRoot.Find("Door Left Collider") ?? collidersRoot.Find("Door Left");
            if (RightDoor.ColliderTransform == null)
                RightDoor.ColliderTransform = collidersRoot.Find("Door Right Collider") ?? collidersRoot.Find("Door Right");
            if (Hood.ColliderTransform == null)
                Hood.ColliderTransform = collidersRoot.Find("Body Hood Collider") ?? collidersRoot.Find("Body Hood");
            if (Tailgate.ColliderTransform == null)
                Tailgate.ColliderTransform = collidersRoot.Find("Body Trunk Hatch Collider") ?? collidersRoot.Find("Body Trunk Hatch") ?? collidersRoot.Find("Tailgate Collider");
        }

        private void OnEnable()
        {
            SubscribeInputActions();
        }

        private void OnDisable()
        {
            UnsubscribeInputActions();
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            LeftDoor.UpdateMotion(AnimationSpeed, dt);
            RightDoor.UpdateMotion(AnimationSpeed, dt);
            Hood.UpdateMotion(AnimationSpeed, dt);
            Tailgate.UpdateMotion(AnimationSpeed, dt);

            ProcessFallbackKeys();
        }

        private void ProcessFallbackKeys()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (_leftDoorActionRef == null && (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame))
            {
                ToggleLeftDoor();
            }
            if (_rightDoorActionRef == null && (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame))
            {
                ToggleRightDoor();
            }
            if (_hoodActionRef == null && (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame))
            {
                ToggleHood();
            }
            if (_tailgateActionRef == null && (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame))
            {
                ToggleTailgate();
            }
        }

        public void ToggleLeftDoor()
        {
            LeftDoor.Toggle();
            PlayDoorSound(LeftDoor.IsOpen);
        }

        public void ToggleRightDoor()
        {
            RightDoor.Toggle();
            PlayDoorSound(RightDoor.IsOpen);
        }

        public void ToggleHood()
        {
            Hood.Toggle();
            PlayDoorSound(Hood.IsOpen);
        }

        public void ToggleTailgate()
        {
            Tailgate.Toggle();
            PlayDoorSound(Tailgate.IsOpen);
        }

        public void ToggleAll()
        {
            bool anyClosed = !LeftDoor.IsOpen || !RightDoor.IsOpen || !Hood.IsOpen || !Tailgate.IsOpen;
            LeftDoor.IsOpen = anyClosed;
            RightDoor.IsOpen = anyClosed;
            Hood.IsOpen = anyClosed;
            Tailgate.IsOpen = anyClosed;
            PlayDoorSound(anyClosed);
        }

        public void CloseAll()
        {
            LeftDoor.IsOpen = false;
            RightDoor.IsOpen = false;
            Hood.IsOpen = false;
            Tailgate.IsOpen = false;
            PlayDoorSound(false);
        }

        private void PlayDoorSound(bool open)
        {
            if (_audioSource == null) return;
            AudioClip clip = open ? _doorOpenSound : _doorCloseSound;
            if (clip != null) _audioSource.PlayOneShot(clip);
        }

        private void ResolveInputActions()
        {
            if (_inputActions == null)
            {
                var allAssets = Resources.FindObjectsOfTypeAll<InputActionAsset>();
                for (int i = 0; i < allAssets.Length; i++)
                {
                    if (allAssets[i] != null && allAssets[i].name == "GameInput")
                    {
                        _inputActions = allAssets[i];
                        break;
                    }
                }
            }

            if (_inputActions != null)
            {
                var map = _inputActions.FindActionMap(_actionMapName);
                if (map != null)
                {
                    _leftDoorActionRef = map.FindAction(_leftDoorAction);
                    _rightDoorActionRef = map.FindAction(_rightDoorAction);
                    _hoodActionRef = map.FindAction(_hoodAction);
                    _tailgateActionRef = map.FindAction(_tailgateAction);
                }
            }
        }

        private void SubscribeInputActions()
        {
            if (_leftDoorActionRef != null) _leftDoorActionRef.performed += OnToggleLeftDoorInput;
            if (_rightDoorActionRef != null) _rightDoorActionRef.performed += OnToggleRightDoorInput;
            if (_hoodActionRef != null) _hoodActionRef.performed += OnToggleHoodInput;
            if (_tailgateActionRef != null) _tailgateActionRef.performed += OnToggleTailgateInput;
        }

        private void UnsubscribeInputActions()
        {
            if (_leftDoorActionRef != null) _leftDoorActionRef.performed -= OnToggleLeftDoorInput;
            if (_rightDoorActionRef != null) _rightDoorActionRef.performed -= OnToggleRightDoorInput;
            if (_hoodActionRef != null) _hoodActionRef.performed -= OnToggleHoodInput;
            if (_tailgateActionRef != null) _tailgateActionRef.performed -= OnToggleTailgateInput;
        }

        private void OnToggleLeftDoorInput(InputAction.CallbackContext ctx) => ToggleLeftDoor();
        private void OnToggleRightDoorInput(InputAction.CallbackContext ctx) => ToggleRightDoor();
        private void OnToggleHoodInput(InputAction.CallbackContext ctx) => ToggleHood();
        private void OnToggleTailgateInput(InputAction.CallbackContext ctx) => ToggleTailgate();
    }
}
