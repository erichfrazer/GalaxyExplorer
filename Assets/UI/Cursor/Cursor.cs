// Copyright Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using GalaxyExplorer.SpectatorView;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Cursor : GalaxyExplorer.HoloToolkit.Unity.Singleton<Cursor>
{
    public enum CursorCollisionSearch
    {
        RaycastSearch,
        SphereCastSearch
    }

    [Serializable]
    public struct PriorityLayerMask
    {
        public CursorCollisionSearch collisionType;
        public LayerMask layers;
    }

    [Tooltip("The cursor will find targets by searching for collisions in-order from first to last.")]
    public PriorityLayerMask[] prioritizedCursorMask;

    public float defaultCursorDistance = 3;
    public float positionUpdateSpeed = 10;
    public float positionUpdateSpeedWhenNoCollision = 1;

    public float visibilitySphereCastRadius = 0.08f;

    public float forwardImpactOffset = -.05f;

    public bool visible = true;

    public float crossFadeDurationInSeconds = .5f;
    public float tapDurationInSeconds = .5f;
    public float pressedAnimationSpeed = 10;

    public float targetScale = 0.4f;
    public float maxScreenSize = 0.4f;

    public CursorStageImage[] stateImages;
    public Material cursorMaterial;
    private float originalAlpha;

    private Vector3 previousPosition;
    private bool isOverToolbar;
    private bool isColliderGalaxyCardPOI;
    private bool tapped;
    private CursorState currentState;

    private Dictionary<CursorState, CursorStageImage> stateImagesRepository;

    private bool runningInEditor = false;

    private void Awake()
    {
#if UNITY_EDITOR
        runningInEditor = true;
#endif
        // The cursor is hidden by default. It will get shown when we load the main scene
        visible = false;

        stateImagesRepository = stateImages.ToDictionary(s => s.mode, s => s);

        if (!cursorMaterial)
        {
            Destroy(this);
        }

        originalAlpha = cursorMaterial.GetFloat("_Alpha");
    }

    private IEnumerator Start()
    {
        InputRouter.Instance.Tapped += Instance_InputTapped;

        while (!Camera.main)
        {
            yield return null;
        }

        previousPosition = Camera.main.transform.position + (Camera.main.transform.forward * defaultCursorDistance);
    }

    private void OnEnable()
    {
        StartCoroutine(StateUpdate());
    }

    private void SetTextures(CursorState state)
    {
        var stateImage = stateImagesRepository[state];
        SetTextures(stateImage.baseState, stateImage.activatedState);
    }

    private void SetTextures(Texture2D main, Texture2D second)
    {
        cursorMaterial.SetTexture("_MainTex", main);
        cursorMaterial.SetTexture("_SecondTex", second);
    }

    private void SetTexturesLevel(float level)
    {
        cursorMaterial.SetFloat("_MainSecondRatio", level);

        if (level == 0)
        {
            cursorMaterial.EnableKeyword("TRANSITION_OFF");
            cursorMaterial.DisableKeyword("TRANSITION_ON");
        }
        else
        {
            cursorMaterial.DisableKeyword("TRANSITION_OFF");
            cursorMaterial.EnableKeyword("TRANSITION_ON");
        }
    }

    private void SetOpacity(float opacity)
    {
        cursorMaterial.SetFloat("_Alpha", opacity);
    }

    private void SetBaseLevel(float level)
    {
        cursorMaterial.SetFloat("_BaseRatio", level);
    }

    private IEnumerator StateUpdate()
    {
        SetTextures(CursorState.Default);
        SetOpacity(0);
        SetTexturesLevel(0);
        SetBaseLevel(1);

        bool wasVisible = false;
        float currentTextureActivatedLevel = 0;

        var previousState = CursorState.Default;
        var wasOverToolbar = false;

        while (true)
        {
            var shouldBeVisible = true;

            if (TransitionManager.Instance && TransitionManager.Instance.InTransition)
            {
                shouldBeVisible = false;
            }

            if (shouldBeVisible && !isOverToolbar)
            {
                if (GazeSelectionManager.Instance && GazeSelectionManager.Instance.SelectedTarget && !isColliderGalaxyCardPOI)
                {
                    // Should be visible
                }
                else
                {
                    // Rule for content that don't want a cursor ... only if a tool isn't currently enabled
                    if (currentState == CursorState.Default)
                    {
                        if (isColliderGalaxyCardPOI)
                        {
                            shouldBeVisible = false;
                        }
                        else
                        {
                            var planet = GameObject.FindObjectOfType<PlanetTransform>();
                            if (planet && planet.gameObject.name == "PlanetStub")
                            {
                                shouldBeVisible = false;
                            }
                        }
                    }
                }
            }

            var currentIsVisible = shouldBeVisible && visible;

            if (!wasVisible && currentIsVisible)
            {
                wasVisible = true;
                tapped = false;
                yield return StartCoroutine(AnimateOpacityFromTo(0, 1));
            }
            else if (wasVisible && !currentIsVisible)
            {
                wasVisible = false;
                tapped = false;
                yield return StartCoroutine(AnimateOpacityFromTo(1, 0));
            }
            else
            {
                if (previousState != currentState)
                {
                    var oldState = previousState;
                    previousState = currentState;

                    if (!isOverToolbar || currentState == CursorState.Default)
                    {
                        if (!isOverToolbar)
                        {
                            yield return StartCoroutine(AnimateTransitionToState(oldState, currentTextureActivatedLevel, currentState));
                        }
                        else
                        {
                            SetTextures(CursorState.Default);
                            SetTexturesLevel(0);
                            yield return StartCoroutine(AnimateTap());
                        }
                    }
                    else if (isOverToolbar)
                    {
                        yield return StartCoroutine(AnimateTap());
                    }

                    currentTextureActivatedLevel = 0;
                }
            }

            if (wasOverToolbar != isOverToolbar)
            {
                wasOverToolbar = isOverToolbar;
                yield return StartCoroutine(AnimateTransitionToState(currentState, currentTextureActivatedLevel, isOverToolbar ? CursorState.Default : currentState));
                currentTextureActivatedLevel = 0;
            }

            switch (currentState)
            {
                default:
                case CursorState.Default:
                    if (tapped)
                    {
                        tapped = false;
                        yield return StartCoroutine(AnimateTap());
                    }

                    break;
                case CursorState.Tilt:
                case CursorState.Zoom:
                case CursorState.Pin:
                    var targetTextureLevel = InputRouter.Instance.PressedSources.Count > 0 ? 1 : 0;
                    currentTextureActivatedLevel = Mathf.Lerp(currentTextureActivatedLevel, targetTextureLevel, Time.deltaTime * pressedAnimationSpeed);
                    SetTexturesLevel(currentTextureActivatedLevel);
                    break;
            }

            yield return null;
        }
    }

    private IEnumerator AnimateTransitionToState(CursorState previousState, float originalTransitionLevel, CursorState newState)
    {
        if (originalTransitionLevel > 0)
        {
            yield return StartCoroutine(AnimateCrossFadeLevelFromTo(originalTransitionLevel, 0, crossFadeDurationInSeconds * .1f));
        }

        SetTextures(stateImagesRepository[previousState].baseState, stateImagesRepository[newState].baseState);
        yield return StartCoroutine(AnimateCrossFadeLevelFromTo(0, 1, crossFadeDurationInSeconds));

        SetTextures(newState);
        SetTexturesLevel(0);
    }

    private IEnumerator AnimateTap()
    {
        yield return StartCoroutine(AnimateCrossFadeLevelFromTo(0, 1, tapDurationInSeconds / 2.0f));
        yield return StartCoroutine(AnimateCrossFadeLevelFromTo(1, 0, tapDurationInSeconds / 2.0f));
        tapped = false;
    }

    private IEnumerator AnimateCrossFadeLevelFromTo(float source, float target, float duration = -1)
    {
        if (duration <= 0)
        {
            duration = crossFadeDurationInSeconds;
        }

        var timeLeft = duration;
        while (timeLeft > 0)
        {
            SetTexturesLevel(Mathf.Lerp(target, source, timeLeft / duration));
            timeLeft -= Time.deltaTime;
            yield return null;
        }

        SetTexturesLevel(target);
    }

    private IEnumerator AnimateOpacityFromTo(float source, float target)
    {
        var timeLeft = crossFadeDurationInSeconds;
        while (timeLeft > 0)
        {
            SetOpacity(Mathf.Lerp(target, source, timeLeft / crossFadeDurationInSeconds));
            timeLeft -= Time.deltaTime;
            yield return null;
        }

        SetOpacity(target);
    }

    private void Instance_InputTapped(UnityEngine.VR.WSA.Input.InteractionSourceKind sourceKind, int tapCount, Ray ray)
    {
        tapped = true;
    }

    private void Update()
    {
        var cam = Camera.main;

        if (!cam)
        {
            return;
        }

        Transform cursorOriginTransform = cam.transform;

        // We do not want the cursor to collide with things inside the near clip plane. shift our gaze position forward by that amount.
        Ray cursorGazeRay = new Ray(cam.transform.position + (cam.nearClipPlane * cam.transform.forward), cam.transform.forward);

        if (runningInEditor && GE_SpectatorViewManager.SpectatorViewEnabled)
        {
            GE_SpectatorViewManager.TryGetHoloLensUserTransform(ref cursorOriginTransform);
            GE_SpectatorViewManager.TryGetHoloLensUserGazeRay(ref cursorGazeRay);
        }

        // If we are running in SpectatorView, OnUpdateCursorTransform takes care
        // of moving the cursor for everyone but the HoloLens user.
        if (GE_SpectatorViewManager.SpectatorViewEnabled && 
            GE_SpectatorViewManager.Instance.SpectatorViewParticipantsReady && 
            !GE_SpectatorViewManager.Instance.IsHoloLensUser)
        {
            if (runningInEditor)
            {
                Transform trans = cam.transform;
                if (GE_SpectatorViewManager.TryGetHoloLensUserTransform(ref trans))
                {
                    var endPoint = trans.position + trans.forward * 10f;
                    Debug.DrawLine(trans.position, endPoint, Color.red);
                }
            }
            return;
        }

        Vector3 desiredPosition = cursorOriginTransform.position + (cursorOriginTransform.forward * defaultCursorDistance);

        bool hasHit = false;
        bool hasUIHit = false;
        isOverToolbar = false;
        isColliderGalaxyCardPOI = false;

        foreach (PriorityLayerMask priorityMask in prioritizedCursorMask)
        {
            RaycastHit hitInfo;
            switch (priorityMask.collisionType)
            {
                case CursorCollisionSearch.RaycastSearch:
                    if (Physics.Raycast(cursorGazeRay, out hitInfo, float.MaxValue, priorityMask.layers))
                    {
                        var collider = hitInfo.collider;
                        isOverToolbar = collider.GetComponent<Button>() != null || collider.GetComponent<Tool>() != null;
                        var poiReference = collider.GetComponentInParent<PointOfInterestReference>();
                        isColliderGalaxyCardPOI = poiReference && poiReference.pointOfInterest && poiReference.pointOfInterest is CardPointOfInterest;

                        desiredPosition = hitInfo.point + (forwardImpactOffset * cursorOriginTransform.forward);
                        hasHit = true;
                        hasUIHit = true;
                    }

                    break;

                case CursorCollisionSearch.SphereCastSearch:
                    hasHit = Physics.SphereCast(cursorGazeRay, visibilitySphereCastRadius, out hitInfo, float.MaxValue, priorityMask.layers);

                    if (hasHit)
                    {
                        // transpose the hit point from world space into cursorOrigin space
                        var cursorOriginSpaceHit = cursorOriginTransform.InverseTransformPoint(hitInfo.point);

                        // transpose the desired position from world space into cursorOrigin space
                        var cursorOriginSpaceDesiredPosition = cursorOriginTransform.InverseTransformPoint(desiredPosition);
                        // take the z component of our raycast hit and apply it to our desired position
                        cursorOriginSpaceDesiredPosition.z = cursorOriginSpaceHit.z;

                        // transpose the desired position back into world space
                        desiredPosition = cursorOriginTransform.TransformPoint(cursorOriginSpaceDesiredPosition);
                    }
                    break;
            }

            if (hasHit == true)
            {
                break;
            }
        }

        // orient the cursor towards its source
        transform.rotation = Quaternion.LookRotation(cursorOriginTransform.forward, cursorOriginTransform.up);

        // get the previous and desired positions in cursor source space
        var cursorOriginSpacePreviousPos = cursorOriginTransform.InverseTransformPoint(previousPosition);
        var cursorOriginSpaceDesiredPos = cursorOriginTransform.InverseTransformPoint(desiredPosition);

        // smoothly move the cursor from its previous position to its final position
        var cursorOriginSpaceFinalPos = Vector3.Lerp(cursorOriginSpacePreviousPos, cursorOriginSpaceDesiredPos, positionUpdateSpeed * Time.deltaTime);
        cursorOriginSpaceFinalPos.z = Mathf.Lerp(cursorOriginSpacePreviousPos.z, cursorOriginSpaceDesiredPos.z,
            (hasUIHit ? positionUpdateSpeed : positionUpdateSpeedWhenNoCollision) * Time.deltaTime);

        // put everything back into world space
        transform.position = previousPosition = cursorOriginTransform.TransformPoint(cursorOriginSpaceFinalPos);

        // scale the cursor based on its distance from its origin
        var distance = (transform.position - cursorOriginTransform.position).magnitude;
        transform.localScale = Vector3.one * Mathf.Min(targetScale, maxScreenSize * distance);

        // now that we know, tell everyone else where to render the cursor
        if (GE_SpectatorViewManager.SpectatorViewEnabled && GE_SpectatorViewManager.Instance.IsHoloLensUser)
        {
            GE_SpectatorViewManager.Instance.SendUpdateCursorTransform();
            //GE_SpectatorViewManager.Instance.SendUpdateTransform(
            //    transform,
            //    GE_SpectatorViewManager.TransformToUpdate.Cursor,
            //    GE_SpectatorViewManager.TransformUpdateFlags.Position |
            //    GE_SpectatorViewManager.TransformUpdateFlags.Rotation |
            //    GE_SpectatorViewManager.TransformUpdateFlags.LocalScale);
        }
    }

    public void ApplyCursorState(CursorState state)
    {
        currentState = state;
    }

    public void ApplyToolState(ToolType type)
    {
        ApplyCursorState(TranslateToolTypeToCursorState(type));
    }

    private CursorState TranslateToolTypeToCursorState(ToolType type)
    {
        switch (type)
        {
            default:
            case ToolType.Pan:
            case ToolType.Reset:
                return CursorState.Default;
            case ToolType.Rotate:
                return CursorState.Tilt;
            case ToolType.Zoom:
                return CursorState.Zoom;
        }
    }

    public void ClearToolState()
    {
        ApplyCursorState(CursorState.Default);
    }

    private void OnDestroy()
    {
        cursorMaterial.SetFloat("_Alpha", originalAlpha);
    }
}
