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

    static readonly Color Normal = new Color(0.94f, 0.92f, 0.86f);
    static readonly Color Picked = new Color(1f, 0.88f, 0.3f);

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
            message.text = $"일어난 순서대로 카드를 클릭하세요. (남은 기회 {Scoring.MaxRestoreTries - tx.RestoreFailures}번)";
            result.text = "";
            confirmButton.gameObject.SetActive(true);
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
        if (picks.Count < 3) { message.text = "카드 3장을 모두 순서대로 고르세요."; return; }

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
            message.text = $"순서가 맞지 않습니다. 다시 배열하세요. (남은 기회 {Scoring.MaxRestoreTries - tx.RestoreFailures}번)";
            Refresh();
        }
    }

    void ShowSolved(string msg)
    {
        picks.Clear();
        picks.AddRange(new[] { 0, 1, 2 });
        message.text = msg;
        result.text = $"<b>복원된 기억</b>\n{set.Result}";
        confirmButton.gameObject.SetActive(false);
        closeLabel.text = "서류로 돌아가기";
        Refresh();
    }

    void Refresh()
    {
        for (int i = 0; i < cards.Length; i++)
        {
            int card = tx.CardOrder[i];
            int n = picks.IndexOf(card);
            cards[i].image.color = n >= 0 ? Picked : Normal;
            cards[i].GetComponentInChildren<Text>().text = (n >= 0 ? $"<b>{n + 1}</b>\n\n" : "\n\n") + set.Cards[card];
        }
    }

    public void Close()
    {
        panel.SetActive(false);
        onClose?.Invoke();
    }
}
