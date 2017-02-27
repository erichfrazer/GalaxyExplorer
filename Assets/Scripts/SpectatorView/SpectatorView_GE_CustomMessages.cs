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
        }

        private NetworkOutMessage CreateMessage(byte MessageType)
        {
            NetworkOutMessage msg = serverConnection.CreateMessage(MessageType);
            msg.Write(MessageType);
            // Add the local userID so that the remote clients know whose message they are receiving
            msg.Write(SharingStage.Instance.Manager.GetLocalUser().GetID());
            return msg;
        }

        private void OnSpectatorViewPlayersReady(NetworkInMessage msg)
        {
            SpectatorViewSharingConnector.Instance.SpectatorViewParticipantsReady = true;
        }

        public void SendSpectatorViewPlayersReady()
        {
            NetworkOutMessage msg = CreateMessage((byte)TestMessageID.SpectatorViewPlayersReady);
            serverConnection.Broadcast(msg, MessagePriority.High, MessageReliability.Reliable);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            connectionAdapter.MessageReceivedCallback -= ConnectionAdapter_OnMessageReceived;
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


    }
}