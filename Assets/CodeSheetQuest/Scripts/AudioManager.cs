using UnityEngine;

// Plays the background music and the sound effects.
// There were no sound files in the project so I make the sounds in code
// (sine waves for the beeps and random noise for the explosion).
// If you drag your own clip into a slot it uses that one instead.
public class AudioManager : MonoBehaviour
{
    [Header("Optional sound files (leave empty = made in code)")]
    public AudioClip music;
    public AudioClip clickSound;
    public AudioClip pickupSound;
    public AudioClip dropSound;
    public AudioClip insertSound;
    public AudioClip acceptSound;
    public AudioClip blastSound;
    public AudioClip celebrationSound;

    [Header("Volume")]
    [Range(0f, 1f)] public float musicVolume = 0.12f;
    [Range(0f, 1f)] public float effectsVolume = 0.6f;

    AudioSource musicSource;
    AudioSource effectsSource;
    int sampleRate = 22050;

    void Awake()
    {
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.loop = true;
        effectsSource = gameObject.AddComponent<AudioSource>();

        if (music == null) music = MakeMusic();
        if (clickSound == null) clickSound = MakeBeeps("Click", new float[] { 880f }, 0f, 0.15f);
        if (pickupSound == null) pickupSound = MakeBeeps("Pickup", new float[] { 523f, 784f }, 0.06f, 0.25f);
        if (dropSound == null) dropSound = MakeBeeps("Drop", new float[] { 330f, 196f }, 0.06f, 0.25f);
        if (insertSound == null) insertSound = MakeBeeps("Insert", new float[] { 1046f, 784f, 523f }, 0.07f, 0.2f);
        if (acceptSound == null) acceptSound = MakeBeeps("Accept", new float[] { 659f, 784f, 1046f }, 0.09f, 0.45f);
        if (celebrationSound == null) celebrationSound = MakeBeeps("Celebration", new float[] { 523f, 659f, 784f, 1046f, 784f, 1046f, 1318f }, 0.12f, 0.6f);
        if (blastSound == null) blastSound = MakeExplosion();

        musicSource.clip = music;
        musicSource.volume = musicVolume;
        effectsSource.volume = effectsVolume;
        musicSource.Play();
    }

    void Update()
    {
        // so you can move the volume sliders while playing
        musicSource.volume = musicVolume;
        effectsSource.volume = effectsVolume;
    }

    public void PlayClick() { Play(clickSound); }
    public void PlayPickup() { Play(pickupSound); }
    public void PlayDrop() { Play(dropSound); }
    public void PlayInsert() { Play(insertSound); }
    public void PlayAccept() { Play(acceptSound); }
    public void PlayBlast() { Play(blastSound); }
    public void PlayCelebration() { Play(celebrationSound); }

    void Play(AudioClip clip)
    {
        if (clip != null) effectsSource.PlayOneShot(clip);
    }

    // a few notes played one after another
    AudioClip MakeBeeps(string clipName, float[] notes, float gap, float noteLength)
    {
        float totalTime = gap * notes.Length + noteLength;
        float[] samples = new float[(int)(sampleRate * totalTime)];

        for (int n = 0; n < notes.Length; n++)
        {
            int start = (int)(sampleRate * gap * n);
            int length = (int)(sampleRate * noteLength);
            for (int i = 0; i < length && start + i < samples.Length; i++)
            {
                float t = (float)i / sampleRate;
                float fadeIn = Mathf.Clamp01(t / 0.005f);
                float fadeOut = Mathf.Exp(-t * 7f) * Mathf.Clamp01((noteLength - t) / 0.02f);
                samples[start + i] += Mathf.Sin(2f * Mathf.PI * notes[n] * t) * fadeIn * fadeOut * 0.3f;
            }
        }
        return MakeClip(clipName, samples);
    }

    // BOOM = a loud crack + noise that fades out + a deep rumble
    AudioClip MakeExplosion()
    {
        float[] samples = new float[(int)(sampleRate * 1.8f)];
        float noise = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            float t = (float)i / sampleRate;
            noise = noise + (Random.Range(-1f, 1f) - noise) * 0.25f; // smoothed noise sounds deeper
            float crack = Random.Range(-1f, 1f) * Mathf.Exp(-t * 30f);
            float rumble = Mathf.Sin(2f * Mathf.PI * 50f * t) * Mathf.Exp(-t * 2.5f);
            float fadeIn = Mathf.Clamp01(t / 0.003f);
            samples[i] = (noise * 2.2f * Mathf.Exp(-t * 2.8f) + crack * 0.8f + rumble * 0.6f) * fadeIn;
        }
        return MakeClip("Explosion", samples);
    }

    // four soft chords (Am, F, C, G) that fade into each other and loop
    AudioClip MakeMusic()
    {
        float[,] chords =
        {
            { 220.00f, 261.63f, 329.63f },
            { 174.61f, 220.00f, 261.63f },
            { 196.00f, 261.63f, 329.63f },
            { 196.00f, 246.94f, 293.66f },
        };
        int chordLength = sampleRate * 4;
        int total = chordLength * 4;
        float[] samples = new float[total];

        for (int c = 0; c < 4; c++)
        {
            int start = c * chordLength - chordLength / 2;
            int length = chordLength * 2;
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / sampleRate;
                float fade = Mathf.Sin(Mathf.PI * i / length);
                fade = fade * fade;
                float sound = 0f;
                for (int note = 0; note < 3; note++)
                    sound += Mathf.Sin(2f * Mathf.PI * chords[c, note] * t);
                int index = ((start + i) % total + total) % total; // wrap around so the loop is smooth
                samples[index] += sound * fade * 0.12f;
            }
        }
        return MakeClip("Music", samples);
    }

    AudioClip MakeClip(string clipName, float[] samples)
    {
        for (int i = 0; i < samples.Length; i++)
            samples[i] = Mathf.Clamp(samples[i], -1f, 1f);
        AudioClip clip = AudioClip.Create(clipName, samples.Length, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
