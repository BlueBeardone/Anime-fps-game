using System.Collections;
using AnimeFpsGame.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace AnimeFpsGame.Tests
{
    public class InputBindingTests
    {
        InputActionAsset asset;

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey(PlayerInputBindings.PrefsKey);
            asset = ScriptableObject.CreateInstance<InputActionAsset>();
            var map = asset.AddActionMap("Player");
            map.AddAction("Move", InputActionType.Value, "<Keyboard>/w");
            map.AddAction("Reload", InputActionType.Button, "<Keyboard>/r");
        }

        [TearDown]
        public void TearDown()
        {
            PlayerInputBindings.CancelRebind();
            PlayerPrefs.DeleteKey(PlayerInputBindings.PrefsKey);
            Object.DestroyImmediate(asset);
        }

        [Test]
        public void RebindingMovesTheActionAndExportsTheOverride()
        {
            var result = PlayerInputBindings.ApplyBindingOverride(asset, "Reload", 0, "<Keyboard>/t");
            Assert.AreEqual(RebindOutcome.Success, result.Outcome);
            Assert.AreEqual("<Keyboard>/t", asset.FindAction("Reload").bindings[0].effectivePath);
            StringAssert.Contains("<Keyboard>/t", PlayerInputBindings.ExportOverrides(asset));
        }

        [Test]
        public void ConflictUnbindsDefaultBindingAndPersists()
        {
            var result = PlayerInputBindings.ApplyBindingOverride(asset, "Reload", 0, "<Keyboard>/w");
            Assert.AreEqual(RebindOutcome.ConflictResolved, result.Outcome);
            Assert.Contains("Move#0", result.ClearedBindings);
            Assert.AreEqual(string.Empty, asset.FindAction("Move").bindings[0].effectivePath);

            // Overrides are keyed by binding id, so a reload scenario must clone the asset (which
            // keeps the ids) rather than build a new one (whose ids are random).
            var fresh = CloneAsset();
            fresh.LoadBindingOverridesFromJson(PlayerInputBindings.ExportOverrides(asset));
            Assert.AreEqual(string.Empty, fresh.FindAction("Move").bindings[0].effectivePath);
            Assert.AreEqual("<Keyboard>/w", fresh.FindAction("Reload").bindings[0].effectivePath);
            Object.DestroyImmediate(fresh);
        }

        [Test]
        public void ResetBindingRestoresDefault()
        {
            PlayerInputBindings.ApplyBindingOverride(asset, "Reload", 0, "<Keyboard>/w");
            PlayerInputBindings.ResetBinding(asset, "Move", 0);
            Assert.AreEqual("<Keyboard>/w", asset.FindAction("Move").bindings[0].effectivePath);
        }

        [Test]
        public void ResetAllRemovesEveryOverride()
        {
            PlayerInputBindings.ApplyBindingOverride(asset, "Reload", 0, "<Keyboard>/t");
            PlayerInputBindings.ResetAllBindings(asset);
            Assert.AreEqual("<Keyboard>/r", asset.FindAction("Reload").bindings[0].effectivePath);
            Assert.IsFalse(PlayerPrefs.HasKey(PlayerInputBindings.PrefsKey));
        }

        [Test]
        public void EscapeAndUnknownActionsAreRejected()
        {
            Assert.AreEqual(RebindOutcome.Failed,
                PlayerInputBindings.ApplyBindingOverride(asset, "Reload", 0, "<Keyboard>/escape").Outcome);
            Assert.AreEqual(RebindOutcome.Failed,
                PlayerInputBindings.ApplyBindingOverride(asset, "Nope", 0, "<Keyboard>/t").Outcome);
            Assert.AreEqual(RebindOutcome.Failed,
                PlayerInputBindings.ApplyBindingOverride(asset, "Reload", 9, "<Keyboard>/t").Outcome);
        }

        [Test]
        public void EnsureLoadedAppliesSavedOverrides()
        {
            PlayerInputBindings.ApplyBindingOverride(asset, "Reload", 0, "<Keyboard>/t");
            PlayerInputBindings.Save(asset);

            var fresh = CloneAsset();
            PlayerInputBindings.EnsureLoaded(fresh);
            Assert.AreEqual("<Keyboard>/t", fresh.FindAction("Reload").bindings[0].effectivePath);
            Object.DestroyImmediate(fresh);
        }

        InputActionAsset CloneAsset()
        {
            var clone = Object.Instantiate(asset);
            clone.RemoveAllBindingOverrides();
            return clone;
        }

        [UnityTest]
        public IEnumerator BeginRebindCapturesKeyboardKey()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>("RebindTestKeyboard");
            try
            {
                RebindResult? result = null;
                Assert.IsTrue(PlayerInputBindings.BeginRebind(asset, "Reload", 0, r => result = r));
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.T));
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                var deadline = Time.realtimeSinceStartup + 2f;
                while (!result.HasValue && Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                }

                Assert.IsTrue(result.HasValue, "Rebind must complete");
                Assert.AreEqual(RebindOutcome.Success, result.Value.Outcome);
                Assert.AreEqual("<Keyboard>/t", asset.FindAction("Reload").bindings[0].effectivePath);
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
            }
        }
    }
}
