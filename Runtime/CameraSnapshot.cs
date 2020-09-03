using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace SKUnityToolkit.PhotoStudio
{
    [RequireComponent(typeof(Camera))]
    public class CameraSnapshot : MonoBehaviour
    {
        private Camera _camera => GetComponent<Camera>();

        [Tooltip("The layer on which theCamera takes snapshots.")]
        [SerializeField]
        private int _snapshotLayer = 5;

        [SerializeField]
        int _width = 128;

        [SerializeField]
        int _height = 128;

        [SerializeField]
        Color _backgroudColor = Color.clear;

        [SerializeField]
        string _directory = "Assets/Art/Avatars";

        public void Take(GameObject target)
        {
            InitCamera(_camera, _snapshotLayer);
            var texture = TakeObjectSnapshot(target, _backgroudColor, _width, _height);


            var dir = Directory.CreateDirectory(Path.Combine(Directory.GetParent(Application.dataPath).FullName, _directory)).FullName;
            FileInfo fi = SavePNG(texture, target.gameObject.name, dir);

            Debug.Log(string.Format("Snapshot {0} saved to {1}", fi.Name, fi.DirectoryName));
        }

        private static void InitCamera(Camera cam, int layer = 5)
        {
            if (layer < 0 || layer > 31)
                throw new ArgumentOutOfRangeException("layer", "layer argument must specify a valid layer between 0 and 31");

            // Configure the Camera
            cam.cullingMask = 1 << layer;
            cam.clearFlags = CameraClearFlags.SolidColor;
            //cam.backgroundColor = Color.clear;
        }

        #region PNG saving
        /// <summary>
        /// Sanitizes a filename string by replacing illegal characters with underscores.
        /// </summary>
        /// <param name="unsanitizedName">The unsanitized filename string.</param>
        /// <returns>A sanitized filename string with illegal characters replaced with underscores.</returns>
        private static string SanitizeFilename(string unsanitizedName)
        {
            string invalidFileNameChars = Regex.Escape(new string(Path.GetInvalidFileNameChars()));
            string invalidRegStr = string.Format(@"([{0}]*\.+$)|([{0}]+)", invalidFileNameChars);

            return Regex.Replace(unsanitizedName, invalidRegStr, "_");
        }

        /// <summary>
        /// Saves a byte array of PNG data as a PNG file.
        /// </summary>
        /// <param name="bytes">The PNG data to write to a file.</param>
        /// <param name="filename">The name of the file. This will be the current timestamp if not specified.</param>
        /// <param name="directory">The directory in which to save the file. This will be the game/Snapshots directory if not specified.</param>
        /// <returns>A FileInfo pointing to the created PNG file</returns>
        private static FileInfo SavePNG(byte[] bytes, string filename = "", string directory = "")
        {
            directory = !string.IsNullOrEmpty(directory) ? Directory.CreateDirectory(directory).FullName : Directory.CreateDirectory(Path.Combine(Application.dataPath, "../Snapshots")).FullName;
            filename = !string.IsNullOrEmpty(filename) ? SanitizeFilename(filename) + ".png" : DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss-ffff") + ".png";
            string filepath = Path.Combine(directory, filename);

            File.WriteAllBytes(filepath, bytes);

            return new FileInfo(filepath);
        }

        /// <summary>
        /// Saves a Texture2D as a PNG file.
        /// </summary>
        /// <param name="tex">The Texture2D to write to a file.</param>
        /// <param name="filename">The name of the file. This will be the current timestamp if not specified.</param>
        /// <param name="directory">The directory in which to save the file. This will be the game/Snapshots directory if not specified.</param>
        /// <returns>A FileInfo pointing to the created PNG file</returns>
        private static FileInfo SavePNG(Texture2D tex, string filename = "", string directory = "")
        {
            return SavePNG(tex.EncodeToPNG(), filename, directory);
        }
        #endregion

        #region Object preparation
        /// <summary>
        /// This stores the a state (layers) of a GameObject, and provides a method to restore it.
        /// </summary>
        private struct GameObjectStateSnapshot
        {
            private Dictionary<GameObject, int> _layers;

            /// <summary>
            /// Store the current state (layers) of a GameObject
            /// </summary>
            /// <param name="gameObject">The GameObject whose state to store.</param>
            public GameObjectStateSnapshot(GameObject gameObject)
            {
                _layers = new Dictionary<GameObject, int>();
                foreach (Transform t in gameObject.GetComponentsInChildren<Transform>(true))
                {
                    _layers.Add(t.gameObject, t.gameObject.layer);
                }
            }

            /// <summary>
            /// Restore the gameObject to the state stored in this GameObjectStateSnapshot.
            /// </summary>
            public void Restore()
            {
                foreach (KeyValuePair<GameObject, int> entry in _layers)
                {
                    entry.Key.layer = entry.Value;
                }
            }
        }

        /// <summary>
        /// Set the layers of the GameObject and all its children to the Camera's snapshot layer so the Camera can see it.
        /// </summary>
        /// <param name="gameObject">The GameObject apply the layer modifications to.</param>
        private void SetLayersRecursively(GameObject gameObject)
        {
            foreach (Transform transform in gameObject.GetComponentsInChildren<Transform>(true))
                transform.gameObject.layer = _snapshotLayer;
        }

        /// <summary>
        /// Prepares an instantiated GameObject for taking a snapshot by setting its layers.
        /// </summary>
        /// <param name="prefab">The instantiated GameObject to prepare.</param>
        /// <returns>A GameObjectStateSnapshot containing the state of the gameObject prior to modifying its layers.</returns>
        private GameObjectStateSnapshot PrepareObject(GameObject gameObject)
        {
            GameObjectStateSnapshot goss = new GameObjectStateSnapshot(gameObject);

            SetLayersRecursively(gameObject);

            return goss;
        }
        #endregion

        #region TakeObjectSnapshot
        /// <summary>
        /// Takes a snapshot of an instantiated GameObject and returns it as a Texture2D.
        /// </summary>
        /// <param name="gameObject">The instantiated GameObject to snapshot.</param>
        /// <param name="backgroundColor">The background color of the snapshot. Can be transparent.</param>
        /// <param name="width">The width of the snapshot image.</param>
        /// <param name="height">The height of the snapshot image.</param>
        /// <returns>A Texture2D containing the captured snapshot.</returns>
        private Texture2D TakeObjectSnapshot(GameObject gameObject, Color backgroundColor, int width = 128, int height = 128)
        {
            if (gameObject == null)
                throw new ArgumentNullException("gameObject");
            else if (gameObject.scene.name == null)
                throw new ArgumentException("gameObject parameter must be an instantiated GameObject! If you want to use a prefab directly, use TakePrefabSnapshot instead.", "gameObject");

            // Prepare the gameObject and save its current state so we can restore it later
            GameObjectStateSnapshot previousState = PrepareObject(gameObject);

            // Take a snapshot
            Texture2D snapshot = TakeSnapshot(_camera, backgroundColor, width, height);

            // Restore the gameObject to its previous state
            previousState.Restore();

            // Return the snapshot
            return snapshot;
        }
        #endregion

        /// <summary>
        /// Takes a snapshot of whatever is in front of the camera and within the camera's culling mask and returns it as a Texture2D.
        /// </summary>
        /// <param name="backgroundColor">The background color to apply to the camera before taking the snapshot.</param>
        /// <param name="width">The width of the snapshot image.</param>
        /// <param name="height">The height of the snapshot image.</param>
        /// <returns>A Texture2D containing the captured snapshot.</returns>
        private static Texture2D TakeSnapshot(Camera cam, Color backgroundColor, int width, int height)
        {
            // Set the background color of the camera
            cam.backgroundColor = backgroundColor;

            // Get a temporary render texture and render the camera
            cam.targetTexture = RenderTexture.GetTemporary(width, height, 24);
            cam.Render();

            // Activate the temporary render texture
            RenderTexture previouslyActiveRenderTexture = RenderTexture.active;
            RenderTexture.active = cam.targetTexture;

            // Extract the image into a new texture without mipmaps
            Texture2D texture = new Texture2D(cam.targetTexture.width, cam.targetTexture.height, TextureFormat.ARGB32, false);
            texture.ReadPixels(new Rect(0, 0, cam.targetTexture.width, cam.targetTexture.height), 0, 0);
            texture.Apply(false);

            // Reactivate the previously active render texture
            RenderTexture.active = previouslyActiveRenderTexture;

            // Clean up after ourselves
            cam.targetTexture = null;
            RenderTexture.ReleaseTemporary(cam.targetTexture);

            // Return the texture
            return texture;
        }
    }
}