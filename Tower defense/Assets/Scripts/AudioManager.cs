using System.Collections.Generic;
using UnityEngine;

// Som do jogo. Os clipes são SINTETIZADOS em runtime — o projeto não tem assets de áudio, e gerar
// as ondas por código evita depender de arquivos externos para o jogo deixar de ser mudo.
// Se você trocar por samples reais depois, basta preencher os campos do inspetor: eles têm prioridade.
public class AudioManager : MonoBehaviour
{
    public static AudioManager main;

    public enum Sfx { Shoot, Explosion, Pop, Buy, Upgrade, PlayerHurt, Ability, Victory, Defeat }

    [Header("Volumes")]
    [SerializeField, Range(0f, 1f)] private float masterVolume = 0.6f;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 0.7f;

    [Header("Substituições opcionais (deixe vazio para usar o som sintetizado)")]
    [SerializeField] private AudioClip shootOverride;
    [SerializeField] private AudioClip explosionOverride;
    [SerializeField] private AudioClip popOverride;
    [SerializeField] private AudioClip buyOverride;

    private const int SampleRate = 44100;

    private readonly Dictionary<Sfx, AudioClip> clips = new Dictionary<Sfx, AudioClip>();
    private AudioSource[] sources;
    private int nextSource;

    // Muitos inimigos morrem no mesmo frame; sem um teto, o áudio satura e vira ruído branco.
    private readonly Dictionary<Sfx, float> lastPlayed = new Dictionary<Sfx, float>();
    private const float MinInterval = 0.04f;

    private void Awake()
    {
        if (main != null && main != this) { Destroy(this); return; }
        main = this;

        // Um punhado de fontes em rodízio permite sons simultâneos sem cortar uns aos outros.
        sources = new AudioSource[8];
        for (int i = 0; i < sources.Length; i++)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f; // 2D
            sources[i] = src;
        }

        BuildClips();
    }

    private void BuildClips()
    {
        // Não usar `??` aqui: o operador nulo do C# ignora a sobrecarga de == de UnityEngine.Object
        // e devolve o "fake null" de um campo vazio do inspetor, deixando o clipe nulo.
        clips[Sfx.Shoot]      = Or(shootOverride,     Tone("sfx_shoot", 0.07f, 880f, 440f, 0.25f, Wave.Square));
        clips[Sfx.Explosion]  = Or(explosionOverride, Noise("sfx_explosion", 0.35f, 0.5f));
        clips[Sfx.Pop]        = Or(popOverride,       Tone("sfx_pop", 0.10f, 300f, 900f, 0.35f, Wave.Sine));
        clips[Sfx.Buy]        = Or(buyOverride,       Arpeggio("sfx_buy", new[] { 523f, 659f, 784f }, 0.07f));
        clips[Sfx.Upgrade]    = Arpeggio("sfx_upgrade", new[] { 523f, 659f, 784f, 1047f }, 0.08f);
        clips[Sfx.PlayerHurt] = Tone("sfx_hurt", 0.35f, 220f, 90f, 0.5f, Wave.Saw);
        clips[Sfx.Ability]    = Arpeggio("sfx_ability", new[] { 392f, 523f, 659f, 880f }, 0.09f);
        clips[Sfx.Victory]    = Arpeggio("sfx_victory", new[] { 523f, 659f, 784f, 1047f, 1319f }, 0.14f);
        clips[Sfx.Defeat]     = Arpeggio("sfx_defeat", new[] { 440f, 349f, 262f, 196f }, 0.18f);
    }

    // Escolhe o clipe do inspetor se ele realmente existir; senão, o sintetizado.
    private static AudioClip Or(AudioClip preferido, AudioClip alternativa)
        => preferido != null ? preferido : alternativa;

    public void Play(Sfx sfx, float volumeScale = 1f)
    {
        if (!clips.TryGetValue(sfx, out AudioClip clip) || clip == null) return;

        if (lastPlayed.TryGetValue(sfx, out float t) && Time.unscaledTime - t < MinInterval) return;
        lastPlayed[sfx] = Time.unscaledTime;

        AudioSource src = sources[nextSource];
        nextSource = (nextSource + 1) % sources.Length;
        src.pitch = Random.Range(0.94f, 1.06f); // pequena variação evita a repetição soar mecânica
        src.PlayOneShot(clip, masterVolume * sfxVolume * volumeScale);
    }

    // Atalho seguro: som é opcional, então nunca deve quebrar quem chama.
    public static void Cue(Sfx sfx, float volumeScale = 1f)
    {
        if (main != null) main.Play(sfx, volumeScale);
    }

    // ───────── Síntese ─────────
    private enum Wave { Sine, Square, Saw }

    // Tom com varredura de frequência (de startHz a endHz) e decaimento exponencial.
    private static AudioClip Tone(string name, float duration, float startHz, float endHz, float decay, Wave wave)
    {
        int count = Mathf.CeilToInt(SampleRate * duration);
        var data = new float[count];
        float phase = 0f;

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / count;
            float hz = Mathf.Lerp(startHz, endHz, t);
            phase += hz / SampleRate;
            float raw = Shape(phase, wave);
            float envelope = Mathf.Exp(-t / Mathf.Max(0.0001f, decay)) * Fade(t);
            data[i] = raw * envelope * 0.5f;
        }

        var clip = AudioClip.Create(name, count, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // Ruído filtrado com queda — serve de explosão.
    private static AudioClip Noise(string name, float duration, float decay)
    {
        int count = Mathf.CeilToInt(SampleRate * duration);
        var data = new float[count];
        float low = 0f;

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / count;
            float white = Random.Range(-1f, 1f);
            low = Mathf.Lerp(low, white, 0.12f); // passa-baixa: deixa grave, não chiado
            float envelope = Mathf.Exp(-t / Mathf.Max(0.0001f, decay)) * Fade(t);
            data[i] = low * envelope * 0.9f;
        }

        var clip = AudioClip.Create(name, count, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // Sequência de notas curtas — usado nas confirmações (compra, upgrade, vitória).
    private static AudioClip Arpeggio(string name, float[] notes, float noteDuration)
    {
        int perNote = Mathf.CeilToInt(SampleRate * noteDuration);
        var data = new float[perNote * notes.Length];

        for (int n = 0; n < notes.Length; n++)
        {
            float phase = 0f;
            for (int i = 0; i < perNote; i++)
            {
                float t = (float)i / perNote;
                phase += notes[n] / SampleRate;
                float envelope = Mathf.Exp(-t / 0.35f) * Fade(t);
                data[n * perNote + i] = Shape(phase, Wave.Sine) * envelope * 0.45f;
            }
        }

        var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static float Shape(float phase, Wave wave)
    {
        float p = phase - Mathf.Floor(phase);
        switch (wave)
        {
            case Wave.Square: return p < 0.5f ? 1f : -1f;
            case Wave.Saw:    return p * 2f - 1f;
            default:          return Mathf.Sin(p * Mathf.PI * 2f);
        }
    }

    // Rampa curtíssima nas pontas para não estalar no início/fim do clipe.
    private static float Fade(float t)
    {
        const float edge = 0.02f;
        if (t < edge) return t / edge;
        if (t > 1f - edge) return (1f - t) / edge;
        return 1f;
    }
}
