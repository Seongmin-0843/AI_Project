using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 서류의 한 항목 (예: Application.Name). 클릭하면 발견 패널의 선택을 토글한다.
[RequireComponent(typeof(Image))]
public class FieldRow : MonoBehaviour, IPointerClickHandler
{
    public string Id, Label;
    public FindingPanel panel;

    static readonly Color Normal = new Color(1f, 1f, 1f, 0f);
    static readonly Color Selected = new Color(1f, 0.88f, 0.3f, 0.8f);

    public void SetSelected(bool on) => GetComponent<Image>().color = on ? Selected : Normal;

    public void OnPointerClick(PointerEventData e) => panel.Toggle(this);
}
