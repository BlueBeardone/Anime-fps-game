using System.Collections;
using AnimeFpsGame.Input;
using AnimeFpsGame.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AnimeFpsGame.Tests
{
    public class InputReaderTests
    {
        [UnityTearDown]
        public IEnumerator TearDownNetwork()
        {
            var manager = Unity.Netcode.NetworkManager.Singleton;
            if (manager != null)
            {
                manager.Shutdown();
            }

            yield return null;

            if (manager != null)
            {
                Object.Destroy(manager.gameObject);
            }

            yield return null;
        }

        /// <summary>
        /// Batchmode runs the Editor without a focused game view, and in that state the Input System
        /// routes synthetic device state into the editor buffer while controls read the player buffer,
        /// so a simulated key press is unobservable. What is testable headlessly is the wiring: the
        /// spawned prefab has an InputReader bound to the shared Player map, live, with neutral
        /// defaults when nothing is pressed.
        /// </summary>
        [UnityTest]
        public IEnumerator SpawnedPlayerHasLiveInputReaderWithNeutralDefaults()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;

            var weapons = default(PlayerWeapons);
            var deadline = Time.realtimeSinceStartup + 10f;
            while (weapons == null && Time.realtimeSinceStartup < deadline)
            {
                weapons = Object.FindAnyObjectByType<PlayerWeapons>();
                yield return null;
            }

            Assert.IsNotNull(weapons, "Hosting must spawn the player prefab");
            var reader = weapons.GetComponent<InputReader>();
            Assert.IsNotNull(reader, "Spawned player has InputReader");

            var playerInput = weapons.GetComponent<PlayerInput>();
            Assert.IsNotNull(playerInput, "Spawned player has PlayerInput");
            Assert.IsNotNull(playerInput.actions, "PlayerInput must own the shared actions");
            Assert.IsTrue(playerInput.actions.FindActionMap("Player").enabled,
                "The Player action map must be enabled for the owner");

            Assert.AreEqual(Vector2.zero, reader.Move, "No move input when nothing is pressed");
            Assert.AreEqual(Vector2.zero, reader.Look, "No look input when nothing is pressed");
            Assert.IsFalse(reader.SprintHeld);
            Assert.IsFalse(reader.CrouchHeld);
            Assert.IsFalse(reader.JumpPressed);
            Assert.IsFalse(reader.AttackPressed);
            Assert.IsFalse(reader.AttackHeld);
            Assert.IsFalse(reader.ReloadPressed);
            Assert.IsFalse(reader.CycleNextPressed);
            Assert.IsFalse(reader.CyclePreviousPressed);
            Assert.AreEqual(0f, reader.ScrollY, 0.001f);
        }
    }
}
