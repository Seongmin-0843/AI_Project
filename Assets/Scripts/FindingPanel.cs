using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Finding
{
    public string Key;        // 정렬한 항목 ID를 +로 이은 값. 판정 때 템플릿 오류 위치와 비교
    public string[] FieldIds;
    public string Label;
    public bool Misread;    // 번호를 잘못 입력해 나온 조회 기록을 근거로 든 반려 사유 (맞는 사유로 치지 않음)
    public GameObject Item; // 발견 목록의 줄

    public bool UsesRecord => Key.Contains("Lookup.") || Key.Contains("Restore."); // 조회 기록 칸의 항목을 씀
}

// 항목 선택(최대 2개) → [기록] → 발견 목록. 목록 줄을 클릭하면 삭제.
public class FindingPanel : MonoBehaviour
{
    public RectTransform listContainer;
    public Button itemTemplate;

    readonly List<FieldRow> selected = new List<FieldRow>();
    readonly List<Finding> findings = new List<Finding>();

    public IReadOnlyList<Finding> Findings => findings;
    public bool IsSelected(string fieldId) => selected.Exists(r => r != null && r.Id == fieldId);
    public bool Has(string key) => findings.Exists(f => f.Key == key);

    public void Toggle(FieldRow row)
    {
        if (selected.Remove(row)) { row.SetSelected(false); return; }
        if (selected.Count == 2)
        {
            selected[0].SetSelected(false);
            selected.RemoveAt(0);
        }
        selected.Add(row);
        row.SetSelected(true);
    }

    public void Record()
    {
        if (selected.Count == 0) return;
        var ids = selected.ConvertAll(r => r.Id);
        ids.Sort(StringComparer.Ordinal);
        Add(ids.ToArray(), string.Join(" ↔ ", selected.ConvertAll(r => r.Label)));
        ClearSelection();
    }

    // 직접 기록하거나, 단말기 조회처럼 자동으로 넣는다 (같은 조합은 하나만 남김)
    public void Add(string[] sortedIds, string label)
    {
        var key = string.Join("+", sortedIds);
        if (findings.Exists(f => f.Key == key)) return;
        var finding = new Finding { Key = key, FieldIds = sortedIds, Label = label };
        findings.Add(finding);
        AddItem(finding);
    }

    // 다른 번호로 다시 조회하면 앞 조회 기록으로 기록한 발견은 지운다
    public void RemoveRecordFindings()
    {
        foreach (var f in findings.FindAll(f => f.UsesRecord))
        {
            findings.Remove(f);
            Destroy(f.Item);
        }
    }

    public void Clear()
    {
        ClearSelection();
        findings.Clear();
        foreach (Transform child in listContainer) Destroy(child.gameObject);
    }

    public void ClearSelection()
    {
        foreach (var r in selected) if (r != null) r.SetSelected(false);
        selected.Clear();
    }

    void AddItem(Finding finding)
    {
        var item = Instantiate(itemTemplate, listContainer);
        item.gameObject.SetActive(true);
        finding.Item = item.gameObject;
        item.GetComponentInChildren<Text>().text = finding.Label.Replace("↔ ", "↔\n"); // 좁은 종이라 두 줄로
        item.onClick.AddListener(() =>
        {
            findings.Remove(finding);
            Destroy(item.gameObject);
        });
    }
}
