using UnityEngine;

namespace UKCity
{
    /// <summary>
    /// Starts the game automatically when you press Play, in any scene (even an empty one).
    /// Existing scene cameras are switched off because the game makes its own.
    /// </summary>
    public static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (Object.FindFirstObjectByType<GameManager>() != null) return;

            foreach (var cam in Camera.allCameras) cam.gameObject.SetActive(false);

            var go = new GameObject("UK City");
            var gm = go.AddComponent<GameManager>();
            gm.Setup();
        }
    }
}
