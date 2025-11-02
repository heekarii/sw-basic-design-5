using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CardNumber : MonoBehaviour
{
    public int number;
    public TextMeshProUGUI numberText;
    private Button button;
    private Image image;
    private MCardGameManager manager;

    void Awake()
    {
        button = GetComponent<Button>();
        image = GetComponent<Image>();
        button.onClick.AddListener(OnClick);
    }

    public void Setup(int num, MCardGameManager mgr)
    {
        number = num;
        manager = mgr;
        numberText.text = num.ToString();
        SetColor(Color.white); 
        button.interactable = true;
    }

    void OnClick()
    {
        manager.SelectCard(this);
    }

    public void SetColor(Color c)
    {
        if (image != null)
            image.color = c;
    }

    public void Disable()
    {
        button.interactable = false;
        SetColor(new Color(0.3f, 0.8f, 0.4f)); // 정답: 초록
    }

    public void ShowNumber(bool on)
    {
        numberText.gameObject.SetActive(on);
        if (on)
            SetColor(Color.white);
        else
            SetColor(new Color(0.7f, 0.7f, 0.7f)); // 뒷면 회색
    }

    // ❌ 오답 카드용 함수 추가
    public void ShowWrong()
    {
        ShowNumber(true); // 숫자 보이게
        SetColor(new Color(0.9f, 0.2f, 0.2f)); // 빨간색
        button.interactable = false;
    }
}