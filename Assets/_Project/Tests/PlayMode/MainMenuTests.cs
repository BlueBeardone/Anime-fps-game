using System.Collections;
using System.Linq;
using AnimeFpsGame.Menu;
using NUnit.Framework;
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

        [TearDown]
        public void TearDown()
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
        public IEnumerator SettingsClickKeepsTheCurrentScene()
        {
            menuObject = new GameObject("Menu", typeof(MainMenuController));
            yield return null;

            var menu = menuObject.GetComponent<MainMenuController>();
            var sceneBefore = SceneManager.GetActiveScene().name;
            menu.GetButton("SETTINGS").onClick.Invoke();
            yield return null;

            Assert.AreEqual(sceneBefore, SceneManager.GetActiveScene().name);
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
    }
}
