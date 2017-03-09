// Copyright Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using GalaxyExplorer.SpectatorView;
using UnityEngine;
using UnityEngine.VR.WSA.Input;

public class OrbitPicker : GazeSelectionTarget
{
    public PointOfInterest pointOfInterest;
    private MeshCollider orbitMesh;
    private GameObject displayCard;
    private bool runningInEditor = false;

    private void Awake()
    {
#if UNITY_EDITOR
        runningInEditor = true;
#endif
    }
    private void Start()
    {
        orbitMesh = GetComponent<MeshCollider>();
        if (orbitMesh && pointOfInterest)
        {
            // Create focus object that'll face the camera
            var focus = new GameObject("OrbitFocus");
            focus.transform.SetParent(transform);
            var faceCamera = focus.AddComponent<FaceCamera>();
            faceCamera.forceToWorldUp = true;

            // Create the display text and parent it to the focus object to ensure that
            // the text will always be facing the camera
            displayCard = Instantiate(pointOfInterest.Description);
            displayCard.transform.SetParent(focus.transform, worldPositionStays: false);
            displayCard.transform.rotation = Quaternion.Euler(0, 180, 0);
            displayCard.SetActive(false);
        }
    }

    public override void OnGazeSelect()
    {
        Ray cameraRay;
        cameraRay = new Ray(Camera.main.transform.position, Camera.main.transform.forward);
        RaycastHit hitInfo;
        if (orbitMesh && orbitMesh.Raycast(cameraRay, out hitInfo, 1000.0f))
        {
            if (GE_SpectatorViewManager.SpectatorViewEnabled)
            {
                GE_SpectatorViewManager.Instance.SendPointOfInterestGazeSelect(pointOfInterest.gameObject.name, true);
            }
            displayCard.transform.position = hitInfo.point;
            displayCard.SetActive(true);
        }
    }

    public override void OnGazeDeselect()
    {
        if (orbitMesh)
        {
            if (GE_SpectatorViewManager.SpectatorViewEnabled)
            {
                GE_SpectatorViewManager.Instance.SendPointOfInterestGazeSelect(pointOfInterest.gameObject.name, false);
            }
            displayCard.SetActive(false);
        }
    }

    public override bool OnTapped(InteractionSourceKind source, int tapCount, Ray ray)
    {
        if (orbitMesh)
        {
            pointOfInterest.GoToScene();
            displayCard.SetActive(false);
            return true;
        }

        return false;
    }
}
