using BehaviorDesigner.Runtime.Tasks;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraAutomator : MonoBehaviour
{
    [SerializeField]
    CameraSnapshot _cameraSnapshot;

    [SerializeField]
    Transform _currentTarget;

    private Camera _camera => this.GetComponent<Camera>();

    [ContextMenu(nameof(Run))]
    public void Run()
    {
        var cameraParams = this.GetCameraParams();
        if (this._currentTarget.parent != null)
        {
            this.StartCoroutine(this.Test(cameraParams));
        }
        else
        {
            Debug.LogError("Current Target's parent can not be null");
        }
    }

    IEnumerator Test(CameraParams cameraParams)
    {
        float allCount = this._currentTarget.parent.childCount;
        this.DisplayProgressBar(0);
        foreach (Transform target in this._currentTarget.parent)
        {
            this.DisplayProgressBar(target.GetSiblingIndex() / allCount);

            try
            {
                this.ChangeCameraTarget(target, cameraParams);
            }
            catch (System.Exception)
            {
                this.ClearProgressBar();
                throw;
            }

            yield return null;

            try
            {
                this._cameraSnapshot.Take(target.gameObject);
            }
            catch (System.Exception)
            {
                this.ClearProgressBar();
                throw;
            }
            
            
            yield return new WaitForSeconds(2);
        }
        this.ClearProgressBar();
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

    private void ChangeCameraTarget(Transform target, CameraParams cameraParams)
    {
        this._camera.transform.position = target.TransformPoint(cameraParams.localPosition);
        this._camera.transform.rotation = target.rotation * cameraParams.localRotation;
    }

    private CameraParams GetCameraParams()
    {
        var ret = new CameraParams();
        ret.localPosition = this.transform.position - this._currentTarget.position;
        ret.localRotation = Quaternion.FromToRotation(this._currentTarget.forward, this.transform.forward);
        return ret;
    }

    class CameraParams
    {
        public Vector3 localPosition;
        public Quaternion localRotation;
    }
}
