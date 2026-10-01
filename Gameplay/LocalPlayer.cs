using System;
using FruktSharedLibrary.Core;
using FruktSharedLibrary.Interop;
using Il2CppData.Player;
using Il2CppPlayer.Appearances.God;
using Il2CppServices.Cam;
using Il2CppServices.Inputs;
using Il2CppServices.Player;
using Il2CppServices.UI;
using UnityEngine;

namespace FruktSharedLibrary.Gameplay
{
    /// <summary>
    /// The player ("god" camera body): position, camera, aiming raycasts, teleporting, flight mode,
    /// held objects and cursor control. Only valid while <see cref="GameState.InSandbox"/>.
    /// </summary>
    public static class LocalPlayer
    {
        /// <summary>True when the player exists in the current scene.</summary>
        public static bool Exists => Appearance != null;

        /// <summary>The game's player-camera service.</summary>
        public static IPlayerCameraService CameraService => GameServices.TryGet<IPlayerCameraService>();

        /// <summary>The player's Unity camera (falls back to Camera.main).</summary>
        public static Camera Camera
        {
            get
            {
                var camera = CameraService?.Camera;
                return camera.Exists() ? camera : Camera.main;
            }
        }

        /// <summary>World position of the camera (the player's eyes).</summary>
        public static Vector3 CameraPosition
        {
            get
            {
                var service = CameraService;
                if (service != null)
                    return service.CameraPosition;
                var camera = Camera;
                return camera.Exists() ? camera.transform.position : Vector3.zero;
            }
        }

        /// <summary>World rotation of the camera.</summary>
        public static Quaternion CameraRotation
        {
            get
            {
                var service = CameraService;
                if (service != null)
                    return service.CameraRotation;
                var camera = Camera;
                return camera.Exists() ? camera.transform.rotation : Quaternion.identity;
            }
        }

        /// <summary>Direction the player is looking.</summary>
        public static Vector3 Forward => CameraRotation * Vector3.forward;

        /// <summary>Ray from the camera through the centre of the screen.</summary>
        public static Ray AimRay => new(CameraPosition, Forward);

        /// <summary>Position of the player's body (falls back to the camera position).</summary>
        public static Vector3 Position => Appearance?.BodyPosition ?? CameraPosition;

        /// <summary>Camera field of view in degrees.</summary>
        public static float FieldOfView
        {
            get => CameraService?.FieldOfView ?? (Camera.Exists() ? Camera.fieldOfView : 60f);
            set
            {
                var service = CameraService;
                if (service != null)
                    service.FieldOfView = value;
                else if (Camera.Exists())
                    Camera.fieldOfView = value;
            }
        }

        /// <summary>Moves the player to a position.</summary>
        public static bool Teleport(Vector3 position)
        {
            var appearance = Appearance;
            if (appearance == null)
                return false;
            appearance.Teleport(position);
            return true;
        }

        /// <summary>Moves the player back to the map's start position.</summary>
        public static bool ResetToStart()
        {
            var service = GameServices.TryGet<IPlayerResetService>();
            if (service == null)
                return false;
            service.Reset();
            return true;
        }

        /// <summary>Sets how the player flies (Flat, Shift, Free).</summary>
        public static bool SetFlightMode(GodFlightMode mode)
        {
            var appearance = Appearance;
            if (appearance == null)
                return false;
            appearance.SetFlightMode(mode);
            return true;
        }

        /// <summary>Plays a camera shake (force around 0.1–2 is sensible).</summary>
        public static void ShakeCamera(float force) => GameServices.TryGet<IPlayerCameraShakeService>()?.PlayCameraShake(force);

        // ------------------------------------------------------------ aiming

        /// <summary>Raycasts from the camera along the view direction (ignores triggers).</summary>
        public static bool Raycast(out RaycastHit hit, float maxDistance = 1000f, int layerMask = Physics.DefaultRaycastLayers)
            => Physics.Raycast(AimRay, out hit, maxDistance, layerMask, QueryTriggerInteraction.Ignore);

        /// <summary>The world point the player is looking at, if anything is hit.</summary>
        public static bool TryGetAimPoint(out Vector3 point, float maxDistance = 1000f)
        {
            if (Raycast(out var hit, maxDistance))
            {
                point = hit.point;
                return true;
            }
            point = default;
            return false;
        }

        /// <summary>
        /// A good spot to put something in front of the player: <paramref name="distance"/> metres ahead
        /// (or just before whatever is in the way), optionally dropped onto the ground below.
        /// </summary>
        public static Vector3 GetPointInFront(float distance = 3f, bool snapToGround = true, float heightAboveGround = 0.05f)
        {
            var ray = AimRay;
            var flatForward = Vector3.ProjectOnPlane(ray.direction, Vector3.up);
            if (flatForward.sqrMagnitude < 0.001f)
                flatForward = Vector3.forward;
            flatForward.Normalize();

            var target = ray.origin + flatForward * distance;
            if (Physics.Raycast(ray.origin, flatForward, out var wall, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                target = wall.point - flatForward * 0.5f;

            if (snapToGround && Physics.Raycast(target + Vector3.up * 0.5f, Vector3.down, out var ground, 100f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                target = ground.point + Vector3.up * heightAboveGround;
            return target;
        }

        /// <summary>Rotation that faces the player from a point (handy when spawning things).</summary>
        public static Quaternion RotationFacingPlayer(Vector3 from)
        {
            var dir = Vector3.ProjectOnPlane(CameraPosition - from, Vector3.up);
            return dir.sqrMagnitude < 0.001f ? Quaternion.identity : Quaternion.LookRotation(dir.normalized, Vector3.up);
        }

        // ------------------------------------------------------------ holding

        /// <summary>The rigidbody the player is currently dragging, or null.</summary>
        public static Rigidbody HeldObject => GameServices.TryGet<IPlayerHoldObjectService>()?.HeldObject;

        /// <summary>Drops whatever the player is holding.</summary>
        public static void ReleaseHeldObject() => GameServices.TryGet<IPlayerHoldObjectService>()?.TryStopHoldingObject();

        /// <summary>Pins a rigidbody in place (like the game's pin tool).</summary>
        public static void Pin(Rigidbody body) => GameServices.TryGet<IPlayerPinObjectsService>()?.Pin(body);

        /// <summary>Unpins a pinned rigidbody.</summary>
        public static void Unpin(Rigidbody body) => GameServices.TryGet<IPlayerPinObjectsService>()?.TryUnpin(body);

        // ------------------------------------------------------------ cursor & input

        /// <summary>
        /// Frees the mouse cursor and blocks the game's button input while <paramref name="owner"/> holds it
        /// (use for your own menus). Call <see cref="ReleaseCursor"/> with the same owner to undo.
        /// </summary>
        public static void CaptureCursor(Il2CppSystem.Object owner)
        {
            if (owner == null)
                throw new ArgumentNullException(nameof(owner));
            Try(() => GameServices.TryGet<ICursorScreenModeService>()?.AddFreeRequest(owner));
            Try(() => GameServices.TryGet<IButtonsInputBlockService>()?.AddBlockRequest(owner));
            Try(() => GameServices.TryGet<IOpenMenus>()?.Add(owner));
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        /// <summary>Undoes <see cref="CaptureCursor"/>.</summary>
        public static void ReleaseCursor(Il2CppSystem.Object owner)
        {
            if (owner == null)
                return;
            Try(() => GameServices.TryGet<ICursorScreenModeService>()?.RemoveFreeRequest(owner));
            Try(() => GameServices.TryGet<IButtonsInputBlockService>()?.RemoveBlockRequest(owner));
            Try(() => GameServices.TryGet<IOpenMenus>()?.Remove(owner));
        }

        // ------------------------------------------------------------ internals

        private static IGodPlayerAppearanceService Appearance
        {
            get
            {
                var service = GameServices.TryGet<IGodPlayerAppearanceService>();
                if (service != null)
                    return service;
                var appearance = GameServices.FindObject<GodPlayerAppearance>();
                return appearance.Exists() ? appearance.Service?.Cast<IGodPlayerAppearanceService>() : null;
            }
        }

        private static void Try(Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                FruktLog.Debug("Cursor/input request failed: " + e.Message);
            }
        }
    }
}
