using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AnimeFpsGame.Input
{
    public enum RebindOutcome
    {
        Success,
        ConflictResolved,
        Canceled,
        Failed,
    }

    public struct RebindResult
    {
        public RebindOutcome Outcome;

        /// <summary>"ActionName#bindingIndex" for every binding the new binding displaced.</summary>
        public string[] ClearedBindings;
    }

    /// <summary>
    /// The single contract a future Settings screen uses to rebind controls. Owns overrides,
    /// cross-action conflict clearing and persistence; it is the only gameplay code besides
    /// <see cref="InputReader"/> that talks to the Input System directly.
    /// </summary>
    public static class PlayerInputBindings
    {
        public const string PrefsKey = "animefps.input.bindings.v1";

        static InputActionRebindingExtensions.RebindingOperation activeOperation;
        static InputActionAsset activeAsset;
        static Action<RebindResult> activeCallback;
        static InputActionMap activeMap;
        static bool activeMapWasEnabled;

        public static void EnsureLoaded(InputActionAsset asset)
        {
            if (asset == null || !PlayerPrefs.HasKey(PrefsKey))
            {
                return;
            }

            var wasEnabled = asset.enabled;
            if (wasEnabled)
            {
                asset.Disable();
            }

            asset.LoadBindingOverridesFromJson(PlayerPrefs.GetString(PrefsKey));

            if (wasEnabled)
            {
                asset.Enable();
            }
        }

        public static RebindResult ApplyBindingOverride(InputActionAsset asset, string actionName,
            int bindingIndex, string controlPath)
        {
            var failed = Failed();
            if (asset == null || string.IsNullOrEmpty(controlPath))
            {
                return failed;
            }

            if (string.Equals(controlPath, "<Keyboard>/escape", StringComparison.OrdinalIgnoreCase))
            {
                // Escape is reserved for cancelling a rebind.
                return failed;
            }

            var action = asset.FindAction(actionName);
            if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count)
            {
                return failed;
            }

            action.ApplyBindingOverride(bindingIndex, controlPath);
            var applied = action.bindings[bindingIndex].effectivePath;
            if (string.IsNullOrEmpty(applied))
            {
                return failed;
            }

            var cleared = new List<string>();
            var map = action.actionMap;
            if (map != null)
            {
                foreach (var other in map.actions)
                {
                    for (var i = 0; i < other.bindings.Count; i++)
                    {
                        if (other == action && i == bindingIndex)
                        {
                            continue;
                        }

                        var path = other.bindings[i].effectivePath;
                        if (string.IsNullOrEmpty(path)
                            || !string.Equals(path, applied, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        // Explicitly unbind, so a binding that came from the defaults (and therefore
                        // has no override) does not simply reappear.
                        other.ApplyBindingOverride(i, string.Empty);
                        cleared.Add($"{other.name}#{i}");
                    }
                }
            }

            return new RebindResult
            {
                Outcome = cleared.Count > 0 ? RebindOutcome.ConflictResolved : RebindOutcome.Success,
                ClearedBindings = cleared.ToArray(),
            };
        }

        public static bool BeginRebind(InputActionAsset asset, string actionName, int bindingIndex,
            Action<RebindResult> completed)
        {
            if (asset == null || activeOperation != null)
            {
                return false;
            }

            var action = asset.FindAction(actionName);
            if (action == null || action.actionMap == null || bindingIndex < 0
                || bindingIndex >= action.bindings.Count)
            {
                return false;
            }

            activeAsset = asset;
            activeCallback = completed;
            activeMap = action.actionMap;
            activeMapWasEnabled = activeMap.enabled;
            activeMap.Disable();

            activeOperation = action.PerformInteractiveRebinding(bindingIndex)
                .WithCancelingThrough("<Keyboard>/escape")
                .WithControlsExcluding("<Mouse>/position")
                .WithControlsExcluding("<Mouse>/delta")
                .OnMatchWaitForAnother(0.1f)
                .OnComplete(operation => Finish(operation, action, bindingIndex, false))
                .OnCancel(operation => Finish(operation, action, bindingIndex, true))
                .Start();
            return true;
        }

        public static void CancelRebind()
        {
            activeOperation?.Cancel();
        }

        public static void ResetBinding(InputActionAsset asset, string actionName, int bindingIndex)
        {
            var action = asset != null ? asset.FindAction(actionName) : null;
            if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count)
            {
                return;
            }

            action.RemoveBindingOverride(bindingIndex);
            Save(asset);
        }

        public static void ResetAllBindings(InputActionAsset asset)
        {
            PlayerPrefs.DeleteKey(PrefsKey);
            PlayerPrefs.Save();

            if (asset == null)
            {
                return;
            }

            asset.RemoveAllBindingOverrides();
        }

        public static string ExportOverrides(InputActionAsset asset)
        {
            return asset != null ? asset.SaveBindingOverridesAsJson() : string.Empty;
        }

        public static void ImportOverrides(InputActionAsset asset, string json)
        {
            if (asset == null || string.IsNullOrEmpty(json))
            {
                return;
            }

            asset.LoadBindingOverridesFromJson(json);
        }

        public static void Save(InputActionAsset asset)
        {
            if (asset == null)
            {
                return;
            }

            PlayerPrefs.SetString(PrefsKey, ExportOverrides(asset));
            PlayerPrefs.Save();
        }

        static void Finish(InputActionRebindingExtensions.RebindingOperation operation,
            InputAction action, int bindingIndex, bool canceled)
        {
            var path = canceled ? null : CanonicalPath(operation.selectedControl);
            operation.Dispose();

            var asset = activeAsset;
            var callback = activeCallback;
            var map = activeMap;
            var reEnable = activeMapWasEnabled;
            activeOperation = null;
            activeAsset = null;
            activeCallback = null;
            activeMap = null;
            activeMapWasEnabled = false;

            if (reEnable && map != null)
            {
                map.Enable();
            }

            if (canceled || string.IsNullOrEmpty(path) || asset == null)
            {
                callback?.Invoke(new RebindResult
                {
                    Outcome = RebindOutcome.Canceled,
                    ClearedBindings = Array.Empty<string>(),
                });
                return;
            }

            var result = ApplyBindingOverride(asset, action.name, bindingIndex, path);
            if (result.Outcome == RebindOutcome.Success || result.Outcome == RebindOutcome.ConflictResolved)
            {
                Save(asset);
            }

            callback?.Invoke(result);
        }

        /// <summary>
        /// Converts a captured control path to the layout-based form that survives device renaming:
        /// <c>/RebindTestKeyboard/t</c> becomes <c>&lt;Keyboard&gt;/t</c>, <c>/Gamepad/dpad/up</c>
        /// becomes <c>&lt;Gamepad&gt;/dpad/up</c>.
        /// </summary>
        static string CanonicalPath(InputControl control)
        {
            if (control == null)
            {
                return null;
            }

            var device = control.device;
            var controlPath = control.path;
            if (device == null || string.IsNullOrEmpty(controlPath))
            {
                return controlPath;
            }

            var canonicalDevice = $"<{device.layout}>";
            if (controlPath.Length <= device.path.Length)
            {
                return canonicalDevice;
            }

            return controlPath.StartsWith(device.path + "/", StringComparison.Ordinal)
                ? canonicalDevice + controlPath.Substring(device.path.Length)
                : controlPath;
        }

        static RebindResult Failed()
        {
            return new RebindResult
            {
                Outcome = RebindOutcome.Failed,
                ClearedBindings = Array.Empty<string>(),
            };
        }
    }
}
