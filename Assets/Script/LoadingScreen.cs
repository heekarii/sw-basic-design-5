using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using TMPro;

public class LoadingScreen : MonoBehaviour
{
    public Slider loadingBar;
    public TextMeshProUGUI loadingText;

    private string _targetScene;
    private LoadSceneMode _loadMode;

    void Start()
    {
        _targetScene = PlayerPrefs.GetString("LOAD_SCENE_NAME");
        _loadMode    = (LoadSceneMode)PlayerPrefs.GetInt("LOAD_SCENE_MODE", 0);
        StartCoroutine(LoadScene());
    }

    private IEnumerator LoadScene()
    {
        yield return null;
        yield return null;
        StartCoroutine(LoadSceneProcess());
    }

    IEnumerator LoadSceneProcess()
    {
        AsyncOperation op = SceneManager.LoadSceneAsync(_targetScene, _loadMode);
        op.allowSceneActivation = false;

        float displayed = 0f;

        while (!op.isDone)
        {
            // 실제 로딩 progress (0 ~ 0.9)
            float target = Mathf.Clamp01(op.progress / 0.9f);

            // 로딩바가 갑자기 점프하지 않도록 부드럽게 증가시키기
            displayed = Mathf.MoveTowards(displayed, target, Time.deltaTime * 0.5f);

            loadingBar.value = displayed;
            loadingText.text = $"Loading.. {(displayed * 100f):F0}%";

            // 실제 로딩 완료됐고, 표시된 로딩바도 100% 도달
            if (displayed >= 1f && op.progress >= 0.9f)
            {
                op.allowSceneActivation = true;

                Debug.Log($"[LoadingScreen] allowSceneActivation true -> target={_targetScene}, mode={_loadMode}");

                // 다음 프레임에서 Additive 로드된 씬을 활성화하기 위해 1~2프레임 대기
                yield return null;
                yield return null;

                Scene loadedScene = SceneManager.GetSceneByName(_targetScene);
                if (loadedScene.IsValid())
                {
                    SceneManager.SetActiveScene(loadedScene);
                    Debug.Log($"[LoadingScreen] SetActiveScene -> {_targetScene}");
                }

                // Map_SCENE 비활성화
                var mapScene = SceneManager.GetSceneByName("Map_SCENE");
                if (mapScene.IsValid())
                {
                    foreach (var root in mapScene.GetRootGameObjects())
                    {
                        try { root.SetActive(false); } catch { }
                    }
                    Debug.Log("[LoadingScreen] Map_SCENE 비활성화");
                }

                SceneManager.UnloadSceneAsync("LoadingScene");
                Debug.Log("[LoadingScreen] Unload LoadingScene 호출");
            }

            yield return null;
        }
    }

}