using System.Text;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using HoloToolkit.Unity;
using HoloToolkit.Sharing;

namespace GalaxyExplorer
{
    public class SpectatorView_GE_CustomMessages : Singleton<SpectatorView_GE_CustomMessages>
    {
        public enum TestMessageID : byte
        {
            SpectatorViewPlayersReady = MessageID.UserMessageIDStart,
            AdvanceIntroduction,
            IntroductionEarthPlaced,
            SceneTransitionForward,
            SceneTransitionBackward,
            ToggleSolarSystemOrbitScale,
            PointOfInterestCardTapped,
            HideAllCards,
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
        }

        private NetworkOutMessage CreateMessage(byte MessageType)
        {
            NetworkOutMessage msg = serverConnection.CreateMessage(MessageType);
            msg.Write(MessageType);
            // Add the local userID so that the remote clients know whose message they are receiving
            msg.Write((long)SharingStage.Instance.Manager.GetLocalUser().GetID());
            return msg;
        }

        private void OnHideAllCards(NetworkInMessage msg)
        {
            Debug.Log("OnHideAllCards");
            CardPOIManager.Instance.HideAllCards();
        }

        private void OnPointOfInterestCardTapped(NetworkInMessage msg)
        {
            Debug.Log("OnPointOfInterestCardTapped");
            msg.ReadInt64(); // read and discard the userID that sent the message.
            string poiName = ReadyByteArrayAsString(msg);
            var Anchor = SpectatorView.SV_ImportExportAnchorManager.Instance.gameObject;
            CardPointOfInterest[] cards = Anchor.GetComponentsInChildren<CardPointOfInterest>();
            bool cardFound = false;
            foreach(var card in cards)
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

        private void OnToggleSolarSystemOrbitScale(NetworkInMessage msg)
        {
            Debug.Log("OnToggleSolarSystemOrbitalScale");
            var Anchor = SpectatorView.SV_ImportExportAnchorManager.Instance.gameObject;
            OrbitScalePointOfInterest ospoi = Anchor.GetComponentInChildren<OrbitScalePointOfInterest>();
            ospoi.OnTapped(UnityEngine.VR.WSA.Input.InteractionSourceKind.Other, 1, new Ray());
        }

        private void OnSceneTransitionBackward(NetworkInMessage msg)
        {
            Debug.Log("OnSceneTransitionBackward");
            ViewLoader.Instance.GoBack();
        }

        private void OnSceneTransitionForward(NetworkInMessage msg)
        {
            Debug.Log("OnSceneTransitionForward");

            msg.ReadInt64(); // read and discard the userID
            string sceneName = ReadyByteArrayAsString(msg);
            string transitionSourceObjectName = ReadyByteArrayAsString(msg);
            // try to find an object with the provided name in the hierarchy
            bool foundSourceObject = false;
            var Anchor = SpectatorView.SV_ImportExportAnchorManager.Instance.gameObject;
            foreach (PointOfInterest poi in Anchor.GetComponentsInChildren<PointOfInterest>())
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

        private void OnSpectatorViewPlayersReady(NetworkInMessage msg)
        {
            Debug.Log("OnSpectatorViewPlayersReady");
            SpectatorViewSharingConnector.Instance.SpectatorViewParticipantsReady = true;
        }

        private void OnAdvanceIntroduction(NetworkInMessage msg)
        {
            Debug.Log("OnAdvanceIntroduction");
            InputRouter.Instance.OnTapped(UnityEngine.VR.WSA.Input.InteractionSourceKind.Other, 1, new Ray());
        }

        private void OnIntroductionEarthPlaced(NetworkInMessage msg)
        {
            Debug.Log("OnEarthPlaced");
            TransitionManager.Instance.ViewVolume.GetComponentInChildren<PlacementControl>().TogglePinnedState();
        }

        private void SendBasicStateChangeMessage(TestMessageID messageId)
        {
            NetworkOutMessage msg = CreateMessage((byte)messageId);
            serverConnection.Broadcast(msg, MessagePriority.High, MessageReliability.Reliable);
        }

        public void SendOnSpectatorViewPlayersReady()
        {
            Debug.Log("SendOnSpectatorViewPlayersReady");
            SendBasicStateChangeMessage(TestMessageID.SpectatorViewPlayersReady);
        }

        public void SendOnAdvanceIntroduction()
        {
            Debug.Log("SendOnAdvanceIntroduction");
            // due to timing issues, whe might need to eventually send the
            // current Introduction Flow state
            SendBasicStateChangeMessage(TestMessageID.AdvanceIntroduction);
        }

        public void SendOnIntroductionEarthPlaced()
        {
            Debug.Log("SendOnIntroductionEarthPlaced");
            SendBasicStateChangeMessage(TestMessageID.IntroductionEarthPlaced);
        }

        public void SendOnSceneTransitionBackward()
        {
            Debug.Log("SendOnSceneTransitionBackward");
            SendBasicStateChangeMessage(TestMessageID.SceneTransitionBackward);
        }

        public void SendOnToggleSolarSystemOrbitScale()
        {
            Debug.Log("SendOnToggleSolarSystemOrbitScale");
            SendBasicStateChangeMessage(TestMessageID.ToggleSolarSystemOrbitScale);
        }

        public void SendOnTransitionSceneForward(string sceneName, string transitionSourceObjectName)
        {
            Debug.Log("SendOnTransitionSceneForward");

            NetworkOutMessage msg = CreateMessage((byte)TestMessageID.SceneTransitionForward);
            Debug.Log(string.Format("SpectatorView_GE_CustomMessages::SendOnTransitionSceneForward({0}, {1});", sceneName, transitionSourceObjectName));
            AppendStringAsByteArray(msg, sceneName);
            AppendStringAsByteArray(msg, transitionSourceObjectName);
            serverConnection.Broadcast(msg, MessagePriority.High, MessageReliability.Reliable);
        }

        public void SendOnPointOfInterestCardTapped(string poiName)
        {
            Debug.Log("SendOnPointOfInterestCardTapped");
            NetworkOutMessage msg = CreateMessage((byte)TestMessageID.PointOfInterestCardTapped);
            AppendStringAsByteArray(msg, poiName);
            serverConnection.Broadcast(msg, MessagePriority.High, MessageReliability.Reliable);
        }

        public void SendOnHideAllCards()
        {
            Debug.Log("SendOnHideAllCards");
            SendBasicStateChangeMessage(TestMessageID.HideAllCards);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (connectionAdapter != null)
            {
                connectionAdapter.MessageReceivedCallback -= ConnectionAdapter_OnMessageReceived;
            }
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
    }
}