// Copyright Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using GalaxyExplorer.SpectatorView.Extensions;
using HoloToolkit.Sharing;
using HoloToolkit.Unity;
using SpectatorView;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VR.WSA;

namespace GalaxyExplorer.SpectatorView
{
    public class GE_SpectatorViewManager : Singleton<GE_SpectatorViewManager>
    {
        public static bool SpectatorViewEnabled = false;
        [HideInInspector]
        public bool SpectatorViewParticipantsReady = false;

        private enum TestMessageID : byte
        {
            // Spectator view messages
            SpectatorViewPlayersReady = MessageID.UserMessageIDStart,
            AnchorLocated,
            // Introduction flow messages
            AdvanceIntroduction,
            IntroductionEarthPlaced,
            // Application navigation messages
            SceneTransitionForward,
            SceneTransitionBackward,
            ToggleSolarSystemOrbitScale,
            PointOfInterestGazeSelect,
            PointOfInterestAnimateDescription,
            PointOfInterestCardTapped,
            HideAllCards,
            // UI messages
            UpdateCursorTransform,
            // Tool messages
            MoveCube,
            ContentPlaced,
            UpdateTransform,
            SelectToolbarButton,
            UpdateCurrentContentLocalScale,
            UpdateCurrentContentRotation,
            ToggleTools,
            ResetView,
            // Last message (unused)
            Max
        }

        private delegate void MessageCallback(NetworkInMessage msg);
        private Dictionary<TestMessageID, MessageCallback> MessageHandlers { set; get; }

        private Dictionary<long, bool> LocatedAnchors { set; get; }

        private NetworkConnection serverConnection;
        private NetworkConnectionAdapter connectionAdapter;

        private long LocalUserId
        {
            get { return (long)SharingStage.Instance.Manager.GetLocalUser().GetID(); }
        }

        protected override void Awake()
        {
            base.Awake();
            MessageHandlers = new Dictionary<TestMessageID, MessageCallback>();
            LocatedAnchors = new Dictionary<long, bool>();
            SpectatorViewEnabled = true;
        }

        private IEnumerator Start()
        {
            // wait until the SpectatorView Components are loaded before
            // exiting start.
            while (!SpectatorViewLoader.Instance.SpectatorViewLoaded)
            {
                yield return new WaitForEndOfFrame();
            }
            while (!SV_CustomMessages.Instance)
            {
                yield return new WaitForEndOfFrame();
            }
            while (!SV_CustomMessages.Instance.Initialized)
            {
                yield return new WaitForEndOfFrame();
            }

            serverConnection = SharingStage.Instance.Manager.GetServerConnection();

            connectionAdapter = new NetworkConnectionAdapter();
            connectionAdapter.MessageReceivedCallback += ConnectionAdapter_OnMessageReceived;

            for (byte index = (byte)TestMessageID.SpectatorViewPlayersReady; index < (byte)TestMessageID.Max; index++)
            {
                if (MessageHandlers.ContainsKey((TestMessageID)index) == false)
                {
                    MessageHandlers.Add((TestMessageID)index, null);
                }

                serverConnection.AddListener(index, connectionAdapter);
            }

            MessageHandlers[TestMessageID.SpectatorViewPlayersReady] = OnSpectatorViewPlayersReady;
            MessageHandlers[TestMessageID.AnchorLocated] = OnAnchorLocated;
            MessageHandlers[TestMessageID.AdvanceIntroduction] = OnAdvanceIntroduction;
            MessageHandlers[TestMessageID.IntroductionEarthPlaced] = OnIntroductionEarthPlaced;
            MessageHandlers[TestMessageID.SceneTransitionForward] = OnSceneTransitionForward;
            MessageHandlers[TestMessageID.SceneTransitionBackward] = OnSceneTransitionBackward;
            MessageHandlers[TestMessageID.ToggleSolarSystemOrbitScale] = OnToggleSolarSystemOrbitScale;
            MessageHandlers[TestMessageID.PointOfInterestGazeSelect] = OnPointOfInterestGazeSelect;
            MessageHandlers[TestMessageID.PointOfInterestAnimateDescription] = OnPointOfInterestAnimateDescription;
            MessageHandlers[TestMessageID.PointOfInterestCardTapped] = OnPointOfInterestCardTapped;
            MessageHandlers[TestMessageID.HideAllCards] = OnHideAllCards;
            MessageHandlers[TestMessageID.UpdateCursorTransform] = OnUpdateCursorTransform;
            MessageHandlers[TestMessageID.MoveCube] = OnMoveCube;
            MessageHandlers[TestMessageID.ContentPlaced] = OnContentPlaced;
            MessageHandlers[TestMessageID.UpdateTransform] = OnUpdateTransform;
            MessageHandlers[TestMessageID.SelectToolbarButton] = OnSelectToolbarButton;
            MessageHandlers[TestMessageID.UpdateCurrentContentLocalScale] = OnUpdateCurrentContentLocalScale;
            MessageHandlers[TestMessageID.UpdateCurrentContentRotation] = OnUpdateCurrentContentRotation;
            MessageHandlers[TestMessageID.ToggleTools] = OnToggleTools;
            MessageHandlers[TestMessageID.ResetView] = OnResetView;

            StartCoroutine(WaitForSpectatorViewParticipantsAsync());
        }

        private IEnumerator WaitForSpectatorViewParticipantsAsync()
        {
            var hcmInstance = HolographicCameraManager.Instance;

            // we only do this waiting if we are the SpectatorView camera rig
            if ((hcmInstance != null) && hcmInstance.IsHolographicCameraRig())
            {
                // wait until we have three players (SpectatorView rig, editor and HoloLens user)
                while (true)
                {
                    if (SharingSessionTracker.Instance.UserIds.Count >= 3)
                    {
                        if ((hcmInstance.editorUser != null))
                        {
                            Debug.Log("### We have all SV participants ###");
                            break;
                        }
                    }
                    Debug.Log(string.Format("Editor User: {0}", (hcmInstance.editorUser == null) ? "NULL" : hcmInstance.editorUser.GetName().ToString()));
                    yield return new WaitForSeconds(1);
                }

                // wait until all three anchors have been located for the SpectatorView rig and HoloLens
                while (true)
                {
                    if (LocatedAnchors.Count >= 2)
                    {
                        Debug.Log("### All anchors reported as located");
                        break;
                    }
                    yield return new WaitForSeconds(1);
                }

                // now we are ready!
                SendSpectatorViewPlayersReady();
            }
        }

        public IEnumerator WaitForAnchorToBeLocated()
        {
            // wait until we have an Anchor
            WorldAnchor anchor = SV_ImportExportAnchorManager.Instance.GetComponent<WorldAnchor>();
            while (anchor == null)
            {
                yield return null;
                anchor = SV_ImportExportAnchorManager.Instance.GetComponent<WorldAnchor>();
            }

            // wait until that anchor is located
            while (!anchor.isLocated)
            {
                yield return null;
            }

            SendAnchorLocated();
        }


        public bool IsHoloLensUser
        {
            get { return HolographicCameraManager.Instance.IsHoloLensUser(); }
        }

        public static bool TryGetHoloLensUserTransform(ref Transform transform)
        {
            if (!SpectatorViewEnabled || !HolographicCameraManager.Instance)
            {
                return false;
            }

            User remoteUser = null;
            if (!HolographicCameraManager.Instance.TryGetHoloLensUser(ref remoteUser))
            {
                return false;
            }

            transform = SV_RemotePlayerManager.Instance.GetRemoteHeadInfo(remoteUser.GetID()).HeadObject.transform;
            return true;
        }

        public static bool TryGetHoloLensUserGazeRay(ref Ray retRay)
        {
            if (!SpectatorViewEnabled || !HolographicCameraManager.Instance)
            {
                return false;
            }

            User remoteUser = null;
            if (!HolographicCameraManager.Instance.TryGetHoloLensUser(ref remoteUser))
            {
                return false;
            }

            var remoteHead = SV_RemotePlayerManager.Instance.GetRemoteHeadInfo(remoteUser.GetID()).HeadObject;

            retRay.origin = remoteHead.transform.position;
            retRay.direction = remoteHead.transform.forward;

            return true;
        }

        #region // event handlers
        private void OnAdvanceIntroduction(NetworkInMessage msg)
        {
            if (msg.ReadInt64() != LocalUserId)
            {
                Debug.Log("OnAdvanceIntroduction");
                InputRouter.Instance.OnTapped(UnityEngine.VR.WSA.Input.InteractionSourceKind.Other, 1, new Ray());
            }
        }

        public void SendAdvanceIntroduction()
        {
            if (IsHoloLensUser)
            {
                Debug.Log("SendAdvanceIntroduction");
                // due to timing issues, whe might need to eventually send the
                // current Introduction Flow state
                SendBasicStateChangeMessage(TestMessageID.AdvanceIntroduction);
            }
        }

        private void OnAnchorLocated(NetworkInMessage msg)
        {
            long messageSender = msg.ReadInt64();
            if (messageSender != LocalUserId)
            {
                Debug.Log(string.Format("OnAnchorLocated for user {0}", messageSender));
                LocatedAnchors[messageSender] = true;
            }
        }

        private void SendAnchorLocated()
        {
            Debug.Log("SendAnchorLocated");
            LocatedAnchors[LocalUserId] = true;
            SendBasicStateChangeMessage(TestMessageID.AnchorLocated);
        }

        private void OnContentPlaced(NetworkInMessage msg)
        {
            if (msg.ReadInt64() != LocalUserId)
            {
                Debug.Log("OnContentPlaced");
                PlacementControl pc = TransitionManager.Instance.ViewVolume.GetComponentInChildren<PlacementControl>();
                if (pc)
                {
                    pc.OnTapped(UnityEngine.VR.WSA.Input.InteractionSourceKind.Other, 1, new Ray());
                }
            }
        }

        public void SendContentPlaced()
        {
            if (IsHoloLensUser)
            {
                Debug.Log("OnContentPlaced");
                // send the final position of the volume
                TransformUpdateFlags flags = TransformUpdateFlags.Position | TransformUpdateFlags.Rotation;
                SendUpdateTransform(TransformToUpdate.Volume, flags);
                SendBasicStateChangeMessage(TestMessageID.ContentPlaced);
            }
        }

        private void OnHideAllCards(NetworkInMessage msg)
        {
            if (msg.ReadInt64() != LocalUserId)
            {
                Debug.Log("OnHideAllCards");
                CardPOIManager.Instance.HideAllCards();
            }
        }

        public void SendHideAllCards()
        {
            if (IsHoloLensUser)
            {
                Debug.Log("SendHideAllCards");
                SendBasicStateChangeMessage(TestMessageID.HideAllCards);
            }
        }

        private void OnIntroductionEarthPlaced(NetworkInMessage msg)
        {
            Debug.Log("OnEarthPlaced");
            ToolManager.Instance.UnlockTools();
            Cursor.Instance.ClearToolState();
        }

        public void SendIntroductionEarthPlaced()
        {
            if (IsHoloLensUser)
            {
                Debug.Log("SendIntroductionEarthPlaced");
                SendBasicStateChangeMessage(TestMessageID.IntroductionEarthPlaced);
            }
        }

        private void OnMoveCube(NetworkInMessage msg)
        {
            if (msg.ReadInt64() != LocalUserId)
            {
                Debug.Log("OnMoveCube");
                ToolManager.Instance.LockTools();
                TransitionManager.Instance.ViewVolume.GetComponentInChildren<PlacementControl>().TogglePinnedState();
            }
        }

        public void SendMoveCube()
        {
            if (IsHoloLensUser)
            {
                Debug.Log("SendMoveCube");
                SendBasicStateChangeMessage(TestMessageID.MoveCube);
            }
        }

        private void OnPointOfInterestAnimateDescription(NetworkInMessage msg)
        {
            if (msg.ReadInt64() != LocalUserId)
            {
                string descriptionName = msg.ReadString().ToString();
                Debug.Log(string.Format("OnPointOfInterestAnimateDescription for {0}", descriptionName));
                string property = msg.ReadString().ToString();
                bool value = msg.ReadByte() == 1;

                Animator[] animators = ViewLoader.Instance.GetComponentsInChildren<Animator>();
                foreach (Animator ani in animators)
                {
                    if (ani.gameObject.name.Equals(descriptionName))
                    {
                        ani.SetBool(property, value);
                        break;
                    }
                }
            }
        }

        public void SendPointOfInterestAnimateDescription(string descriptionName, string property, bool value)
        {
            if (IsHoloLensUser)
            {
                Debug.Log(string.Format("SendPointOfInterestAnimateDescription({0}, {1})", descriptionName, value.ToString()));
                NetworkOutMessage msg = CreateMessage(TestMessageID.PointOfInterestAnimateDescription);
                msg.Write(new XString(descriptionName));
                msg.Write(new XString(property));
                msg.Write((byte)(value ? 1 : 0));
                serverConnection.Broadcast(msg);
            }
        }

        private void OnPointOfInterestCardTapped(NetworkInMessage msg)
        {
            if (msg.ReadInt64() != LocalUserId)
            {
                string poiName = msg.ReadString().ToString();
                Debug.Log(string.Format("OnPointOfInterestCardTapped: {0}", poiName));
                CardPointOfInterest[] cards = ViewLoader.Instance.GetComponentsInChildren<CardPointOfInterest>();
                bool cardFound = false;
                foreach (var card in cards)
                {
                    var cardParent = card.gameObject.transform.parent;
                    var cardParentParent = cardParent.gameObject.transform.parent;
                    if (cardParentParent.name.Equals(poiName))
                    {
                        cardFound = true;
                        card.OnTapped(UnityEngine.VR.WSA.Input.InteractionSourceKind.Other, 1, new Ray());
                        break;
                    }
                }
                if (!cardFound)
                {
                    Debug.Log(string.Format("### Unable to find POI {0}", poiName));
                }
            }
        }

        public void SendPointOfInterestCardTapped(CardPointOfInterest card)
        {
            if (IsHoloLensUser)
            {
                var cardParent = card.gameObject.transform.parent;
                var cardParentParent = cardParent.gameObject.transform.parent;
                var poiName = cardParentParent.name;

                Debug.Log(string.Format("SendPointOfInterestCardTapped({0})", poiName));
                NetworkOutMessage msg = CreateMessage(TestMessageID.PointOfInterestCardTapped);
                msg.Write(new XString(poiName));
                serverConnection.Broadcast(msg, MessagePriority.High, MessageReliability.Reliable);
            }
        }

        private void OnPointOfInterestGazeSelect(NetworkInMessage msg)
        {
            if (msg.ReadInt64() != LocalUserId)
            {
                bool isSelect = msg.ReadByte() == 1;
                string nameToFind = msg.ReadString().ToString();
                Debug.Log(string.Format("OnPointOfInterestGaze{1} for POI named {0}", nameToFind, isSelect?"Select":"Deselect"));
                PointOfInterest[] points = ViewLoader.Instance.GetComponentsInChildren<PointOfInterest>();
                foreach (PointOfInterest poi in points)
                {
                    GameObject goToFind;
                    if (ViewLoader.Instance.CurrentView.Equals("GalaxyView"))
                    {
                        goToFind = poi.transform.parent.parent.gameObject;
                    }
                    else
                    {
                        // Center of Galaxy and Solar System views
                        goToFind = poi.gameObject;
                    }
                    if (goToFind.name.Equals(nameToFind))
                    {
                        if (isSelect)
                        {
                            poi.OnGazeSelect();
                        }
                        else
                        {
                            poi.OnGazeDeselect();
                        }
                        break;
                    }
                }
            }
        }

        public void SendPointOfInterestGazeSelect(string nameToFind, bool isSelect)
        {
            if (IsHoloLensUser)
            {
                Debug.Log(string.Format("SendPointOfInterestGazeSelect({0}, {1})", nameToFind, isSelect.ToString()));
                var msg = CreateMessage(TestMessageID.PointOfInterestGazeSelect);
                msg.Write((byte)(isSelect ? 1 : 0));
                msg.Write(new XString(nameToFind));
                serverConnection.Broadcast(msg, MessagePriority.High, MessageReliability.Reliable);
            }
        }

        private void OnResetView(NetworkInMessage msg)
        {
            if (msg.ReadInt64() != LocalUserId)
            {
                Debug.Log("OnReset");
                TransitionManager.Instance.ResetView();
            }
        }

        public void SendResetview()
        {
            if (IsHoloLensUser)
            {
                Debug.Log("SendResetView");
                SendBasicStateChangeMessage(TestMessageID.ResetView);
            }
        }

        private void OnSceneTransitionBackward(NetworkInMessage msg)
        {
            Debug.Log("OnSceneTransitionBackward");
            ViewLoader.Instance.GoBack();
        }

        public void SendSceneTransitionBackward()
        {
            if (IsHoloLensUser)
            {
                Debug.Log("SendSceneTransitionBackward");
                SendBasicStateChangeMessage(TestMessageID.SceneTransitionBackward);
            }
        }

        private void OnSceneTransitionForward(NetworkInMessage msg)
        {
            if (msg.ReadInt64() != LocalUserId)
            {
                string sceneName = msg.ReadString().ToString();
                string transitionSourceObjectName = msg.ReadString().ToString();
                Debug.Log(string.Format("OnSceneTransitionForward: to {0}; from {1}", sceneName, transitionSourceObjectName));

                // try to find an object with the provided name in the hierarchy
                bool foundSourceObject = false;
                foreach (PointOfInterest poi in ViewLoader.Instance.GetComponentsInChildren<PointOfInterest>())
                {
                    if (poi.name.Equals(transitionSourceObjectName))
                    {
                        foundSourceObject = true;
                        TransitionManager.Instance.LoadNextScene(sceneName, poi.gameObject);
                        break;
                    }
                }
                if (!foundSourceObject)
                {
                    Debug.Log(string.Format("Could not find PointOfInterest {0} to transtion to scene {1}", transitionSourceObjectName, sceneName));
                }
            }
        }

        public void SendSceneTransitionForward(string sceneName, GameObject transitionSourceObject)
        {
            if (IsHoloLensUser)
            {
                string transitionSourceObjectName = string.Empty;
                if (transitionSourceObject)
                {
                    transitionSourceObjectName = transitionSourceObject.name;
                }

                Debug.Log(string.Format("SendSceneTransitionForward({0}, {1}", sceneName, transitionSourceObjectName));
                NetworkOutMessage msg = CreateMessage(TestMessageID.SceneTransitionForward);
                msg.Write(new XString(sceneName));
                msg.Write(new XString(transitionSourceObjectName));
                serverConnection.Broadcast(msg, MessagePriority.High, MessageReliability.Reliable);
            }
        }

        private void OnSelectToolbarButton(NetworkInMessage msg)
        {
            if (msg.ReadInt64() != LocalUserId)
            {
                ToolType toolType = (ToolType)msg.ReadByte();
                Debug.Log(string.Format("OnSelectToolbarButton: {0}", toolType.ToString()));
                Tool[] tools = ToolManager.Instance.GetComponentsInChildren<Tool>();
                foreach (Tool tool in tools)
                {
                    if (tool.type == toolType)
                    {
                        tool.OnTapped(UnityEngine.VR.WSA.Input.InteractionSourceKind.Other, 1, new Ray());
                    }
                }
            }
        }

        public void SendSelectToolbarButton(ToolType tool)
        {
            if (IsHoloLensUser)
            {
                Debug.Log(string.Format("SendToolbarButton({0})", tool.ToString()));
                NetworkOutMessage msg = CreateMessage(TestMessageID.SelectToolbarButton);
                msg.Write((byte)tool);
                serverConnection.Broadcast(msg, MessagePriority.Medium, MessageReliability.Reliable);
            }
        }

        private void OnSpectatorViewPlayersReady(NetworkInMessage msg)
        {
            Debug.Log("OnSpectatorViewPlayersReady");
            SpectatorViewParticipantsReady = true;
        }

        public void SendSpectatorViewPlayersReady()
        {
            // Only the Spectator View CameraRig should send this message
            if (HolographicCameraManager.Instance.IsHolographicCameraRig())
            {
                Debug.Log("SendSpectatorViewPlayersReady");
                SendBasicStateChangeMessage(TestMessageID.SpectatorViewPlayersReady);
                SpectatorViewParticipantsReady = true;
            }
        }

        private void OnToggleSolarSystemOrbitScale(NetworkInMessage msg)
        {
            if (msg.ReadInt64() != LocalUserId)
            {
                Debug.Log("OnToggleSolarSystemOrbitalScale");
                var Anchor = SV_ImportExportAnchorManager.Instance.gameObject;
                OrbitScalePointOfInterest ospoi = Anchor.GetComponentInChildren<OrbitScalePointOfInterest>();
                ospoi.OnTapped(UnityEngine.VR.WSA.Input.InteractionSourceKind.Other, 1, new Ray());
            }
        }

        public void SendToggleSolarSystemOrbitScale()
        {
            if (IsHoloLensUser)
            {
                Debug.Log("SendToggleSolarSystemOrbitScale");
                SendBasicStateChangeMessage(TestMessageID.ToggleSolarSystemOrbitScale);
            }
        }

        private void OnToggleTools(NetworkInMessage msg)
        {
            if (msg.ReadInt64() != LocalUserId)
            {
                Debug.Log("OnToggleTools");
                ToolManager.Instance.ToggleTools();
            }
        }

        public void SendToggleTools()
        {
            if (IsHoloLensUser)
            {
                Debug.Log("SendToggleTools");
                SendBasicStateChangeMessage(TestMessageID.ToggleTools);
            }
        }

        private void OnUpdateCurrentContentRotation(NetworkInMessage msg)
        {
            if (msg.ReadInt64() != LocalUserId)
            {
                //Debug.Log("OnUpdateCurrentContentRotation");
                var newRot = msg.ReadQuaternion();
                ViewLoader.Instance.GetCurrentContent().transform.rotation = newRot;
            }
        }

        public void SendUpdateCurrentContentRotation(Quaternion rot)
        {
            if (IsHoloLensUser)
            {
                //Debug.Log("SendUpdateCurrentContentRotation");
                NetworkOutMessage msg = CreateMessage(TestMessageID.UpdateCurrentContentRotation);
                msg.Write(rot);
                serverConnection.Broadcast(msg, MessagePriority.Medium, MessageReliability.Reliable);
            }
        }

        private void OnUpdateCurrentContentLocalScale(NetworkInMessage msg)
        {
            if (msg.ReadInt64() != LocalUserId)
            {
                //Debug.Log("OnUpdateCurrentContentLocalScale");
                var newScale = msg.ReadVector3();
                ViewLoader.Instance.GetCurrentContent().transform.localScale = newScale;
            }
        }

        public void SendUpdateCurrentContentLocalScale(Vector3 scale)
        {
            if (IsHoloLensUser)
            {
                //Debug.Log("SendUpdateCurrentContentLocalScale");
                NetworkOutMessage msg = CreateMessage(TestMessageID.UpdateCurrentContentLocalScale);
                msg.Write(scale);
                serverConnection.Broadcast(msg, MessagePriority.Medium, MessageReliability.Reliable);
            }
        }

        public enum TransformUpdateFlags : byte
        {
            Position = 1,
            Rotation = 2,
            LocalPosition = 4,
            LocalRotation = 8,
            LocalScale = 16
        }

        public enum TransformToUpdate : byte
        {
            Volume,
            Cursor,
            Tools
        }

        private Transform GetRelativeTransform(TransformToUpdate transEnum)
        {
            if (transEnum == TransformToUpdate.Volume || transEnum == TransformToUpdate.Tools)
            {
                // Send the Volume relative to the Anchor transform
                return SV_ImportExportAnchorManager.Instance.transform;
            }
            else
            {
                // Send everything else (Cursor and Tools) relative to the Volume transform
                return TransitionManager.Instance.ViewVolume.transform;
            }
        }

        private void OnUpdateTransform(NetworkInMessage msg)
        {
            if (msg.ReadInt64() != LocalUserId)
            {
                //Debug.Log("OnUpdateTransform");

                TransformToUpdate transEnum = (TransformToUpdate)msg.ReadByte();
                Transform transform = null;
                switch (transEnum)
                {
                    case TransformToUpdate.Cursor: transform = Cursor.Instance.transform; break;
                    case TransformToUpdate.Volume: transform = TransitionManager.Instance.ViewVolume.transform; break;
                    case TransformToUpdate.Tools: transform = ToolManager.Instance.transform; break;
                }

                TransformUpdateFlags flags = (TransformUpdateFlags)msg.ReadByte();

                if ((flags & TransformUpdateFlags.Position) != 0)
                {
                    var position = msg.ReadVector3();
                    transform.position = GetRelativeTransform(transEnum).TransformPoint(position);
                }
                if ((flags & TransformUpdateFlags.Rotation) != 0)
                {
                    transform.rotation = msg.ReadQuaternion();
                }
                if ((flags & TransformUpdateFlags.LocalPosition) != 0)
                {
                    transform.localPosition = msg.ReadVector3();

                }
                if ((flags & TransformUpdateFlags.LocalRotation) != 0)
                {
                    var localRot = msg.ReadQuaternion();
                }
                if ((flags & TransformUpdateFlags.LocalScale) != 0)
                {
                    transform.localScale = msg.ReadVector3();
                }
            }
        }

        public void SendUpdateTransform(TransformToUpdate transEnum, TransformUpdateFlags flags)
        {
            if (IsHoloLensUser)
            {
                //Debug.Log("SendUpdateTransform");
                NetworkOutMessage msg = CreateMessage(TestMessageID.UpdateTransform);
                msg.Write((byte)transEnum);
                msg.Write((byte)flags);

                Transform transform = null;
                switch (transEnum)
                {
                    case TransformToUpdate.Cursor: transform = Cursor.Instance.transform; break;
                    case TransformToUpdate.Volume: transform = TransitionManager.Instance.ViewVolume.transform; break;
                    case TransformToUpdate.Tools: transform = ToolManager.Instance.transform; break;
                }

                if ((flags & TransformUpdateFlags.Position) != 0)
                {
                    msg.Write(GetRelativeTransform(transEnum).InverseTransformPoint(transform.position));
                }
                if ((flags & TransformUpdateFlags.Rotation) != 0)
                {
                    msg.Write(transform.rotation);
                }
                if ((flags & TransformUpdateFlags.LocalPosition) != 0)
                {
                    msg.Write(transform.localPosition);
                }
                if ((flags & TransformUpdateFlags.LocalRotation) != 0)
                {
                    msg.Write(transform.localRotation);
                }
                if ((flags & TransformUpdateFlags.LocalScale) != 0)
                {
                    msg.Write(transform.localScale);
                }
                serverConnection.Broadcast(msg, MessagePriority.Medium, MessageReliability.Reliable);
            }
        }

#endregion // event handlers

#region // message helpers
        private void SendBasicStateChangeMessage(TestMessageID messageId)
        {
            NetworkOutMessage msg = CreateMessage(messageId);
            serverConnection.Broadcast(msg, MessagePriority.High, MessageReliability.Reliable);
        }

        private NetworkOutMessage CreateMessage(TestMessageID messageType)
        {
            NetworkOutMessage msg = serverConnection.CreateMessage((byte)messageType);
            msg.Write((byte)messageType);
            // Add the local userID so that the remote clients know whose message they are receiving
            msg.Write(LocalUserId);
            return msg;
        }

        private void ConnectionAdapter_OnMessageReceived(NetworkConnection connection, NetworkInMessage msg)
        {
            byte messageType = msg.ReadByte();
            MessageCallback messageHandler = MessageHandlers[(TestMessageID)messageType];
            if (messageHandler != null)
            {
                messageHandler(msg);
            }
        }
#endregion

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (connectionAdapter != null)
            {
                connectionAdapter.MessageReceivedCallback -= ConnectionAdapter_OnMessageReceived;
            }
        }
    }
}

namespace GalaxyExplorer.SpectatorView.Extensions
{
    public static class NetworkOutMessageExt
    {
        public static void Write(this NetworkOutMessage msg, Vector3 vector)
        {
            msg.Write(vector.x);
            msg.Write(vector.y);
            msg.Write(vector.z);
        }

        public static void Write(this NetworkOutMessage msg, Quaternion rotation)
        {
            msg.Write(rotation.x);
            msg.Write(rotation.y);
            msg.Write(rotation.z);
            msg.Write(rotation.w);
        }
    }

    public static class NetworkInMessageExt
    {
        public static Vector3 ReadVector3(this NetworkInMessage msg)
        {
            return new Vector3(msg.ReadFloat(), msg.ReadFloat(), msg.ReadFloat());
        }

        public static Quaternion ReadQuaternion(this NetworkInMessage msg)
        {
            return new Quaternion(msg.ReadFloat(), msg.ReadFloat(), msg.ReadFloat(), msg.ReadFloat());
        }
    }

    public static class HolographicCameraManagerExt
    {
        public static bool IsHolographicCameraRig(this HolographicCameraManager hcm)
        {
            return hcm.localIPs.Contains(hcm.HolographicCameraIP.Trim());
        }

        public static bool IsHoloLensUser(this HolographicCameraManager hcm)
        {
            if (holoLensUser == null)
            {
                if (!hcm.TryGetHoloLensUser(ref holoLensUser))
                {
                    return false;
                }
            }

            return holoLensUser.GetID() == SharingStage.Instance.Manager.GetLocalUser().GetID();
        }

        private static User holoLensUser = null;
        public static bool TryGetHoloLensUser(this HolographicCameraManager hcm, ref User user)
        {
            // if we've already determined the HoloLens user, return that cached value.
            if (holoLensUser != null)
            {
                user = holoLensUser;
                return true;
            }

            // If our singletons haven't been initialized yet, return false.
            if (!hcm ||
                !SharingSessionTracker.Instance ||
                SharingStage.Instance.Manager == null)
            {
                return false;
            }

            // get the user ID's and bail out if there are too few
            var userIds = SharingSessionTracker.Instance.UserIds;
            if (userIds.Count < 3)
            {
                return false;
            }

            // find the HoloLens user amongst the connected users
            for (int i = 0; i < userIds.Count; i++)
            {
                long userId = userIds[i];
#if UNITY_EDITOR
                // If we are running inside the Unity editor, we can skip the LocalUser because that's us
                if (userId == SharingStage.Instance.Manager.GetLocalUser().GetID())
                {
                    continue;
                }
                // We can also skip the tppcUser because that's the Spectator View camera rig
                if (hcm.tppcUser == null)
                {
                    return false;
                }
                else if (userId == hcm.tppcUser.GetID())
                {
                    continue;
                }
#else
                // we aren't running as the editor, check to see if we are the Spectator View camera rig
                if (hcm.IsHolographicCameraRig())
                {
                    // if we are, we can skip the Local User because that's us
                    if (userId == SharingStage.Instance.Manager.GetLocalUser().GetID())
                    {
                        continue;
                    }
                    // We can also skip the editor user for obvious reasons
                    if (hcm.editorUser == null)
                    {
                        return false;
                    }
                    else if (userId == hcm.editorUser.GetID())
                    {
                        continue;
                    }
                }
                else
                {
                    // if we aren't the editorUser or the Spectator View camera rig...
                    holoLensUser = SharingStage.Instance.Manager.GetLocalUser();
                    break;
                }
#endif
                // The only thing left is the HoloLens user so return that.
                holoLensUser = SharingSessionTracker.Instance.GetUserById(userId);
                break;
            }

            user = holoLensUser;
            return true;
        }
    }
}