using System.Collections;
using UnityEngine;
using HoloToolkit.Unity;
using HoloToolkit.Sharing;

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

        StartCoroutine(WaitForSpectatorViewParticipantsAsync());
    }

    private IEnumerator WaitForSpectatorViewParticipantsAsync()
    {
        // we only do this waiting if we are the SpectatorView camera rig
        if (SpectatorView.HolographicCameraManager.Instance != null)
        {
            Debug.Log("We are the Holographic Camera; waiting until active");
            while (!SpectatorView.HolographicCameraManager.Instance.IsCurrentlyActive)
            {
                yield return new WaitForEndOfFrame();
            }
        }

        var hcmInstance = SpectatorView.HolographicCameraManager.Instance;
        while (true)
        {
            if (SharingSessionTracker.Instance.UserIds.Count >= 3)
            {
                if ((hcmInstance.tppcUser != null) &&
                    (hcmInstance.editorUser != null))
                {
                    Debug.Log("### have all SV participants ###");
                    break;
                }
            }
            Debug.Log(string.Format("  TPPC User: {0}", (hcmInstance.tppcUser == null) ? "NULL" : hcmInstance.tppcUser.GetName().ToString()));
            Debug.Log(string.Format("Editor User: {0}", (hcmInstance.editorUser == null) ? "NULL" : hcmInstance.editorUser.GetName().ToString()));
            yield return new WaitForEndOfFrame();
        }
        SpectatorViewParticipantsReady = true;
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
