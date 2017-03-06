using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using HoloToolkit.Unity;
using HoloToolkit.Sharing;
using GalaxyExplorer_SpectatorView.Extensions;

namespace GalaxyExplorer_SpectatorView
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
            // Introduction flow messages
            AdvanceIntroduction,
            IntroductionEarthPlaced,
            // Application navigation messages
            SceneTransitionForward,
            SceneTransitionBackward,
            ToggleSolarSystemOrbitScale,
            PointOfInterestCardTapped,
            HideAllCards,
            // Movement messages
            MoveCube,
            UpdateVolumeTransform,
            // Last message (unused)
            Max
        }

        private delegate void MessageCallback(NetworkInMessage msg);
        private Dictionary<TestMessageID, MessageCallback> MessageHandlers
        {
            set;
            get;
        }

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
            while (!SpectatorView.SV_CustomMessages.Instance)
            {
                yield return new WaitForEndOfFrame();
            }
            while (!SpectatorView.SV_CustomMessages.Instance.Initialized)
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
            MessageHandlers[TestMessageID.AdvanceIntroduction] = OnAdvanceIntroduction;
            MessageHandlers[TestMessageID.IntroductionEarthPlaced] = OnIntroductionEarthPlaced;
            MessageHandlers[TestMessageID.SceneTransitionForward] = OnSceneTransitionForward;
            MessageHandlers[TestMessageID.SceneTransitionBackward] = OnSceneTransitionBackward;
            MessageHandlers[TestMessageID.ToggleSolarSystemOrbitScale] = OnToggleSolarSystemOrbitScale;
            MessageHandlers[TestMessageID.PointOfInterestCardTapped] = OnPointOfInterestCardTapped;
            MessageHandlers[TestMessageID.HideAllCards] = OnHideAllCards;
            MessageHandlers[TestMessageID.MoveCube] = OnMoveCube;
            MessageHandlers[TestMessageID.UpdateVolumeTransform] = OnUpdateVolumeTransform;

            StartCoroutine(WaitForSpectatorViewParticipantsAsync());
        }

        private IEnumerator WaitForSpectatorViewParticipantsAsync()
        {
            var hcmInstance = SpectatorView.HolographicCameraManager.Instance;

            // we only do this waiting if we are the SpectatorView camera rig
            if ((hcmInstance != null) && hcmInstance.IsHolographicCameraRig())
            {
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

                SendOnSpectatorViewPlayersReady();
            }
        }

        public bool IsHoloLensUser
        {
            get { return SpectatorView.HolographicCameraManager.Instance.IsHoloLensUser(); }
        }

        public static Transform GetHoloLensUserTransform(Transform defaultTransform)
        {
            if (!SpectatorViewEnabled || !SpectatorView.HolographicCameraManager.Instance)
            {
                //Debug.Log("GetHoloLensUserTransform: Returning defaultTransform(1)");
                return defaultTransform;
            }

            var remoteUser = SpectatorView.HolographicCameraManager.Instance.GetHoloLensUser();

            if (remoteUser == null)
            {
                //Debug.Log("GetHoloLensUserTransform: Returning defaultTransform(2)");
                return defaultTransform;
            }

            return SpectatorView.SV_RemotePlayerManager.Instance.GetRemoteHeadInfo(remoteUser.GetID()).HeadObject.transform;
        }

        public static Ray GetHoloLensUserGazeRay(Ray defaultRay, float offsetFromOrigin)
        {
            if (!SpectatorViewEnabled || !SpectatorView.HolographicCameraManager.Instance)
            {
                //Debug.Log("GetHololensUserGazeRay: Returning defaultRay(1)");
                return defaultRay;
            }

            var remoteUser = SpectatorView.HolographicCameraManager.Instance.GetHoloLensUser();

            if (remoteUser == null)
            {
                //Debug.Log("GetHololensUserGazeRay: Returning defaultRay(2)");
                return defaultRay;
            }

            var remoteHead = SpectatorView.SV_RemotePlayerManager.Instance.GetRemoteHeadInfo(remoteUser.GetID()).HeadObject;

            Ray retRay = new Ray();

            retRay.origin = remoteHead.transform.position + offsetFromOrigin * remoteHead.transform.forward;
            retRay.direction = remoteHead.transform.forward;

            return retRay;
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

        public void SendOnAdvanceIntroduction()
        {
            if (SpectatorView.HolographicCameraManager.Instance.IsHoloLensUser())
            {
                Debug.Log("SendOnAdvanceIntroduction");
                // due to timing issues, whe might need to eventually send the
                // current Introduction Flow state
                SendBasicStateChangeMessage(TestMessageID.AdvanceIntroduction);
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

        public void SendOnHideAllCards()
        {
            if (SpectatorView.HolographicCameraManager.Instance.IsHoloLensUser())
            {
                Debug.Log("SendOnHideAllCards");
                SendBasicStateChangeMessage(TestMessageID.HideAllCards);
            }
        }

        private void OnIntroductionEarthPlaced(NetworkInMessage msg)
        {
            Debug.Log("OnEarthPlaced");
            TransitionManager.Instance.ViewVolume.GetComponentInChildren<PlacementControl>().TogglePinnedState();
        }

        public void SendOnIntroductionEarthPlaced()
        {
            if (SpectatorView.HolographicCameraManager.Instance.IsHoloLensUser())
            {
                Debug.Log("SendOnIntroductionEarthPlaced");
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

        public void SendOnMoveCube()
        {
            if (SpectatorView.HolographicCameraManager.Instance.IsHoloLensUser())
            {
                Debug.Log("SendOnMoveCube");
                SendBasicStateChangeMessage(TestMessageID.MoveCube);
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

        public void SendOnPointOfInterestCardTapped(CardPointOfInterest card)
        {
            if (SpectatorView.HolographicCameraManager.Instance.IsHoloLensUser())
            {
                var cardParent = card.gameObject.transform.parent;
                var cardParentParent = cardParent.gameObject.transform.parent;
                var poiName = cardParentParent.name;

                Debug.Log(string.Format("SendOnPointOfInterestCardTapped({0})", poiName));
                NetworkOutMessage msg = CreateMessage((byte)TestMessageID.PointOfInterestCardTapped);
                msg.Write(new XString(poiName));
                serverConnection.Broadcast(msg, MessagePriority.High, MessageReliability.Reliable);
            }
        }

        private void OnSceneTransitionBackward(NetworkInMessage msg)
        {
            Debug.Log("OnSceneTransitionBackward");
            ViewLoader.Instance.GoBack();
        }

        public void SendOnSceneTransitionBackward()
        {
            if (SpectatorView.HolographicCameraManager.Instance.IsHoloLensUser())
            {

                Debug.Log("SendOnSceneTransitionBackward");
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

        public void SendOnSceneTransitionForward(string sceneName, GameObject transitionSourceObject)
        {
            if (SpectatorView.HolographicCameraManager.Instance.IsHoloLensUser())
            {
                string transitionSourceObjectName = string.Empty;
                if (transitionSourceObject)
                {
                    transitionSourceObjectName = transitionSourceObject.name;
                }

                Debug.Log(string.Format("SendOnSceneTransitionForward({0}, {1}", sceneName, transitionSourceObjectName));
                NetworkOutMessage msg = CreateMessage((byte)TestMessageID.SceneTransitionForward);
                msg.Write(new XString(sceneName));
                msg.Write(new XString(transitionSourceObjectName));
                serverConnection.Broadcast(msg, MessagePriority.High, MessageReliability.Reliable);
            }
        }

        private void OnSpectatorViewPlayersReady(NetworkInMessage msg)
        {
            Debug.Log("OnSpectatorViewPlayersReady");
            SpectatorViewParticipantsReady = true;
        }

        public void SendOnSpectatorViewPlayersReady()
        {
            // Only the Spectator View CameraRig can send this message
            if (SpectatorView.HolographicCameraManager.Instance.IsHolographicCameraRig())
            {
                Debug.Log("SendOnSpectatorViewPlayersReady");
                SendBasicStateChangeMessage(TestMessageID.SpectatorViewPlayersReady);
                SpectatorViewParticipantsReady = true;
            }
        }

        private void OnToggleSolarSystemOrbitScale(NetworkInMessage msg)
        {
            if (msg.ReadInt64() != LocalUserId)
            {
                Debug.Log("OnToggleSolarSystemOrbitalScale");
                var Anchor = SpectatorView.SV_ImportExportAnchorManager.Instance.gameObject;
                OrbitScalePointOfInterest ospoi = Anchor.GetComponentInChildren<OrbitScalePointOfInterest>();
                ospoi.OnTapped(UnityEngine.VR.WSA.Input.InteractionSourceKind.Other, 1, new Ray());
            }
        }

        public void SendOnToggleSolarSystemOrbitScale()
        {
            if (SpectatorView.HolographicCameraManager.Instance.IsHoloLensUser())
            {
                Debug.Log("SendOnToggleSolarSystemOrbitScale");
                SendBasicStateChangeMessage(TestMessageID.ToggleSolarSystemOrbitScale);
            }
        }

        public enum VolumeUpdateFlags : byte
        {
            Position = 1,
            Rotation = 2,
            Scale = 4
        }

        private void OnUpdateVolumeTransform(NetworkInMessage msg)
        {
            if (msg.ReadInt64() != LocalUserId)
            {
                //Debug.Log("OnUpdateVolumeTransform");
                var anchorTrans = SpectatorView.SV_ImportExportAnchorManager.Instance.transform;

                VolumeUpdateFlags flags = (VolumeUpdateFlags)msg.ReadByte();
                if ((flags & VolumeUpdateFlags.Position) != 0)
                {
                    var position = SpectatorView.SV_CustomMessages.Instance.ReadVector3(msg);
                    // take the local positionread in and convert it to world space
                    var worldPos = anchorTrans.TransformPoint(position);
                    TransitionManager.Instance.ViewVolume.transform.position = worldPos;
                }
                if ((flags & VolumeUpdateFlags.Rotation) != 0)
                {
                    var rotation = SpectatorView.SV_CustomMessages.Instance.ReadQuaternion(msg);
                    TransitionManager.Instance.ViewVolume.transform.rotation = rotation;
                }
                if ((flags & VolumeUpdateFlags.Scale) != 0)
                {
                    var scale = SpectatorView.SV_CustomMessages.Instance.ReadVector3(msg);
                    TransitionManager.Instance.ViewVolume.transform.localScale = scale;
                }
            }
        }

        public void SendOnUpdateVolumeTransform(GameObject volume, VolumeUpdateFlags flags)
        {
            if (SpectatorView.HolographicCameraManager.Instance.IsHoloLensUser())
            {
                //Debug.Log("SendOnUpdateVolumeTransform");
                NetworkOutMessage msg = CreateMessage((byte)TestMessageID.UpdateVolumeTransform);
                msg.Write((byte)flags);
                // take the world position of the volume and convert it into Anchor's local space
                var anchorTrans = SpectatorView.SV_ImportExportAnchorManager.Instance.gameObject.transform;
                var anchorLocalPos = anchorTrans.InverseTransformPoint(volume.transform.position);
                if ((flags & VolumeUpdateFlags.Position) != 0)
                {
                    SpectatorView.SV_CustomMessages.Instance.AppendVector3(msg, anchorLocalPos);
                }
                if ((flags & VolumeUpdateFlags.Rotation) != 0)
                {
                    SpectatorView.SV_CustomMessages.Instance.AppendQuaternion(msg, volume.transform.rotation);
                }
                if ((flags & VolumeUpdateFlags.Scale) != 0)
                {
                    SpectatorView.SV_CustomMessages.Instance.AppendVector3(msg, volume.transform.localScale);
                }
                serverConnection.Broadcast(msg, MessagePriority.Medium, MessageReliability.Reliable);
            }
        }

        #endregion // event handlers

        #region // message helpers
        private void SendBasicStateChangeMessage(TestMessageID messageId)
        {
            NetworkOutMessage msg = CreateMessage((byte)messageId);
            serverConnection.Broadcast(msg, MessagePriority.High, MessageReliability.Reliable);
        }

        private NetworkOutMessage CreateMessage(byte MessageType)
        {
            NetworkOutMessage msg = serverConnection.CreateMessage(MessageType);
            msg.Write(MessageType);
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

namespace GalaxyExplorer_SpectatorView.Extensions
{
    public static class HolographicCameraManager
    {
        public static bool IsHolographicCameraRig(this SpectatorView.HolographicCameraManager hcm)
        {
            return hcm.localIPs.Contains(hcm.HolographicCameraIP.Trim());
        }

        public static bool IsHoloLensUser(this SpectatorView.HolographicCameraManager hcm)
        {
            if (holoLensUser == null)
            {
                if (GetHoloLensUser(hcm) == null)
                {
                    return false;
                }
            }

            return holoLensUser.GetID() == SharingStage.Instance.Manager.GetLocalUser().GetID();
        }

        private static User holoLensUser = null;
        public static User GetHoloLensUser(this SpectatorView.HolographicCameraManager hcm)
        {
            // if we've already determined the HoloLens user, return that cached value.
            if (holoLensUser != null)
            {
                //Debug.Log(string.Format("HoloLensUser.GetId() == {0}", holoLensUser.GetID()));
                return holoLensUser;
            }

            // get the user ID's and bail out if there are too few
            var userIds = SharingSessionTracker.Instance.UserIds;
            if (userIds.Count < 3)
            {
                //Debug.Log("GetHoloLensUser() returning null(1)");
                return null;
            }

            // find the HoloLens user amongst the connected users
            for (int i=0; i<userIds.Count; i++)
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
                    //Debug.Log("GetHoloLensUser() returning null(2)");
                    return null;
                }
                else if (userId  == hcm.tppcUser.GetID())
                {
                    continue;
                }
#else
                // we aren't running as the editor, check to see if we are the Spectator View camera rig
                if (SpectatorView.HolographicCameraManager.Instance.IsHolographicCameraRig())
                {
                    // if we are, we can skip the Local User because that's us
                    if (userId == SharingStage.Instance.Manager.GetLocalUser().GetID())
                    {
                        continue;
                    }
                    // We can also skip the editor user for obvious reasons
                    if (userId == hcm.editorUser.GetID())
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

            return holoLensUser;
        }
    }
}