using System.Text;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using HoloToolkit.Unity;
using HoloToolkit.Sharing;
using GalaxyExplorer.SpectatorViewExtensions;

namespace GalaxyExplorer
{
    public class SpectatorView_GE_CustomMessages : Singleton<SpectatorView_GE_CustomMessages>
    {
        public enum TestMessageID : byte
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
            Max
        }

        public delegate void MessageCallback(NetworkInMessage msg);
        public Dictionary<TestMessageID, MessageCallback> MessageHandlers
        {
            private set;
            get;
        }

        NetworkConnection serverConnection;
        NetworkConnectionAdapter connectionAdapter;

        private long LocalUserId
        {
            get { return (long)SharingStage.Instance.Manager.GetLocalUser().GetID(); }
        }

        protected override void Awake()
        {
            base.Awake();
            MessageHandlers = new Dictionary<TestMessageID, MessageCallback>();
        }

        private IEnumerator Start()
        {
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

        private void OnHideAllCards(NetworkInMessage msg)
        {
            if (msg.ReadInt64() != LocalUserId)
            {
                Debug.Log("OnHideAllCards");
                CardPOIManager.Instance.HideAllCards();
            }
        }

        private void OnIntroductionEarthPlaced(NetworkInMessage msg)
        {
            Debug.Log("OnEarthPlaced");
            TransitionManager.Instance.ViewVolume.GetComponentInChildren<PlacementControl>().TogglePinnedState();
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

        private void OnPointOfInterestCardTapped(NetworkInMessage msg)
        {
            if (msg.ReadInt64() != LocalUserId)
            {
                string poiName = ReadyByteArrayAsString(msg);
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

        private void OnSceneTransitionBackward(NetworkInMessage msg)
        {
            Debug.Log("OnSceneTransitionBackward");
            ViewLoader.Instance.GoBack();
        }

        private void OnSceneTransitionForward(NetworkInMessage msg)
        {
            if (msg.ReadInt64() != LocalUserId)
            {
                string sceneName = ReadyByteArrayAsString(msg);
                string transitionSourceObjectName = ReadyByteArrayAsString(msg);
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

        private void OnSpectatorViewPlayersReady(NetworkInMessage msg)
        {
            Debug.Log("OnSpectatorViewPlayersReady");
            SpectatorViewSharingConnector.Instance.SpectatorViewParticipantsReady = true;
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

        private void OnUpdateVolumeTransform(NetworkInMessage msg)
        {
            if (msg.ReadInt64() != LocalUserId)
            {
                Debug.Log("OnUpdateVolumeTransform");
                var position = SpectatorView.SV_CustomMessages.Instance.ReadVector3(msg);
                var rotation = SpectatorView.SV_CustomMessages.Instance.ReadQuaternion(msg);
                // take the local position read in and transform it to world space
                var anchorTrans = SpectatorView.SV_ImportExportAnchorManager.Instance.transform;
                var worldPos = anchorTrans.TransformPoint(position);
                TransitionManager.Instance.ViewVolume.transform.position = worldPos;
                TransitionManager.Instance.ViewVolume.transform.rotation = rotation;
            }
        }
        #endregion // event handlers

        #region // message senders
        private void SendBasicStateChangeMessage(TestMessageID messageId)
        {
            NetworkOutMessage msg = CreateMessage((byte)messageId);
            serverConnection.Broadcast(msg, MessagePriority.High, MessageReliability.Reliable);
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

        public void SendOnMoveCube()
        {
            if (SpectatorView.HolographicCameraManager.Instance.IsHoloLensUser())
            {
                Debug.Log("SendOnMoveCube");
                SendBasicStateChangeMessage(TestMessageID.MoveCube);
            }
        }

        public void SendOnSpectatorViewPlayersReady()
        {
            Debug.Log("SendOnSpectatorViewPlayersReady");
            SendBasicStateChangeMessage(TestMessageID.SpectatorViewPlayersReady);
        }

        public void SendOnIntroductionEarthPlaced()
        {
            if (SpectatorView.HolographicCameraManager.Instance.IsHoloLensUser())
            {
                Debug.Log("SendOnIntroductionEarthPlaced");
                SendBasicStateChangeMessage(TestMessageID.IntroductionEarthPlaced);
            }
        }

        public void SendOnSceneTransitionBackward()
        {
            if (SpectatorView.HolographicCameraManager.Instance.IsHoloLensUser())
            {

                Debug.Log("SendOnSceneTransitionBackward");
                SendBasicStateChangeMessage(TestMessageID.SceneTransitionBackward);
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

        public void SendOnTransitionSceneForward(string sceneName, string transitionSourceObjectName)
        {
            if (SpectatorView.HolographicCameraManager.Instance.IsHoloLensUser())
            {
                Debug.Log(string.Format("SendOnTransitionSceneForward({0}, {1}", sceneName, transitionSourceObjectName));
                NetworkOutMessage msg = CreateMessage((byte)TestMessageID.SceneTransitionForward);
                AppendStringAsByteArray(msg, sceneName);
                AppendStringAsByteArray(msg, transitionSourceObjectName);
                serverConnection.Broadcast(msg, MessagePriority.High, MessageReliability.Reliable);
            }
        }

        public void SendOnPointOfInterestCardTapped(string poiName)
        {
            if (SpectatorView.HolographicCameraManager.Instance.IsHoloLensUser())
            {
                Debug.Log(string.Format("SendOnPointOfInterestCardTapped({0})", poiName));
                NetworkOutMessage msg = CreateMessage((byte)TestMessageID.PointOfInterestCardTapped);
                AppendStringAsByteArray(msg, poiName);
                serverConnection.Broadcast(msg, MessagePriority.High, MessageReliability.Reliable);
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

        public void SendOnUpdateVolumeTransform(GameObject volume)
        {
            if (SpectatorView.HolographicCameraManager.Instance.IsHoloLensUser())
            {
                Debug.Log("SendOnVolumePositionUpdate");
                NetworkOutMessage msg = CreateMessage((byte)TestMessageID.UpdateVolumeTransform);
                // take the world position of volume and transform it into Anchor's local space
                var anchorTrans = SpectatorView.SV_ImportExportAnchorManager.Instance.gameObject.transform;
                var anchorLocalPos = anchorTrans.InverseTransformPoint(volume.transform.position);
                SpectatorView.SV_CustomMessages.Instance.AppendTransform(msg, anchorLocalPos, volume.transform.rotation);
                serverConnection.Broadcast(msg, MessagePriority.Medium, MessageReliability.Reliable);
            }
        }

        #endregion // message senders

        #region // message helpers
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

        private void AppendStringAsByteArray(NetworkOutMessage msg, string data)
        {
            byte[] bytes = System.Text.Encoding.ASCII.GetBytes(data);
            long byteSize = bytes.Length;
            msg.Write(byteSize);
            msg.WriteArray(bytes, (uint)byteSize);
        }
        private string ReadyByteArrayAsString(NetworkInMessage msg)
        {
            long byteSize = msg.ReadInt64();
            byte[] bytes = new byte[(uint)byteSize];
            msg.ReadArray(bytes, (uint)byteSize);

            return System.Text.Encoding.ASCII.GetString(bytes);
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