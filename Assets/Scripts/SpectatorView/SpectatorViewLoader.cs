// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using HoloToolkit.Unity;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GalaxyExplorer.SpectatorView
{
    public class SpectatorViewLoader : Singleton<SpectatorViewLoader>
    {
        private string SpectatorView = "SpectatorView";
        [HideInInspector]
        public bool SpectatorViewLoaded = false;

        protected override void Awake()
        {
            base.Awake();
            StartCoroutine(LoadSpectatorViewAsync());
        }

        private IEnumerator LoadSpectatorViewAsync()
        {
            // skip a frame
            yield return null;

            var loadSpectatorViewOp = SceneManager.LoadSceneAsync(SpectatorView, LoadSceneMode.Additive);
            while (!loadSpectatorViewOp.isDone)
            {
                yield return new WaitForEndOfFrame();
            }

            SpectatorViewLoaded = true;
        }
    }
}