using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 기억 복원 창 (기획서 4-3): 섞인 글 카드 3장을 일어난 순서대로 클릭하고 [확인].
// 고른 카드를 다시 클릭하면 빠진다. 3회 틀리면 정답 순서를 자동으로 보여 준다.
public class RestorePanel : MonoBehaviour
{
    public GameObject panel;
    public Button[] cards = new Button[3];
    public Button confirmButton;
    public Text message, result, closeLabel;
    [Tooltip("남은 기회 (그림의 | 오른쪽). 비어 있으면 message 뒤에 붙인다")]
    public Text tries;
    [Tooltip("카드 배경색: 고르기 전 / 고른 뒤 (그림 위에서는 투명 / 옅은 색)")]
    public Color normalColor = new Color(0.94f, 0.92f, 0.86f), pickedColor = new Color(1f, 0.88f, 0.3f);
    [Tooltip("카드에 붙는 순서 번호 색")]
    public Color numberColor = new Color(0.1f, 0.1f, 0.1f);

    Transaction tx;
    MemorySet set;
    Action onClose;
    readonly List<int> picks = new List<int>(); // 고른 카드 (세트 안의 정답 순서 번호)
    public IReadOnlyList<int> Picks => picks; // 튜토리얼 안내가 읽음

    void Awake()
    {
        panel.SetActive(false);
        for (int i = 0; i < cards.Length; i++)
        {
            int slot = i;
            cards[i].onClick.AddListener(() => ToggleCard(slot));
        }
    }

    public void Show(Transaction transaction, Action closed)
    {
        tx = transaction;
        set = transaction.Memory;
        onClose = closed;
        picks.Clear();
        panel.SetActive(true);
        if (tx.RestoreDone) ShowSolved(tx.Restored ? "복원된 기억입니다." : "3회 실패하여 정답 순서로 자동 복원했습니다.");
        else
        {
            Say("일어난 순서대로 카드를 클릭하세요.", Left());
            result.text = "";
            SetConfirm(true);
            closeLabel.text = "닫기";
            Refresh();
        }
    }

    void ToggleCard(int slot)
    {
        if (tx.RestoreDone) return;
        int card = tx.CardOrder[slot];
        if (!picks.Remove(card) && picks.Count < 3) picks.Add(card);
        Refresh();
    }

    public void Confirm()
    {
        if (tx.RestoreDone) return;
        if (picks.Count < 3) { Say("카드 3장을 모두 순서대로 고르세요.", Left()); return; }

        if (picks[0] == 0 && picks[1] == 1 && picks[2] == 2)
        {
            tx.Restored = true;
            ShowSolved(tx.RestoreFailures == 0 ? "복원에 성공했습니다." : "복원에 성공했습니다. (재시도)");
            return;
        }
        tx.RestoreFailures++;
        picks.Clear();
        if (tx.RestoreDone) ShowSolved("3회 실패했습니다. 정답 순서로 자동 복원합니다.");
        else
        {
            Say("순서가 맞지 않습니다. 다시 배열하세요.", Left());
            Refresh();
        }
    }

    void ShowSolved(string msg)
    {
        picks.Clear();
        picks.AddRange(new[] { 0, 1, 2 });
        Say(msg, tx.Restored ? "복원 완료" : "자동 복원");
        result.text = tries != null ? $"<b>복원된 기억</b>   {set.Result}" : $"<b>복원된 기억</b>\n{set.Result}"; // 그림에서는 카드 아래 한 줄
        SetConfirm(false);
        closeLabel.text = "서류로 돌아가기";
        Refresh();
    }

    void Refresh()
    {
        for (int i = 0; i < cards.Length; i++)
        {
            int card = tx.CardOrder[i];
            int n = picks.IndexOf(card);
            cards[i].image.color = n >= 0 ? pickedColor : normalColor;
            // 번호 줄과 같은 높이의 투명한 줄을 아래에도 둬 문장이 카드 한가운데에 오게 (빈 줄만 두면 높이에 안 들어감)
            const string pad = "<b><color=#00000000>0</color></b>";
            string number = n >= 0 ? $"<b><color=#{ColorUtility.ToHtmlStringRGB(numberColor)}>{n + 1}</color></b>" : pad;
            var text = cards[i].GetComponentInChildren<Text>();
            text.verticalOverflow = VerticalWrapMode.Overflow; // 넘친 줄을 잘라 내면 가운데가 틀어짐
            text.text = $"{number}\n{set.Cards[card]}\n{pad}";
        }
    }

    // 복원이 끝나면 [확인]을 끈다. 그림에 버튼 자리가 있어 숨기지 않고 흐리게
    void SetConfirm(bool on)
    {
        confirmButton.interactable = on;
        confirmButton.GetComponentInChildren<Text>().canvasRenderer.SetAlpha(on ? 1f : 0.4f);
    }

    string Left() => $"남은 기회 {Scoring.MaxRestoreTries - tx.RestoreFailures}회";

    void Say(string msg, string side)
    {
        if (tries != null) { message.text = msg; tries.text = side; }
        else message.text = $"{msg} ({side})";
    }

    public void Close()
    {
        panel.SetActive(false);
        onClose?.Invoke();
    }
}
