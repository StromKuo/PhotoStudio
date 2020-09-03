using System.Collections;
using UnityEngine;

namespace SKUnityToolkit.PhotoStudio
{
    [RequireComponent(typeof(Camera))]
    public class CameraAutomator : MonoBehaviour
    {
        [SerializeField]
        CameraSnapshot _cameraSnapshot = null;

        [Tooltip("The camera will check all the siblings of the current target one by one")]
        [SerializeField]
        Transform _currentTarget = null;

        [SerializeField]
        float _intervalSeconds = 0.5f;

        Camera _camera => GetComponent<Camera>();

#if UNITY_EDITOR
        [ContextMenu(nameof(Run))]
        public void Run()
        {
            if (!Application.isPlaying)
            {
                UnityEditor.EditorUtility.DisplayDialog("PhotoStudio", "This tool can only run when editor playing.", "OK");
                return;
            }

            var cameraParams = GetCameraRelativeInfo();
            if (_currentTarget.parent != null)
            {
                StartCoroutine(Test(cameraParams));
            }
            else
            {
                Debug.LogError("Current Target's parent can not be null");
            }
        }
#endif

        IEnumerator Test(CameraRelativeInfo cameraParams)
        {
            float allCount = _currentTarget.parent.childCount;
            DisplayProgressBar(0);
            foreach (Transform target in _currentTarget.parent)
            {
                DisplayProgressBar(target.GetSiblingIndex() / allCount);

                try
                {
                    ChangeCameraTarget(target, cameraParams);
                }
                catch (System.Exception)
                {
                    ClearProgressBar();
                    throw;
                }

                yield return null;

                try
                {
                    if (_cameraSnapshot != null)
                    {
                        _cameraSnapshot.Take(target.gameObject);
                    }
                    else
                    {
                        Debug.LogError("Camera Snapshot is null");
                    }
                }
                catch (System.Exception)
                {
                    ClearProgressBar();
                    throw;
                }


                yield return new WaitForSeconds(_intervalSeconds);
            }
            ClearProgressBar();
        }

        void DisplayProgressBar(float progress)
        {
#if UNITY_EDITOR
            UnityEditor.EditorUtility.DisplayProgressBar("PhotoStudio", "ing...", progress);
#endif
        }

        void ClearProgressBar()
        {
#if UNITY_EDITOR
            UnityEditor.EditorUtility.ClearProgressBar();
#endif
        }

        private void ChangeCameraTarget(Transform target, CameraRelativeInfo cameraParams)
        {
            //this._camera.transform.position = target.TransformPoint(cameraParams.localPosition);
            //ignore the scale
            _camera.transform.position = target.rotation * cameraParams.deltaPosition + target.position;
            _camera.transform.rotation = target.rotation * cameraParams.deltaRotation;
        }

        private CameraRelativeInfo GetCameraRelativeInfo()
        {
            var ret = new CameraRelativeInfo();
            ret.deltaPosition = _camera.transform.position - _currentTarget.position;
            ret.deltaRotation = _camera.transform.rotation * Quaternion.Inverse(_currentTarget.rotation);
            return ret;
        }

        struct CameraRelativeInfo
        {
            public Vector3 deltaPosition;
            public Quaternion deltaRotation;
        }
    }
}