using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]

public class PlayerDamageEffect : MonoBehaviour
{
    [SerializeField] private Image damageImage;
    [SerializeField] private float fadeInTime = 0.05f;
    [SerializeField] private float holdTime = 0.1f;
    [SerializeField] private float fadeOutTime = 0.3f;
    [SerializeField] private float maxAlpha = 0.7f;

    private Coroutine _routine;

    private void Awake()
    {
        // 인스펙터에 안 넣어도 자동으로 자기 Image 가져오게
        if (damageImage == null)
            damageImage = GetComponent<Image>();
    }

    public void Play()
    {
        if (_routine != null)
            StopCoroutine(_routine);

        _routine = StartCoroutine(FlashCoroutine());
    }

    private IEnumerator FlashCoroutine()
    {
        Color c = damageImage.color;

        // 1) 빠르게 켜지기
        float t = 0f;
        while (t < fadeInTime)
        {
            t += Time.deltaTime;
            float a = Mathf.Lerp(0f, maxAlpha, t / fadeInTime);
            damageImage.color = new Color(c.r, c.g, c.b, a);
            yield return null;
        }

        // 2) 잠깐 유지
        yield return new WaitForSeconds(holdTime);

        // 3) 서서히 꺼지기
        t = 0f;
        while (t < fadeOutTime)
        {
            t += Time.deltaTime;
            float a = Mathf.Lerp(maxAlpha, 0f, t / fadeOutTime);
            damageImage.color = new Color(c.r, c.g, c.b, a);
            yield return null;
        }

        damageImage.color = new Color(c.r, c.g, c.b, 0f);
        _routine = null;
    }
}