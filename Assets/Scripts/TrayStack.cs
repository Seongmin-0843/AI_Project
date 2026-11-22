using System.Collections.Generic;
using UnityEngine;

// 서류함에 넣은 서류를 한 장씩 쌓아 보여 준다. 서류함(부모)의 BoxCollider 바닥 기준.
[RequireComponent(typeof(BoxCollider))]
public class TrayStack : MonoBehaviour
{
    public Material paper;
    [Tooltip("서류함 바닥 두께 (m). 종이가 바닥에 묻히거나 떠 보이면 조정")]
    public float floorOffset = 0.01f;
    [Tooltip("종이 한 장 두께 (m)")]
    public float thickness = 0.004f;

    readonly List<GameObject> sheets = new List<GameObject>();

    public void Add()
    {
        var col = GetComponent<BoxCollider>();
        var ls = transform.lossyScale;
        var size = Vector3.Scale(col.size, ls); // 서류함 실제 크기 (m)
        float w = Mathf.Min(0.21f, size.x * 0.8f), d = Mathf.Min(0.297f, size.z * 0.8f); // A4, 서류함보다 크면 줄임

        var sheet = GameObject.CreatePrimitive(PrimitiveType.Cube);
        sheet.name = "Sheet";
        Destroy(sheet.GetComponent<Collider>()); // 서류함 클릭을 가리지 않게
        sheet.GetComponent<MeshRenderer>().sharedMaterial = paper;
        sheet.transform.SetParent(transform, false);
        sheet.transform.localRotation = Quaternion.Euler(0f, Random.Range(-4f, 4f), 0f);
        sheet.transform.localScale = new Vector3(w / ls.x, thickness / ls.y, d / ls.z);
        float y = col.center.y - col.size.y / 2f + (floorOffset + thickness * (sheets.Count + 0.5f)) / ls.y;
        sheet.transform.localPosition = new Vector3(col.center.x + Random.Range(-0.008f, 0.008f) / ls.x, y, col.center.z + Random.Range(-0.008f, 0.008f) / ls.z);
        sheets.Add(sheet);
    }

    // 다음 종이가 놓일 자리 (서류함 바닥 가운데 + 쌓인 높이, 월드 좌표)
    public Vector3 NextTop()
    {
        var col = GetComponent<BoxCollider>();
        float y = col.center.y - col.size.y / 2f + (floorOffset + thickness * sheets.Count) / transform.lossyScale.y;
        return transform.TransformPoint(new Vector3(col.center.x, y, col.center.z));
    }

    // 가로 w·세로 d(m) 종이가 서류함 안(폭의 80%)에 들어가는 배율 (1 이하)
    public float Fit(float w, float d)
    {
        var size = Vector3.Scale(GetComponent<BoxCollider>().size, transform.lossyScale);
        return Mathf.Min(1f, size.x * 0.8f / w, size.z * 0.8f / d);
    }

    // 도장 연출의 종이를 그대로 쌓는다. 서류함 비율(가로 1.5배 등)에 찌그러지지 않게 부모로 붙이지 않고 자리만 둔다
    public void Keep(GameObject paper) => sheets.Add(paper);

    public void Clear()
    {
        foreach (var s in sheets) Destroy(s);
        sheets.Clear();
    }
}
