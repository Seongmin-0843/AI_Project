using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// 클릭 가능한 책상 위 물체. Collider는 이 오브젝트에, 겉모습은 자식 Visual에 둔다.
// 마우스를 올리면 Visual이 커지고, 올려 둔 동안 커진 상태를 유지한다.
public class Clickable : MonoBehaviour
{
    public string displayName;
    public UnityEvent onClicked = new UnityEvent();
    [Tooltip("마우스를 올렸을 때 배율")]
    public float hoverScale = 1.1f;

    Transform visual;
    Vector3 baseScale, basePos;
    bool hovered;
    float k = 1f;

    // 튜토리얼 강조: 모델이 노란빛으로 밝아졌다 어두워지기를 반복 (URP Lit의 Emission)
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    static readonly Color GlowColor = new Color(1f, 0.8f, 0.3f);
    List<(Material mat, Color baseColor)> glowMats;
    bool glowing;

    public void SetGlow(bool on)
    {
        if (on == glowing) return;
        glowing = on;
        if (glowMats == null)
        {
            glowMats = new List<(Material, Color)>();
            foreach (var r in (visual != null ? visual : transform).GetComponentsInChildren<Renderer>())
                foreach (var m in r.materials) // 이 물체 전용 복사본 (다른 물체 재질은 그대로)
                {
                    if (!m.HasProperty(EmissionId)) continue;
                    m.EnableKeyword("_EMISSION");
                    glowMats.Add((m, m.GetColor(EmissionId)));
                }
        }
        if (!on) foreach (var (m, c) in glowMats) m.SetColor(EmissionId, c);
    }

    void Awake()
    {
        visual = transform.Find("Visual");
        if (visual == null) return;
        baseScale = visual.localScale;
        basePos = visual.localPosition;
    }

    void Update()
    {
        if (glowing)
        {
            float pulse = 0.15f + 0.6f * (Mathf.Sin(Time.time * 5f) + 1f) * 0.5f;
            foreach (var (m, c) in glowMats) m.SetColor(EmissionId, c + GlowColor * pulse);
        }
        if (visual == null) return;
        k = Mathf.Lerp(k, hovered ? hoverScale : 1f, 1f - Mathf.Exp(-20f * Time.deltaTime));
        // 위치도 같이 곱해 부모 피벗(바닥 가운데) 기준으로 커지게 → 책상에 파묻히지 않음
        visual.localScale = baseScale * k;
        visual.localPosition = basePos * k;
    }

    public void SetHover(bool on) => hovered = on;

    public void Click() => onClicked.Invoke();
}
