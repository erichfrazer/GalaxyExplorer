// Copyright Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using GalaxyExplorer.SpectatorView;
using System.Collections;
using UnityEngine;

public class ToolPanel : MonoBehaviour
{
    public class ToolsFader : Fader
    {
        public IFadeTarget[] fadeTargets;

        protected override bool CanAddMaterialsFromRenderer(Renderer renderer, Fader[] faders)
        {
            return false;
        }

        public override bool SetAlpha(float alphaValue)
        {
            alpha = alphaValue;

            if (fadeTargets != null)
            {
                foreach (var target in fadeTargets)
                {
                    target.Opacity = alphaValue;
                }
            }

            return true;
        }
    }

    public Vector3 ShownViewOffset;
    public Vector3 HiddenViewOffset;
    public float RotationDeadZoneAngle = 5.0f;
    public bool IsLowered = false;
    public float Epsilon = 0.001f;
    public float Speed = 5.0f;
    public float ShownTopAngle = 5.0f;
    public float ShownBottomAngle = 46.0f;
    public float HiddenTopAngle = 35.0f;
    public float HiddenBottomAngle = 76.0f;
    public float OutOfViewRecenterTime = 1.0f;

    public AnimationCurve FadeInTransitionCurve;
    public AnimationCurve FadeOutTransitionCurve;
    public float FadeTransitionDuration = 2;

    private float lastRotation = -360.0f; // uninitialized value
    private Vector3 lastRotationVector;
    private float outOfViewTimer = 0.0f;

    private ToolsFader toolsFader;

    private bool runningInEditor = false;

    private void Awake()
    {
#if UNITY_EDITOR
        runningInEditor = true;
#endif
        var fadersGo = new GameObject("Tools Fader");
        fadersGo.transform.SetParent(transform, worldPositionStays: false);
        toolsFader = fadersGo.AddComponent<ToolsFader>();

        toolsFader.fadeTargets = GetComponentsInChildren<IFadeTarget>();
    }

    private void OnEnable()
    {
        RecenterTools(Camera.main.transform);
    }

    public IEnumerator FadeOut(bool instant)
    {
        if (instant)
        {
            toolsFader.SetAlpha(0);
        }
        else
        {
            yield return StartCoroutine(TransitionManager.Instance.FadeContent(toolsFader.gameObject, TransitionManager.FadeType.FadeOut, FadeTransitionDuration, FadeOutTransitionCurve));
        }
    }

    public IEnumerator FadeIn()
    {
        yield return StartCoroutine(TransitionManager.Instance.FadeContent(toolsFader.gameObject, TransitionManager.FadeType.FadeIn, FadeTransitionDuration, FadeInTransitionCurve));
    }

    private void Update()
    {
        if (Camera.main == null)
        {
            return;
        }

        if (GE_SpectatorViewManager.SpectatorViewEnabled && !GE_SpectatorViewManager.Instance.IsHoloLensUser)
        {
            // In Spectator View, the HoloLens user updates the tool panel's position.
            return;
        }

        Transform controllingTransform = Camera.main.transform;

        Vector3 desiredRotationVector = Quaternion.AngleAxis(controllingTransform.rotation.eulerAngles.y, Vector3.up) * Vector3.forward;
        Vector3 verticalLook = Quaternion.AngleAxis(controllingTransform.rotation.eulerAngles.x, controllingTransform.right) * controllingTransform.forward;

        float angle = Vector3.Angle(desiredRotationVector, verticalLook);

        // detect if the tool panel is in the user's view, if it isn't, start a timer to
        //  recenter it so it is directly in front of them when they look back at it
        if ((IsLowered && (angle < HiddenTopAngle || angle > HiddenBottomAngle)) ||
            (!IsLowered && (angle < ShownTopAngle || angle > ShownBottomAngle)) || verticalLook.y > 0)
        {
            outOfViewTimer += Time.deltaTime;

            if (outOfViewTimer >= OutOfViewRecenterTime)
            {
                RecenterTools(controllingTransform);
                outOfViewTimer = OutOfViewRecenterTime;
            }
        }
        else
        {
            // if the user is looking at the toolbar, reset the timer
            outOfViewTimer = 0.0f;
        }

        if (lastRotation == -360.0f) // uninitialized value
        {
            RecenterTools(controllingTransform);
        }
        else
        {
            float angleDifference = SignedAngle(lastRotationVector, desiredRotationVector, Vector3.up);
            float unsignedAngleDifference = Mathf.Abs(angleDifference);

            if (unsignedAngleDifference > RotationDeadZoneAngle)
            {
                lastRotation += Mathf.Sign(angleDifference) * (unsignedAngleDifference - RotationDeadZoneAngle);
                lastRotationVector = Quaternion.AngleAxis(lastRotation, Vector3.up) * Vector3.forward;
            }
        }

        Quaternion panelRotation = Quaternion.Euler(0.0f, lastRotation, 0.0f);
        UpdatePosition(controllingTransform, panelRotation);

        // Now that we know the position, tell the rest of the SpectatorView participants.
        if (GE_SpectatorViewManager.SpectatorViewEnabled && GE_SpectatorViewManager.Instance.IsHoloLensUser)
        {
            GE_SpectatorViewManager.Instance.SendUpdateTransform(
                transform,
                GE_SpectatorViewManager.TransformToUpdate.Tools,
                GE_SpectatorViewManager.TransformUpdateFlags.Position |
                GE_SpectatorViewManager.TransformUpdateFlags.Rotation);
        }
    }

    private void RecenterTools(Transform controllingTransform)
    {
        float desiredRotation = controllingTransform.rotation.eulerAngles.y;
        lastRotation = desiredRotation;
        lastRotationVector = Quaternion.AngleAxis(desiredRotation, Vector3.up) * Vector3.forward;
    }

    private void UpdatePosition(Transform controllingTransform, Quaternion rotation)
    {
        Vector3 targetPos = controllingTransform.position + (rotation * (IsLowered ? HiddenViewOffset : ShownViewOffset));

        if (Vector3.Distance(targetPos, transform.position) > Epsilon)
        {
            transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * Speed);
        }
    }

    private float SignedAngle(Vector3 vector1, Vector3 vector2, Vector3 positiveNormal)
    {
        Vector3 vectorCrossProduct = Vector3.Cross(vector1, vector2);
        float sign = Mathf.Sign(Vector3.Dot(vectorCrossProduct, positiveNormal));
        return sign * Vector3.Angle(vector1, vector2);
    }
}
