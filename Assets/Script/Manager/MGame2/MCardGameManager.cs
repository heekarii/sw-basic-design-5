using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class MCardGameManager : MonoBehaviour
{
    [Header("Settings")]
    public float memorizeTime = 10f;   // 외울 시간
    public float limitTime = 5f;       // 실제 플레이 제한 시간
    public int totalCards = 6;         // 카드 개수

    [Header("References")]
    public Transform gridArea;         // 카드 배치 부모
    public GameObject cardPrefab;      // 카드 프리팹
    public TextMeshProUGUI textTimer;  // 상단 타이머
    public TextMeshProUGUI textPhase;  // 상단 상태 텍스트
    public GameObject resultPanel;     // 결과 패널
    public TextMeshProUGUI textResult; // 결과 텍스트

    private List<CardNumber> cards = new List<CardNumber>();
    private int currentTarget = 1;
    private float timeLeft;
    private bool gameActive = false;
    private bool inputLocked = false;

    void Start()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayMainBGM();
        
        resultPanel.SetActive(false);
        StartCoroutine(GameFlow());
    }

    IEnumerator GameFlow()
    {
        // 초기화
        textPhase.text = "Memorize";
        InitCards();

        // Memorize 타이머
        timeLeft = memorizeTime;
        while (timeLeft > 0f)
        {
            timeLeft -= Time.deltaTime;
            if (textTimer) textTimer.text = $"Time: {Mathf.Max(0f, timeLeft):F1}";
            yield return null;
        }

        // 암기 끝 → 숫자 숨기기
        foreach (var c in cards)
            c.ShowNumber(false);

        // 맞추기 시작 (플레이 단계 진입 SFX)
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayMCardStart();

        // 플레이 시작
        textPhase.text = "Play";
        timeLeft = limitTime;
        gameActive = true;
    }

    void Update()
    {
        if (!gameActive) return;

        timeLeft -= Time.deltaTime;
        if (textTimer) textTimer.text = $"Time: {Mathf.Max(0f, timeLeft):F1}";

        if (timeLeft <= 0f)
        {
            EndGame(false);
        }
    }

    void InitCards()
    {
        // 기존 카드 정리
        foreach (Transform t in gridArea)
            Destroy(t.gameObject);
        cards.Clear();
        currentTarget = 1;

        // 1~N 숫자 섞기
        List<int> nums = new List<int>();
        for (int i = 1; i <= totalCards; i++) nums.Add(i);
        Shuffle(nums);

        // 카드 생성
        foreach (int n in nums)
        {
            GameObject obj = Instantiate(cardPrefab, gridArea);
            var card = obj.GetComponent<CardNumber>();
            card.Setup(n, this);
            cards.Add(card);
        }
    }

    public void SelectCard(CardNumber selected)
    {
        if (!gameActive || inputLocked) return;

        // 카드 클릭 SFX
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayMCardClick();

        // ✅ 정답 카드
        if (selected.number == currentTarget)
        {
            selected.ShowNumber(true);
            selected.Disable();
            currentTarget++;

            if (currentTarget > totalCards)
            {
                EndGame(true);
            }
        }
        else
        {
            // ❌ 틀린 카드 → 빨갛게 표시 후 실패 처리
            StartCoroutine(WrongCardEffect(selected));
        }
    }

    IEnumerator WrongCardEffect(CardNumber wrongCard)
    {
        inputLocked = true;
        wrongCard.ShowWrong();  // 카드 자체에서 색상 변경
        yield return new WaitForSeconds(0.3f);
        EndGame(false);
    }

    void EndGame(bool isSuccess)
    {
        if (!gameActive) return;
        gameActive = false;

        // 성공/실패 SFX
        if (AudioManager.Instance != null)
        {
            if (isSuccess) AudioManager.Instance.PlayMCardSuccess();
            else           AudioManager.Instance.PlayMCardFail();

            // ★ 성공/실패하면 BGM 끄기
            AudioManager.Instance.StopBGM();
        }

        textPhase.text = isSuccess ? "SUCCESS" : "FAILED";
        if (textResult)
            textResult.text = isSuccess ? "SUCCESS!" : "FAILED!";

        resultPanel.SetActive(true);

        if (isSuccess)
            SendPlayer_Weapon();

        Debug.Log(isSuccess ? "게임 성공!" : "게임 실패!");
        TransitionManager.Instance.EndMiniGame("MCardGame", isSuccess);
    }
    
    IEnumerator AutoClose()
    {
        yield return new WaitForSeconds(2.0f);
        gameObject.SetActive(false);
    }

    void SendPlayer_Weapon()
    {
        // 보상 전달 (필요하면 여기 채우기)
    }

    void Shuffle(List<int> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int r = Random.Range(i, list.Count);
            (list[i], list[r]) = (list[r], list[i]);
        }
    }
}
