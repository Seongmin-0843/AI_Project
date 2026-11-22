using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

// 글 강조 표시: [클릭할 것] = 굵게 + 색 + 괄호, {중요한 규정} = 굵게 + 형광펜(Highlighter가 칠함)
public static class Emphasis
{
    // 형광펜을 칠할 글자는 이 색으로 찾는다 (다른 글자에 쓰지 않는 값)
    public static readonly Color32 MarkOnPaper = new Color32(0x2B, 0x1A, 0x01, 255), MarkOnDark = new Color32(0xFF, 0xF6, 0xD1, 255);

    public static string Rich(string text, bool onDark)
    {
        string click = onDark ? "#FFC94A" : "#9C1F1F", mark = onDark ? "#FFF6D1" : "#2B1A01";
        text = Regex.Replace(text, @"\[([^\]\n]+)\]", $"<b><color={click}>[$1]</color></b>");
        return Regex.Replace(text, @"\{([^}\n]+)\}", $"<b><color={mark}>$1</color></b>");
    }
}

// {중요} 글자 뒤에 형광펜을 칠한다. 실행 중에 Text 바로 앞 형제로 붙어 Text의 자리를 따라다닌다 (씬에 미리 둘 필요 없음).
public class Highlighter : MaskableGraphic
{
    Text target;
    string drawn;

    public static void Attach(Text text)
    {
        var go = new GameObject("Highlight", typeof(RectTransform), typeof(CanvasRenderer));
        var h = go.AddComponent<Highlighter>();
        h.target = text;
        h.raycastTarget = false;
        h.color = new Color(1f, 0.8f, 0f, 0.45f);
        h.Follow();
    }

    void LateUpdate()
    {
        if (target == null) return;
        Follow();
        if (drawn == target.text) return;
        drawn = target.text;
        SetVerticesDirty();
    }

    // 글자와 같은 부모·자리, 그리는 순서는 글자 바로 뒤쪽 (튜토리얼 안내 줄처럼 부모가 바뀌어도 따라감)
    void Follow()
    {
        var t = target.rectTransform;
        if (transform.parent != t.parent) transform.SetParent(t.parent, false);
        if (transform.GetSiblingIndex() != t.GetSiblingIndex() - 1) transform.SetSiblingIndex(Mathf.Max(0, t.GetSiblingIndex() - (transform.GetSiblingIndex() < t.GetSiblingIndex() ? 1 : 0)));
        var rt = rectTransform;
        rt.anchorMin = t.anchorMin;
        rt.anchorMax = t.anchorMax;
        rt.pivot = t.pivot;
        rt.sizeDelta = t.sizeDelta;
        rt.anchoredPosition = t.anchoredPosition;
        rt.localScale = t.localScale;
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (target == null || string.IsNullOrEmpty(target.text)) return;

        var gen = target.cachedTextGenerator;
        gen.PopulateWithErrors(target.text, target.GetGenerationSettings(target.rectTransform.rect.size), target.gameObject);
        var verts = gen.verts;
        float unit = 1f / target.pixelsPerUnit;
        bool open = false;
        var run = new Rect();

        // 글자 하나 = 꼭짓점 4개. 같은 줄에서 이어지는 강조 글자를 한 칸으로 묶는다
        for (int i = 0; i + 3 < verts.Count; i += 4)
        {
            Vector2 a = verts[i].position, b = verts[i + 2].position;
            var q = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
            if (q.width < 0.01f) continue; // 그려지지 않는 글자 (공백, 태그)

            bool mark = Same(verts[i].color, Emphasis.MarkOnPaper) || Same(verts[i].color, Emphasis.MarkOnDark);
            if (open && (!mark || q.xMin < run.xMin)) { Draw(vh, run, unit); open = false; } // 강조가 끝났거나 다음 줄
            if (!mark) continue;
            run = open ? Rect.MinMaxRect(run.xMin, Mathf.Min(run.yMin, q.yMin), q.xMax, Mathf.Max(run.yMax, q.yMax)) : q;
            open = true;
        }
        if (open) Draw(vh, run, unit);
    }

    static bool Same(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b;

    void Draw(VertexHelper vh, Rect r, float unit)
    {
        const float pad = 3f;
        float x0 = r.xMin * unit - pad, x1 = r.xMax * unit + pad, y0 = r.yMin * unit - pad, y1 = r.yMax * unit + pad;
        int n = vh.currentVertCount;
        vh.AddVert(new Vector3(x0, y0), color, Vector2.zero);
        vh.AddVert(new Vector3(x0, y1), color, Vector2.zero);
        vh.AddVert(new Vector3(x1, y1), color, Vector2.zero);
        vh.AddVert(new Vector3(x1, y0), color, Vector2.zero);
        vh.AddTriangle(n, n + 1, n + 2);
        vh.AddTriangle(n, n + 2, n + 3);
    }
}
