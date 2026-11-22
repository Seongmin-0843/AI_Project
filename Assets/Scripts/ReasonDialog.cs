using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 반려 도장을 찍을 때 뜨는 사유 선택 창. 발견 목록 전체가 체크된 상태로 시작한다.
public class ReasonDialog : MonoBehaviour
{
    public GameObject panel;
    public RectTransform listContainer;
    public Button itemTemplate;
    public Text emptyLabel;

    static readonly Color On = new Color(1f, 0.88f, 0.3f);
    static readonly Color Off = new Color(0.85f, 0.85f, 0.8f);

    readonly List<(Finding finding, Button button)> items = new List<(Finding, Button)>();
    readonly HashSet<Finding> checkedSet = new HashSet<Finding>();
    Action<List<Finding>> onConfirm;

    void Awake() => panel.SetActive(false);

    public void Show(IReadOnlyList<Finding> findings, Action<List<Finding>> confirm)
    {
        onConfirm = confirm;
        foreach (var (_, b) in items) Destroy(b.gameObject);
        items.Clear();
        checkedSet.Clear();

        foreach (var f in findings)
        {
            var button = Instantiate(itemTemplate, listContainer);
            button.gameObject.SetActive(true);
            button.GetComponentInChildren<Text>().text = f.Label;
            button.onClick.AddListener(() => Toggle(f, button));
            checkedSet.Add(f);
            button.image.color = On;
            items.Add((f, button));
        }
        emptyLabel.gameObject.SetActive(findings.Count == 0);
        panel.SetActive(true);
    }

    void Toggle(Finding f, Button button)
    {
        bool on = !checkedSet.Remove(f);
        if (on) checkedSet.Add(f);
        button.image.color = on ? On : Off;
    }

    public void Confirm()
    {
        panel.SetActive(false);
        onConfirm?.Invoke(items.ConvertAll(i => i.finding).FindAll(checkedSet.Contains));
    }

    public void Cancel() => panel.SetActive(false);
}
