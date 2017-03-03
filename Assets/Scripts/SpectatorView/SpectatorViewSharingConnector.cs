using System.Collections;
using UnityEngine;
using HoloToolkit.Unity;
using HoloToolkit.Sharing;
using GalaxyExplorer.SpectatorViewExtensions;

namespace GalaxyExplorer
{
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

                SendOnSpectatorViewPlayersReady();
                SpectatorViewParticipantsReady = true;
            }
        }

        public bool IsHoloLensUser
        {
            get { return SpectatorView.HolographicCameraManager.Instance.IsHoloLensUser(); }
        }

        #region //editor only code
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
        #endregion

        public void SendOnAdvanceIntroduction()
        {
            SpectatorView_GE_CustomMessages.Instance.SendOnAdvanceIntroduction();
        }

        public void SendOnHideAllCards()
        {
            SpectatorView_GE_CustomMessages.Instance.SendOnHideAllCards();
        }

        public void SendOnIntroductionEarthPlaced()
        {
            SpectatorView_GE_CustomMessages.Instance.SendOnIntroductionEarthPlaced();
        }

        public void SendOnMoveCube()
        {
            SpectatorView_GE_CustomMessages.Instance.SendOnMoveCube();
        }

        public void SendOnPointOfInterestCardTapped(CardPointOfInterest card)
        {
            var cardParent = card.gameObject.transform.parent;
            var cardParentParent = cardParent.gameObject.transform.parent;
            SpectatorView_GE_CustomMessages.Instance.SendOnPointOfInterestCardTapped(cardParentParent.name);
        }

        public void SendOnSceneTransitionBackward()
        {
            SpectatorView_GE_CustomMessages.Instance.SendOnSceneTransitionBackward();
        }

        public void SendOnSceneTransitionForward(string sceneName, GameObject transitionSourceObject)
        {
            string transitionSourceObjectName = string.Empty;
            if (transitionSourceObject)
            {
                transitionSourceObjectName = transitionSourceObject.name;
            }
            SpectatorView_GE_CustomMessages.Instance.SendOnTransitionSceneForward(sceneName, transitionSourceObjectName);
        }

        private void SendOnSpectatorViewPlayersReady()
        {
            SpectatorView_GE_CustomMessages.Instance.SendOnSpectatorViewPlayersReady();
        }

        public void SendOnToggleSolarSystemOrbitScale()
        {
            SpectatorView_GE_CustomMessages.Instance.SendOnToggleSolarSystemOrbitScale();
        }

        public void SendOnUpdateVolumeTransform(GameObject volume)
        {
            SpectatorView_GE_CustomMessages.Instance.SendOnUpdateVolumeTransform(volume);
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
                return holoLensUser;
            }

            // get the user ID's and bail out if there are too few
            var userIds = SharingSessionTracker.Instance.UserIds;
            if (userIds.Count < 3)
            {
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