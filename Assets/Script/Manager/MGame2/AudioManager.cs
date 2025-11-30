using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Sources")]
    [SerializeField] private AudioSource bgmSource;   // BGM 1개
    [SerializeField] private AudioSource sfxSource;   // 효과음

    [Header("Single BGM")]
    public AudioClip mainBGM;   // 게임 전체에서 사용할 BGM 하나

    [Header("MCardGame SFX")]
    public AudioClip mcardStartClip;
    public AudioClip mcardClickClip;
    public AudioClip mcardSuccessClip;
    public AudioClip mcardFailClip;

    private void Awake()
    {
        // 싱글턴
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // AudioSource 자동 생성
        if (bgmSource == null)
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.loop = true;
            bgmSource.playOnAwake = false;
        }

        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.loop = false;
            sfxSource.playOnAwake = false;
        }
    }

    /* ===== BGM (하나만 사용) ===== */

    public void PlayMainBGM(float volume = 1f)
    {
        if (mainBGM == null || bgmSource == null) return;

        bgmSource.clip = mainBGM;
        bgmSource.volume = volume;
        bgmSource.Play();
    }

    public void StopBGM()
    {
        if (bgmSource != null)
            bgmSource.Stop();
    }

    /* ===== SFX ===== */

    public void PlaySFX(AudioClip clip, float volume = 1f)
    {
        if (clip == null || sfxSource == null) return;
        sfxSource.PlayOneShot(clip, volume);
    }

    /* ===== MCardGame 전용 ===== */

    public void PlayMCardStart()   => PlaySFX(mcardStartClip);
    public void PlayMCardClick()   => PlaySFX(mcardClickClip);
    public void PlayMCardSuccess() => PlaySFX(mcardSuccessClip);
    public void PlayMCardFail()    => PlaySFX(mcardFailClip);
}
