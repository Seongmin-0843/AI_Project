using System;
using UnityEngine;
using UnityEngine.UI;

// 단말기 번호 입력 창 (기획서 4-2). "M-" 뒤 숫자 4자리를 입력하고 [조회] 또는 Enter.
// 판정은 DeskFlow가 한다: submit(번호)이 돌려준 문구를 창에 띄운다.
public class TerminalPanel : MonoBehaviour
{
    public GameObject panel;
    public InputField input;
    public Text message;

    Func<string, string> submit;

    void Awake()
    {
        panel.SetActive(false);
        input.onSubmit.AddListener(_ => Submit());
    }

    public void Show(Func<string, string> onSubmit)
    {
        submit = onSubmit;
        input.text = "";
        message.text = ""; // 입력 안내는 그림에 있음
        panel.SetActive(true);
        input.ActivateInputField();
    }

    public void Submit()
    {
        if (input.text.Length == 0) return;
        message.text = submit("M-" + input.text.PadLeft(4, '0'));
        if (panel.activeSelf) input.ActivateInputField(); // 다시 입력하기 쉽게
    }

    public void Close() => panel.SetActive(false);
}
