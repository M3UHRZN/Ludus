using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Unity.Netcode; // Çok oyunculu sahne geçiþi için þart!

public class LoadingManager : MonoBehaviour
{
    // Singleton mantýðý: Oyunda bundan sadece 1 tane olabilir ve asla yok olmaz
    public static LoadingManager Instance;

    [Header("UI Referanslarý")]
    public GameObject loadingCanvasPanel; // Background objesini buraya sürükle
    public Slider progressBar;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Sahne deðiþse de bu ekran SÝLÝNMEZ!
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // Oyun ilk açýldýðýnda loading ekranýný gizle
        if (loadingCanvasPanel != null) loadingCanvasPanel.SetActive(false);
    }

    // 1. TEK KÝÞÝLÝK YÜKLEME (Ana Menü -> Lobi gibi aða baðlý OLMAYAN geçiþler)
    public void LoadSceneLocal(string sceneName)
    {
        StartCoroutine(LoadSceneCoroutine(sceneName, false));
    }

    // 2. ÇOK OYUNCULU YÜKLEME (Lobi -> RNGMap gibi takýmca yapýlan geçiþler)
    public void LoadSceneNetwork(string sceneName)
    {
        StartCoroutine(LoadSceneCoroutine(sceneName, true));
    }

    private IEnumerator LoadSceneCoroutine(string sceneName, bool isNetworked)
    {
        // 1. Ekraný aç ve barý sýfýrla
        loadingCanvasPanel.SetActive(true);
        progressBar.value = 0f;

        // Okuma payý: Sahne anýnda yüklense bile oyuncu o efsane lore metnini okuyabilsin diye 1.5 saniye bekle
        yield return new WaitForSeconds(1.5f);

        if (isNetworked && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            // === AÐ ÜZERÝNDEN YÜKLEME (Sadece HOST tetikler, diðerleri otomatik takip eder) ===
            NetworkManager.Singleton.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);

            // NGO Sahneyi anlýk yüklediði için barý görsel olarak akýcý dolduruyoruz
            float fakeProgress = 0f;
            while (fakeProgress < 1f)
            {
                fakeProgress += Time.deltaTime / 2f; // 2 saniye civarýnda dolar
                progressBar.value = fakeProgress;
                yield return null;
            }
        }
        else if (!isNetworked)
        {
            // === NORMAL (LOCAL) YÜKLEME ===
            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
            operation.allowSceneActivation = false; // Sahne hazýr olsa bile %90'da bekle (gizli geçiþ için)

            while (!operation.isDone)
            {
                // Unity'de yükleme 0 ile 0.9 arasý döner, bunu barýmýz için 0 ile 1 arasýna çeviriyoruz
                float progress = Mathf.Clamp01(operation.progress / 0.9f);
                progressBar.value = progress;

                if (operation.progress >= 0.9f)
                {
                    operation.allowSceneActivation = true; // Geçiþe izin ver
                }
                yield return null;
            }
        }

        // Yükleme bitti, küçük bir yumuþak geçiþ beklemesi
        yield return new WaitForSeconds(0.5f);
        loadingCanvasPanel.SetActive(false);
    }
}