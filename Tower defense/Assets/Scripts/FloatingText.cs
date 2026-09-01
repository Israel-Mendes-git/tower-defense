using UnityEngine;
using TMPro;

// Texto flutuante no mundo (ex: "+10" ao matar um inimigo): sobe e some.
public class FloatingText : MonoBehaviour
{
    private const float Life = 0.8f;
    private const float RiseSpeed = 1.3f;

    private TextMeshPro tmp;
    private float t;

    public static void Spawn(Vector3 worldPos, string text, Color color)
    {
        var go = new GameObject("FloatingText");
        go.transform.position = worldPos + Vector3.up * 0.3f;
        go.transform.localScale = Vector3.one * 0.12f;

        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = 8;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = color;
        tmp.fontStyle = FontStyles.Bold;

        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null) mr.sortingOrder = 200; // por cima dos sprites

        go.AddComponent<FloatingText>().tmp = tmp;
    }

    private void Update()
    {
        t += Time.deltaTime;
        transform.position += Vector3.up * RiseSpeed * Time.deltaTime;
        float p = Mathf.Clamp01(t / Life);
        Color c = tmp.color;
        c.a = 1f - p;
        tmp.color = c;
        if (t >= Life) Destroy(gameObject);
    }
}
