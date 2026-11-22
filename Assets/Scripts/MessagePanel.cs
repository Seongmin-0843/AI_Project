using System;
using UnityEngine;
using UnityEngine.UI;

// 글 한 장 + 버튼 하나짜리 화면. 업무 지시문과 스테이지 결과 화면에 쓴다.
public class MessagePanel : MonoBehaviour
{
    public GameObject panel;
    public Text body, buttonLabel;

    Action onButton;
    bool hideOnPress;

    void Awake()
    {
        // 글이 길면 영역 안에 들어가도록 글씨를 줄임. 최대 크기는 Inspector의 Font Size
        body.resizeTextForBestFit = true;
        body.resizeTextMaxSize = body.fontSize;
        body.resizeTextMinSize = 16;
        panel.SetActive(false);
    }

    // hideOnPress=false: 버튼을 눌러도 바로 닫지 않음 (페이드가 덮은 뒤 Hide로 닫기)
    public void Show(string text, string button, Action action, bool hideOnPress = true)
    {
        body.text = text;
        buttonLabel.text = button;
        onButton = action;
        this.hideOnPress = hideOnPress;
        panel.SetActive(true);
    }

    public void Press()
    {
        var action = onButton;
        onButton = null; // 열어 둔 채 페이드 중에 다시 눌러도 두 번 실행되지 않게
        if (hideOnPress) panel.SetActive(false);
        action?.Invoke();
    }

    public void Hide() => panel.SetActive(false);
}
