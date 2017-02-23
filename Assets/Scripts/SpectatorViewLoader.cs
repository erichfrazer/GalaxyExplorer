using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using HoloToolkit.Unity;

public class SpectatorViewLoader : Singleton<SpectatorViewLoader>
{
    private string SpectatorView = "SpectatorView";
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
