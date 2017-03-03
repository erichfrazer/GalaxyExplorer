// Copyright Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
using UnityEngine;
using UnityEngine.VR.WSA;
using GalaxyExplorer_SpectatorView;

public class WorldAnchorHandler : GalaxyExplorer.HoloToolkit.Unity.Singleton<WorldAnchorHandler>
{
    private WorldAnchor viewLoaderAnchor;
    private bool viewLoaderAnchorActivelyTracking = true;

    private const float placeViewLoaderWaitTime = 5.0f; // seconds
    private float timeToReplaceViewLoader = placeViewLoaderWaitTime;

    private PlacementControl placementControl;

    private void Start()
    {
        placementControl = TransitionManager.Instance.ViewVolume.GetComponentInChildren<PlacementControl>();

        if (placementControl != null)
        {
            placementControl.ContentHeld += PlacementControl_ContentHeld;
            placementControl.ContentPlaced += PlacementControl_ContentPlaced;
        }

        if (TransitionManager.Instance != null)
        {
            TransitionManager.Instance.ResetStarted += ResetStarted;
        }
    }

    private void Update()
    {
        if (!SpectatorViewSharingConnector.SpectatorViewEnabled)
        {
            // Update will be suspended if the app is suspended or if the device is not tracking
            if (viewLoaderAnchor != null && !viewLoaderAnchorActivelyTracking)
            {
                timeToReplaceViewLoader -= Time.deltaTime;

                if (timeToReplaceViewLoader <= 0.0f)
                {
                    placementControl.TogglePinnedState();
                }
            }
        }
    }

    private void CreateWorldAnchor()
    {
        GameObject sourceObject = ViewLoader.Instance.gameObject;

        viewLoaderAnchor = sourceObject.AddComponent<WorldAnchor>();

        viewLoaderAnchor.OnTrackingChanged += GalaxyWorldAnchor_OnTrackingChanged;

        timeToReplaceViewLoader = placeViewLoaderWaitTime;
    }

    private void DestroyWorldAnchor()
    {
        if (viewLoaderAnchor != null)
        {
            viewLoaderAnchor.OnTrackingChanged -= GalaxyWorldAnchor_OnTrackingChanged;
            Destroy(viewLoaderAnchor);
        }
    }

    private void SetViewLoaderActive(bool active)
    {
        if (viewLoaderAnchor != null)
        {
            for (int i = 0; i < viewLoaderAnchor.transform.childCount; i++)
            {
                viewLoaderAnchor.transform.GetChild(i).gameObject.SetActive(active);
            }
        }
    }

    private void DetachContentFromSpectatorViewAnchor()
    {
        // remove the current content from the Spectator View's Anchor
        // but keep its world position constant
        var viewVolume = TransitionManager.Instance.ViewVolume;
        Debug.Log(string.Format("Detaching content {0} from its parent.", viewVolume.name));
        viewVolume.transform.SetParent(null, true);
    }
    private void AttachContentToSpectatorViewAnchor()
    {
        // reparent our content to Spectator View's Anchor
        var viewVolume = TransitionManager.Instance.ViewVolume;
        var newParent = SpectatorView.SV_ImportExportAnchorManager.Instance.gameObject;
        Debug.Log(string.Format("Attaching content {0} to {1}.", viewVolume.name, newParent.name));
        viewVolume.transform.SetParent(newParent.transform, true);
    }

    #region Callbacks
    private void PlacementControl_ContentHeld()
    {
        if (SpectatorViewSharingConnector.SpectatorViewEnabled)
        {
            DetachContentFromSpectatorViewAnchor();
        }
        else
        {
            // Make sure our content is active/shown
            SetViewLoaderActive(true);
            // Destroy our galaxy WorldAnchor if we are moving it
            DestroyWorldAnchor();
        }
    }

    private void PlacementControl_ContentPlaced()
    {
        if (ViewLoader.Instance != null)
        {
            if (SpectatorViewSharingConnector.SpectatorViewEnabled)
            {
                AttachContentToSpectatorViewAnchor();
            }
            else
            {
                CreateWorldAnchor();
            }
        }
    }

    private void GalaxyWorldAnchor_OnTrackingChanged(WorldAnchor self, bool located)
    {
        viewLoaderAnchorActivelyTracking = located;

        SetViewLoaderActive(located);
    }

    private void ResetStarted()
    {
        if (SpectatorViewSharingConnector.SpectatorViewEnabled)
        {
            DetachContentFromSpectatorViewAnchor();
        }
        else
        {
            if (viewLoaderAnchor != null)
            {
                DestroyWorldAnchor();
            }
        }
        TransitionManager.Instance.ResetFinished += ResetFinished;
    }

    private void ResetFinished()
    {
        if (SpectatorViewSharingConnector.SpectatorViewEnabled)
        {
            AttachContentToSpectatorViewAnchor();
        }
        else
        {
            CreateWorldAnchor();
        }

        TransitionManager.Instance.ResetFinished -= ResetFinished;
    }
    #endregion
}