using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraSnapshot : MonoBehaviour
{
    // This disables the "never assigned" warning.
    // These fields will be assigned by the factory.
    /// <summary>
    /// The Camera used internally by the SnapshotCamera.
    /// </summary>
    private Camera cam => this.GetComponent<Camera>();
    /// <summary>
    /// The layer on which the SnapshotCamera takes snapshots.
    /// </summary>
    
    [SerializeField]
    private int _snapshotLayer = 5;

    [SerializeField]
    int _width = 128;

    [SerializeField]
    int _height = 128;

    [SerializeField]
    string _directory = "Assets/Art/Avatars";

    public void Take(GameObject target)
    {
        InitSnapshotCamera(this.cam, this._snapshotLayer);
        var texture = this.TakeObjectSnapshot(target, Color.clear, this._width, this._height);

        
        var dir = Directory.CreateDirectory(Path.Combine(Directory.GetParent(Application.dataPath).FullName, this._directory)).FullName;
        System.IO.FileInfo fi = SavePNG(texture, target.gameObject.name, dir);

        Debug.Log(string.Format("Snapshot {0} saved to {1}", fi.Name, fi.DirectoryName));

        //return Task.FromResult(1);
    }

    /// <summary>
    /// Factory method which sets up and configures a new SnapshotCamera, then returns it.
    /// </summary>
    /// <param name="layer">The layer number of the layer on which to take snapshots.</param>
    /// <param name="name">The name that will be given to the new GameObject the SnapshotCamera will be attached to.</param>
    /// <returns>A new SnapshotCamera, ready for use.</returns>
    public static void InitSnapshotCamera(Camera cam, int layer = 5)
    {
        if (layer < 0 || layer > 31)
            throw new ArgumentOutOfRangeException("layer", "layer argument must specify a valid layer between 0 and 31");

        // Configure the Camera
        cam.cullingMask = 1 << layer;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.clear;
    }

    #region PNG saving
    /// <summary>
    /// Sanitizes a filename string by replacing illegal characters with underscores.
    /// </summary>
    /// <param name="dirty">The unsanitized filename string.</param>
    /// <returns>A sanitized filename string with illegal characters replaced with underscores.</returns>
    private static string SanitizeFilename(string dirty)
    {
        string invalidFileNameChars = Regex.Escape(new string(Path.GetInvalidFileNameChars()));
        string invalidRegStr = string.Format(@"([{0}]*\.+$)|([{0}]+)", invalidFileNameChars);

        return Regex.Replace(dirty, invalidRegStr, "_");
    }

    /// <summary>
    /// Saves a byte array of PNG data as a PNG file.
    /// </summary>
    /// <param name="bytes">The PNG data to write to a file.</param>
    /// <param name="filename">The name of the file. This will be the current timestamp if not specified.</param>
    /// <param name="directory">The directory in which to save the file. This will be the game/Snapshots directory if not specified.</param>
    /// <returns>A FileInfo pointing to the created PNG file</returns>
    public static FileInfo SavePNG(byte[] bytes, string filename = "", string directory = "")
    {
        directory = directory != "" ? Directory.CreateDirectory(directory).FullName : Directory.CreateDirectory(Path.Combine(Application.dataPath, "../Snapshots")).FullName;
        filename = filename != "" ? SanitizeFilename(filename) + ".png" : System.DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss-ffff") + ".png";
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
    public static FileInfo SavePNG(Texture2D tex, string filename = "", string directory = "")
    {
        return SavePNG(tex.EncodeToPNG(), filename, directory);
    }
    #endregion

    #region Object preparation
    /// <summary>
    /// This stores the a state (layers, position, rotation, and scale) of a GameObject, and provides a method to restore it.
    /// </summary>
    private struct GameObjectStateSnapshot
    {
        private GameObject gameObject;
        private Dictionary<GameObject, int> layers;

        /// <summary>
        /// Store the current state (layers, position, rotation, and scale) of a GameObject
        /// </summary>
        /// <param name="gameObject">The GameObject whose state to store.</param>
        public GameObjectStateSnapshot(GameObject gameObject)
        {
            this.gameObject = gameObject;

            this.layers = new Dictionary<GameObject, int>();
            foreach (Transform t in gameObject.GetComponentsInChildren<Transform>(true))
            {
                this.layers.Add(t.gameObject, t.gameObject.layer);
            }
        }

        /// <summary>
        /// Restore the gameObject to the state stored in this GameObjectStateSnapshot.
        /// </summary>
        public void Restore()
        {
            foreach (KeyValuePair<GameObject, int> entry in this.layers)
            {
                entry.Key.layer = entry.Value;
            }
        }
    }

    /// <summary>
    /// Set the layers of the GameObject and all its children to the SnapshotCamera's snapshot layer so the SnapshotCamera can see it.
    /// </summary>
    /// <param name="gameObject">The GameObject apply the layer modifications to.</param>
    private void SetLayersRecursively(GameObject gameObject)
    {
        foreach (Transform transform in gameObject.GetComponentsInChildren<Transform>(true))
            transform.gameObject.layer = _snapshotLayer;
    }

    /// <summary>
    /// Prepares an instantiated GameObject for taking a snapshot by setting its layers and applying the specified position offset, rotation, and scale to it.
    /// </summary>
    /// <param name="prefab">The instantiated GameObject to prepare.</param>
    /// <param name="positionOffset">The position offset relative to the SnapshotCamera to apply to the gameObject.</param>
    /// <param name="rotation">The rotation to apply to the gameObject.</param>
    /// <param name="scale">The scale to apply to the gameObject.</param>
    /// <returns>A GameObjectStateSnapshot containing the state of the gameObject prior to modifying its layers, position, rotation, and scale.</returns>
    private GameObjectStateSnapshot PrepareObject(GameObject gameObject)
    {
        GameObjectStateSnapshot goss = new GameObjectStateSnapshot(gameObject);

        SetLayersRecursively(gameObject);

        return goss;
    }

    /// <summary>
    /// Prepares a prefab for taking a snapshot by creating an instance, setting its layers and applying the specified position offset, rotation, and scale to it.
    /// </summary>
    /// <param name="prefab">The prefab to prepare.</param>
    /// <param name="positionOffset">The position offset relative to the SnapshotCamera to apply to the prefab.</param>
    /// <param name="rotation">The rotation to apply to the prefab.</param>
    /// <param name="scale">The scale to apply to the prefab.</param>
    /// <returns>A prefab instance ready for taking a snapshot.</returns>
    private GameObject PreparePrefab(GameObject prefab, Vector3 positionOffset, Quaternion rotation, Vector3 scale)
    {
        GameObject gameObject = GameObject.Instantiate(prefab, transform.position + positionOffset, rotation) as GameObject;
        gameObject.transform.localScale = scale;
        SetLayersRecursively(gameObject);

        return gameObject;
    }
    #endregion

    #region TakeObjectSnapshot
    /// <summary>
    /// Takes a snapshot of an instantiated GameObject and returns it as a Texture2D.
    /// </summary>
    /// <param name="gameObject">The instantiated GameObject to snapshot.</param>
    /// <param name="backgroundColor">The background color of the snapshot. Can be transparent.</param>
    /// <param name="positionOffset">The position offset relative to the SnapshotCamera that will be applied to the gameObject while taking the snapshot. Its position will be restored after taking the snapshot.</param>
    /// <param name="rotation">The rotation that will be applied to the gameObject while taking the snapshot. Its rotation will be restored after taking the snapshot.</param>
    /// <param name="scale">The scale that will be applied to the gameObject while taking the snapshot. Its scale will be restored after taking the snapshot.</param>
    /// <param name="width">The width of the snapshot image.</param>
    /// <param name="height">The height of the snapshot image.</param>
    /// <returns>A Texture2D containing the captured snapshot.</returns>
    public Texture2D TakeObjectSnapshot(GameObject gameObject, Color backgroundColor, int width = 128, int height = 128)
    {
        if (gameObject == null)
            throw new ArgumentNullException("gameObject");
        else if (gameObject.scene.name == null)
            throw new ArgumentException("gameObject parameter must be an instantiated GameObject! If you want to use a prefab directly, use TakePrefabSnapshot instead.", "gameObject");

        // Prepare the gameObject and save its current state so we can restore it later
        GameObjectStateSnapshot previousState = PrepareObject(gameObject);

        // Take a snapshot
        Texture2D snapshot = TakeSnapshot(this.cam, backgroundColor, width, height);

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
