using System.Collections;
using UnityEngine;
using HoloToolkit.Unity;

public enum SpectatorViewParticipant
{
    UnityClient,
    SpectatorViewRig,
    ClientHoloLens
}

public class SpectatorViewSharingConnector : Singleton<SpectatorViewSharingConnector>
{
    private string myIP = string.Empty;
    [HideInInspector]
    public bool SpectatorViewEnabled = false;
    //[HideInInspector]
    public bool SpectatorViewParticipantsReady = false;

    // Use this for initialization
    private IEnumerator Start()
    {
        SpectatorViewEnabled = true;
        //myIP = UnityEngine.Networking.NetworkManager.singleton.networkAddress;

        // wait until the SpectatorView Components are loaded before
        // exiting start.
        while (!SpectatorViewLoader.Instance.SpectatorViewLoaded)
        {
            yield return new WaitForEndOfFrame();
        }
    }

    private SpectatorViewParticipant WhoAmI()
    {
        var svInstance = SpectatorView.SpectatorViewManager.Instance;

        if (myIP.Equals(svInstance.SpectatorViewIP))
        {
            return SpectatorViewParticipant.SpectatorViewRig;
        }
#if UNITY_EDITOR
        return SpectatorViewParticipant.UnityClient;
#else
        return SpectatorViewParticipant.ClientHoloLens;
#endif
    }
}
