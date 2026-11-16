using System.Collections;
using System.Linq;
using AnimeFpsGame.Menu;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AnimeFpsGame.Tests
{
    public class MainMenuTests
    {
        GameObject menuObject;

        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            if (menuObject != null)
            {
                Object.Destroy(menuObject);
            }

            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                Object.Destroy(canvas.gameObject);
            }

            foreach (var eventSystem in Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None))
            {
                Object.Destroy(eventSystem.gameObject);
            }

            // Range's auto-host creates a DDOL NetworkManager that would survive into the next test.
            var manager = NetworkManager.Singleton;
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

        [UnityTest]
        public IEnumerator MenuBuildsTheFiveExpectedButtons()
        {
            menuObject = new GameObject("Menu", typeof(MainMenuController));
            yield return null;

            var menu = menuObject.GetComponent<MainMenuController>();
            Assert.AreEqual(5, menu.Buttons.Length);
            CollectionAssert.AreEqual(
                new[] { "CAMPAIGN", "MULTIPLAYER", "RANGE", "SETTINGS", "EXIT" },
                menu.Buttons.Select(button => button.name).ToArray());
            Assert.IsFalse(menu.GetButton("CAMPAIGN").interactable);
            Assert.IsFalse(menu.GetButton("MULTIPLAYER").interactable);
            Assert.IsTrue(menu.GetButton("RANGE").interactable);
            Assert.IsTrue(menu.GetButton("SETTINGS").interactable);
            Assert.IsTrue(menu.GetButton("EXIT").interactable);
            Assert.AreSame(menu.GetButton("RANGE").gameObject, EventSystem.current.currentSelectedGameObject);
        }

        [UnityTest]
        public IEnumerator UnavailableButtonsAreVisiblyDimmed()
        {
            menuObject = new GameObject("Menu", typeof(MainMenuController));
            yield return null;

            var menu = menuObject.GetComponent<MainMenuController>();
            var campaign = menu.GetButton("CAMPAIGN").GetComponentInChildren<Text>();
            var multiplayer = menu.GetButton("MULTIPLAYER").GetComponentInChildren<Text>();
            var range = menu.GetButton("RANGE").GetComponentInChildren<Text>();

            Assert.IsNotNull(campaign);
            Assert.IsNotNull(multiplayer);
            Assert.IsNotNull(range);
            Assert.Less(campaign.color.maxColorComponent, range.color.maxColorComponent,
                "CAMPAIGN's label must be dimmer than an enabled label");
            Assert.Less(multiplayer.color.maxColorComponent, range.color.maxColorComponent,
                "MULTIPLAYER's label must be dimmer than an enabled label");
        }

        [UnityTest]
        public IEnumerator SettingsClickKeepsTheCurrentScene()
        {
            menuObject = new GameObject("Menu", typeof(MainMenuController));
            yield return null;

            var menu = menuObject.GetComponent<MainMenuController>();
            var sceneBefore = SceneManager.GetActiveScene().name;
            LogAssert.Expect(LogType.Log, "[Menu] Settings is not implemented yet.");
            menu.GetButton(MainMenuController.SettingsLabel).onClick.Invoke();
            yield return null;

            Assert.AreEqual(sceneBefore, SceneManager.GetActiveScene().name);
        }

        [UnityTest]
        public IEnumerator ExitClickRequestsAQuit()
        {
            menuObject = new GameObject("Menu", typeof(MainMenuController));
            yield return null;

            var menu = menuObject.GetComponent<MainMenuController>();
            var quitRequested = false;
            menu.QuitHandler = () => quitRequested = true;
            menu.GetButton(MainMenuController.ExitLabel).onClick.Invoke();
            yield return null;

            Assert.IsTrue(quitRequested, "EXIT must be wired to the quit path");
        }

        [UnityTest]
        public IEnumerator RangeClickLoadsTheMainScene()
        {
            menuObject = new GameObject("Menu", typeof(MainMenuController));
            yield return null;

            var menu = menuObject.GetComponent<MainMenuController>();
            menu.GetButton(MainMenuController.RangeLabel).onClick.Invoke();

            var deadline = Time.realtimeSinceStartup + 10f;
            while (SceneManager.GetActiveScene().name != MainMenuController.RangeSceneName &&
                   Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.AreEqual(MainMenuController.RangeSceneName, SceneManager.GetActiveScene().name);
        }

        [UnityTest]
        public IEnumerator MenuReusesAnExistingEventSystem()
        {
            var existing = new GameObject("ExistingEventSystem", typeof(EventSystem),
                typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            menuObject = new GameObject("Menu", typeof(MainMenuController));
            yield return null;

            Assert.AreEqual(1, Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length);
            Object.Destroy(existing);
        }

        [Test]
        public void MainMenuIsFirstInBuildSettings()
        {
            StringAssert.EndsWith("MainMenu.unity", SceneUtility.GetScenePathByBuildIndex(0));
            StringAssert.EndsWith("Main.unity", SceneUtility.GetScenePathByBuildIndex(1));
        }

        [UnityTest]
        public IEnumerator MainMenuSceneLoadsItsController()
        {
            SceneManager.LoadScene("MainMenu");
            yield return null;
            yield return null;

            Assert.IsNotNull(Object.FindAnyObjectByType<MainMenuController>());
            Assert.IsNotNull(Object.FindAnyObjectByType<EventSystem>());
        }

        [Test]
        public void SessionCommandLineIsDetected()
        {
            Assert.IsTrue(MainMenuController.HasSessionCommandLine(new[] { "game.exe", "-host" }));
            Assert.IsTrue(MainMenuController.HasSessionCommandLine(new[] { "game.exe", "-join", "127.0.0.1" }));
            Assert.IsFalse(MainMenuController.HasSessionCommandLine(new[] { "game.exe", "-batchmode" }));
        }
    }
}
