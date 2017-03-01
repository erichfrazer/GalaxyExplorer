using System.Collections;
using UnityEngine;
using HoloToolkit.Unity;
using HoloToolkit.Sharing;
using GalaxyExplorer.SpectatorViewExtensions;

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
        public static bool SpectatorViewEnabled = false;
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
            var hcmInstance = SpectatorView.HolographicCameraManager.Instance;

            // we only do this waiting if we are the SpectatorView camera rig
            if ((hcmInstance != null) && hcmInstance.IsHolographicCameraRig())
            {
                Debug.Log("We are the Holographic Camera; waiting until active");
                //while (!hcmInstance.IsCurrentlyActive)
                {
                    yield return new WaitForEndOfFrame();
                }

                Debug.Log("Holographic Camera is active ");
                while (true)
                {
                    if (SharingSessionTracker.Instance.UserIds.Count >= 3)
                    {
                        if ((hcmInstance.editorUser != null))
                        {
                            Debug.Log("### have all SV participants ###");
                            break;
                        }
                    }
                    Debug.Log(string.Format("Editor User: {0}", (hcmInstance.editorUser == null) ? "NULL" : hcmInstance.editorUser.GetName().ToString()));
                    yield return new WaitForSeconds(1);
                }

                SendSpectatorViewPlayersReady();
                SpectatorViewParticipantsReady = true;
            }
        }

#if UNITY_EDITOR
        public static Ray GetSpectatorViewGazeRay(Ray defaultRay, float offsetFromOrigin)
        {
            if (!SpectatorViewEnabled)
            {
                return defaultRay;
            }

            if (!SpectatorView.HolographicCameraManager.Instance)
            {
                return defaultRay;
            }

            var remoteUser = SpectatorView.HolographicCameraManager.Instance.GetHoloLensUser();

            if (remoteUser == null)
            {
                return defaultRay;
            }

            var remoteHead = SpectatorView.SV_RemotePlayerManager.Instance.GetRemoteHeadInfo(remoteUser.GetID()).HeadObject;

            Ray retRay = new Ray();

            retRay.origin = remoteHead.transform.position + offsetFromOrigin * remoteHead.transform.forward;
            retRay.direction = remoteHead.transform.forward;

            return retRay;
        }

        public static Transform GetSpectatorViewUserTransform(Transform defaultTransform)
        {
            if (!SpectatorViewEnabled)
            {
                return defaultTransform;
            }

            if (!SpectatorView.HolographicCameraManager.Instance)
            {
                return defaultTransform;
            }

            var remoteUser = SpectatorView.HolographicCameraManager.Instance.GetHoloLensUser();

            if (remoteUser == null)
            {
                return defaultTransform;
            }

            var remoteHead = SpectatorView.SV_RemotePlayerManager.Instance.GetRemoteHeadInfo(remoteUser.GetID()).HeadObject;
            return remoteHead.transform;
        }
#endif

        private void SendSpectatorViewPlayersReady()
        {
            SpectatorView_GE_CustomMessages.Instance.SendSpectatorViewPlayersReady();
        }

        public void SendIntroductionEarthPlaced()
        {
            SpectatorView_GE_CustomMessages.Instance.SendOnIntroductionEarthPlaced();
        }
    }
}

namespace GalaxyExplorer.SpectatorViewExtensions
{
    public static class HolographicCameraManager
    {
        public static bool IsHolographicCameraRig(this SpectatorView.HolographicCameraManager hcm)
        {
            return hcm.localIPs.Contains(hcm.HolographicCameraIP.Trim());
        }

#if UNITY_EDITOR
        private static User holoLensUser = null;
        public static User GetHoloLensUser(this SpectatorView.HolographicCameraManager hcm)
        {
            if (holoLensUser != null)
            {
                return holoLensUser;
            }

            var userIds = SharingSessionTracker.Instance.UserIds;

            if (userIds.Count < 3)
            {
                return null;
            }


            User retUser = null;
            for (int i=0; i<userIds.Count; i++)
            {
                long userId = userIds[i];
                if (userId == SharingStage.Instance.Manager.GetLocalUser().GetID())
                {
                    continue;
                }
                if (userId  == SpectatorView.HolographicCameraManager.Instance.tppcUser.GetID())
                {
                    continue;
                }
                retUser = SharingSessionTracker.Instance.GetUserById(userId);
                break;
            }

            return retUser;
        }
#endif
    }
}