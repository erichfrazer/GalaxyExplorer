using System.Collections;
using UnityEngine;
using HoloToolkit.Unity;
using HoloToolkit.Sharing;
using GalaxyExplorer._SpectatorView;

namespace GalaxyExplorer
{
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

        protected override void Awake()
        {
            base.Awake();
            if (!GetComponent<SpectatorView_GE_CustomMessages>())
            {
                gameObject.AddComponent<SpectatorView_GE_CustomMessages>();
            }
        }

        // Use this for initialization
        private IEnumerator Start()
        {
            SpectatorViewEnabled = true;

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
            if ((SpectatorView.HolographicCameraManager.Instance != null) &&
                (SpectatorView.HolographicCameraManager.Instance.IsHolographicCameraRig()))
            {
                Debug.Log("We are the Holographic Camera; waiting until active");
                while (!SpectatorView.HolographicCameraManager.Instance.IsCurrentlyActive)
                {
                    yield return new WaitForEndOfFrame();
                }

                Debug.Log("Holographic Camera is active ");
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

                SpectatorView_GE_CustomMessages.Instance.SendSpectatorViewPlayersReady();
                SpectatorViewParticipantsReady = true;
            }
        }
    }
}
namespace GalaxyExplorer._SpectatorView
{
    public static class Extensions
    {
        public static bool IsHolographicCameraRig(this SpectatorView.HolographicCameraManager hcm)
        {
            return hcm.localIPs.Contains(hcm.HolographicCameraIP.Trim());
        }
    }
}