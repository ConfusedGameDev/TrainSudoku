using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TrainSudoku.VisionOS.Editor
{
    /// <summary>
    /// Writes the Vision Pro edition's music: one arrival jingle per line and two background loops, synthesised here
    /// so they can be regenerated, retuned or re-seeded rather than being opaque files.
    /// </summary>
    /// <remarks>
    /// <b>The brief</b> (2026-10-04): music "which resembles the Yamanote line station jingles" — the short departure
    /// melodies (発車メロディ) Tokyo's stations play. Those are copyrighted compositions, so nothing here transcribes
    /// one. What is borrowed is the <b>form</b> they share: about seven seconds, a major key, a bright bell or
    /// electric-piano lead over a soft pad and bass, four bars that state a motif, answer it, climb to a peak and
    /// resolve home on the tonic. Each line's melody is generated from its own seed inside that form, so every line
    /// has its own jingle (as each station has its own melody) and re-running the composer reproduces them exactly.
    ///
    /// <b>The loops</b> are seamless: each is rendered with a tail past its last bar, and the tail — the reverb and
    /// the last notes ringing on — is folded back onto the start, which is what the start would have heard had the
    /// loop just come round.
    ///
    /// Output goes to <see cref="OutputFolder"/>, a <c>Resources</c> folder that <c>VisionOSMusic</c> loads by name.
    /// Window > TrainSudoku > VisionOS > Compose Music.
    /// </remarks>
    public static class VisionOSMusicComposer
    {
        public const string OutputFolder = "Assets/03.Data/VisionOS/Resources/VisionOSMusic";

        /// <summary>One per line in Network.asset. More lines than jingles reuse them in order (VisionOSMusic).</summary>
        public const int JingleCount = 24;

        const int Rate = 44100;

        [MenuItem("Window/TrainSudoku/VisionOS/Compose Music")]
        public static void ComposeAll()
        {
            Directory.CreateDirectory(OutputFolder);
            try
            {
                for (var line = 0; line < JingleCount; line++)
                {
                    EditorUtility.DisplayProgressBar("Compose Music", $"Jingle {line + 1} of {JingleCount}", line / (float)(JingleCount + 2));
                    Write($"Jingle-{line:00}", Jingle(line), 0.9f);
                }

                EditorUtility.DisplayProgressBar("Compose Music", "Concourse loop", JingleCount / (float)(JingleCount + 2));
                Write("Bgm-Concourse", Concourse(), 0.8f);
                EditorUtility.DisplayProgressBar("Compose Music", "Platform loop", (JingleCount + 1) / (float)(JingleCount + 2));
                Write("Bgm-Platform", Platform(), 0.8f);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.Refresh();
            ConfigureImporters();
            Debug.Log($"[AVP music] Composed {JingleCount} jingles and two loops into {OutputFolder}.");
        }

        // ------------------------------------------------------------------ the jingles

        static readonly int[] Major = { 0, 2, 4, 5, 7, 9, 11 };

        /// <summary>Chords as scale degrees of their root (0 = I). Four bars; the last bar is V then I.</summary>
        static readonly int[][] Progressions =
        {
            new[] { 0, 4, 3, 4 },
            new[] { 0, 5, 3, 4 },
            new[] { 3, 4, 2, 4 },
            new[] { 0, 3, 5, 4 },
            new[] { 0, 2, 3, 4 },
            new[] { 5, 3, 0, 4 },
        };

        /// <summary>Bar rhythms in eighths (each sums to 8).</summary>
        static readonly int[][] Rhythms =
        {
            new[] { 2, 2, 2, 2 },
            new[] { 1, 1, 2, 1, 1, 2 },
            new[] { 1, 1, 1, 1, 2, 2 },
            new[] { 3, 1, 2, 2 },
            new[] { 2, 1, 1, 2, 2 },
            new[] { 1, 1, 1, 1, 1, 1, 2 },
            new[] { 2, 2, 1, 1, 2 },
        };

        /// <summary>The cadence bar: the last note is the long tonic.</summary>
        static readonly int[][] Cadences =
        {
            new[] { 2, 2, 4 },
            new[] { 1, 1, 2, 4 },
            new[] { 2, 1, 1, 4 },
            new[] { 3, 1, 4 },
        };

        enum Voice { Bell, EPiano, Marimba, MusicBox, Vibes, Pad, Bass, Tick }

        static readonly Voice[] Leads = { Voice.Bell, Voice.EPiano, Voice.Marimba, Voice.Vibes, Voice.MusicBox };

        static Mix Jingle(int line)
        {
            var random = new System.Random(7919 * (line + 1));
            // Tonic between G4 and F5, so the melody sits in the bright register the platforms use.
            var tonic = 67 + random.Next(0, 11);
            var bpm = 118 + random.Next(0, 27);
            var eighth = 30.0 / bpm;
            var lead = Leads[line % Leads.Length];
            var sparkle = (line / Leads.Length) % 2 == 1;
            var progression = Progressions[random.Next(Progressions.Length)];

            var bar = 8 * eighth;
            var length = 4 * bar + 3.5;
            var mix = new Mix(length);

            var melody = Melody(random, tonic, progression);
            foreach (var note in melody)
            {
                var at = note.Start * eighth;
                var duration = note.Length * eighth;
                mix.Note(lead, at, note.Pitch, duration, 0.55f * note.Accent, 0.1f);
                if (sparkle) mix.Note(Voice.MusicBox, at, note.Pitch + 12, duration, 0.12f * note.Accent, 0.35f);
            }

            // The pad and bass under it: a chord per bar, the last bar split V then I, and the I held into the tail.
            for (var b = 0; b < 4; b++)
            {
                var root = progression[b];
                if (b < 3)
                {
                    Chord(mix, tonic - 12, root, b * bar, bar, 0.16f);
                    mix.Note(Voice.Bass, b * bar, Degree(tonic - 24, root), bar * 0.5, 0.35f, 0f);
                    mix.Note(Voice.Bass, b * bar + bar * 0.5, Degree(tonic - 24, root), bar * 0.5, 0.25f, 0f);
                }
                else
                {
                    Chord(mix, tonic - 12, 4, b * bar, bar * 0.5, 0.16f);
                    mix.Note(Voice.Bass, b * bar, Degree(tonic - 24, 4), bar * 0.5, 0.35f, 0f);
                    Chord(mix, tonic - 12, 0, b * bar + bar * 0.5, bar * 0.5 + 1.6, 0.18f);
                    mix.Note(Voice.Bass, b * bar + bar * 0.5, tonic - 24, bar * 0.5 + 1.6, 0.4f, 0f);
                }
            }

            mix.Reverb(0.28f);
            mix.FadeOut(0.6);
            return mix;
        }

        struct Note
        {
            public int Start;     // in eighths
            public int Length;    // in eighths
            public int Pitch;     // MIDI
            public float Accent;
        }

        /// <summary>
        /// Four bars: a motif, its answer (the same rhythm, moved to the next chord), a climb to the peak, and the
        /// cadence home. Strong beats take chord tones; the rest move by step, following the bar's contour.
        /// </summary>
        static List<Note> Melody(System.Random random, int tonic, int[] progression)
        {
            var notes = new List<Note>();
            var motif = Rhythms[random.Next(Rhythms.Length)];
            var climb = Rhythms[random.Next(Rhythms.Length)];
            var cadence = Cadences[random.Next(Cadences.Length)];
            var bars = new[] { motif, motif, climb, cadence };
            // The contour each bar leans towards: up, gently up, up to the peak, and down home.
            var contour = new[] { 1, 1, 2, -1 };

            // Scale steps from the tonic: 0 is the tonic, 7 the octave above.
            var step = random.Next(0, 3) * 2;   // start on 1, 3 or 5
            var lowest = -3;
            var highest = 11;

            for (var b = 0; b < 4; b++)
            {
                var start = 0;
                var rhythm = bars[b];
                for (var i = 0; i < rhythm.Length; i++)
                {
                    var last = b == 3 && i == rhythm.Length - 1;
                    var strong = start == 0 || start == 4;
                    // The last bar's chord is V until its back half, then I.
                    var chord = b < 3 ? progression[b] : (start < 4 ? 4 : 0);

                    if (last)
                    {
                        // Home: the tonic, the octave above when the melody is already high.
                        step = step >= 4 ? 7 : 0;
                    }
                    else if (b == 3 && i == rhythm.Length - 2)
                    {
                        // Into the tonic from a step away: the leading tone or the second.
                        step = step >= 4 ? 6 : 1;
                    }
                    else if (strong)
                    {
                        step = NearestChordTone(step + contour[b], chord, lowest, highest);
                    }
                    else
                    {
                        var move = random.Next(0, 10) < 7 ? 1 : 2;
                        var direction = contour[b] > 0 ? (random.Next(0, 10) < 7 ? 1 : -1) : (random.Next(0, 10) < 7 ? -1 : 1);
                        step = Mathf.Clamp(step + move * direction, lowest, highest);
                    }

                    notes.Add(new Note
                    {
                        Start = b * 8 + start,
                        Length = rhythm[i],
                        Pitch = Degree(tonic, step),
                        Accent = strong ? 1f : 0.8f,
                    });
                    start += rhythm[i];
                }

                // The motif's answer starts where the motif ended, a step higher.
                if (b == 0) step = Mathf.Clamp(step + 1, lowest, highest);
            }

            return notes;
        }

        static int NearestChordTone(int target, int chordRoot, int lowest, int highest)
        {
            var best = target;
            var bestDistance = int.MaxValue;
            for (var s = lowest; s <= highest; s++)
            {
                var degree = ((s % 7) + 7) % 7;
                var fromRoot = ((degree - chordRoot) % 7 + 7) % 7;
                if (fromRoot != 0 && fromRoot != 2 && fromRoot != 4) continue;
                var distance = Math.Abs(s - target);
                if (distance < bestDistance)
                {
                    best = s;
                    bestDistance = distance;
                }
            }

            return best;
        }

        /// <summary>The MIDI note <paramref name="step"/> scale steps above <paramref name="tonic"/> (negative goes below).</summary>
        static int Degree(int tonic, int step)
        {
            var octave = Mathf.FloorToInt(step / 7f);
            var degree = step - octave * 7;
            return tonic + octave * 12 + Major[degree];
        }

        /// <summary>A triad on <paramref name="root"/> (a scale degree), voiced close above <paramref name="tonic"/>.</summary>
        static void Chord(Mix mix, int tonic, int root, double at, double duration, float velocity)
        {
            for (var i = 0; i < 3; i++)
            {
                var pitch = Degree(tonic, root + 2 * i);
                while (pitch > tonic + 9) pitch -= 12;
                mix.Note(Voice.Pad, at, pitch, duration, velocity, (i - 1) * 0.3f);
            }
        }

        static void Chord7(Mix mix, int bass, int[] intervals, double at, double duration, float velocity)
        {
            for (var i = 0; i < intervals.Length; i++)
                mix.Note(Voice.Pad, at, bass + intervals[i], duration, velocity, (i - intervals.Length / 2f) * 0.25f);
        }

        // ------------------------------------------------------------------ the loops

        /// <summary>The menus and the maps: an unhurried concourse, electric piano over a pad, a chime now and then.</summary>
        static Mix Concourse()
        {
            const int bars = 16;
            const double bpm = 92;
            var beat = 60.0 / bpm;
            var bar = 4 * beat;
            var loop = bars * bar;
            var mix = new Mix(loop + 5.0);
            var random = new System.Random(4242);

            // F major: Fmaj7, Dm7, Bbmaj7, C sus4 → C, Am7, Dm7, Gm7, C7.
            var roots = new[] { 53, 50, 46, 48, 45, 50, 43, 48 };
            var shapes = new[]
            {
                new[] { 12, 16, 19, 23 }, new[] { 12, 15, 19, 22 }, new[] { 12, 16, 19, 23 }, new[] { 12, 17, 19, 24 },
                new[] { 12, 15, 19, 22 }, new[] { 12, 15, 19, 22 }, new[] { 12, 15, 19, 22 }, new[] { 12, 16, 19, 22 },
            };

            for (var b = 0; b < bars; b++)
            {
                var at = b * bar;
                var chord = b % 8;
                Chord7(mix, roots[chord], shapes[chord], at, bar, 0.09f);
                mix.Note(Voice.Bass, at, roots[chord] - 12, beat * 1.8, 0.32f, 0f);
                mix.Note(Voice.Bass, at + 2.5 * beat, roots[chord] - 5, beat * 1.2, 0.22f, 0f);

                // Electric piano comping on 2 and the "and" of 3.
                foreach (var offset in new[] { 1.0, 2.5 })
                    foreach (var interval in shapes[chord])
                        mix.Note(Voice.EPiano, at + offset * beat, roots[chord] + interval, beat * 0.7, 0.07f, 0.2f);

                // A quiet tick on the off-beats, for motion.
                for (var e = 0; e < 8; e++)
                    mix.Note(Voice.Tick, at + e * beat * 0.5, 0, 0.03, e % 2 == 1 ? 0.05f : 0.025f, 0.3f);

                // A chime phrase every other bar, from the chord's own tones, an octave up.
                if (b % 2 == 0)
                {
                    var count = 2 + random.Next(0, 3);
                    for (var n = 0; n < count; n++)
                    {
                        var interval = shapes[chord][random.Next(shapes[chord].Length)];
                        mix.Note(Voice.Bell, at + (n * 1.5 + 0.5) * beat, roots[chord] + interval + 12, beat * 1.4, 0.2f, n % 2 == 0 ? -0.35f : 0.35f);
                    }
                }
            }

            mix.Reverb(0.32f);
            mix.WrapLoop(loop);
            return mix;
        }

        /// <summary>Playing a station: slower and sparser, so it sits under the puzzle rather than beside it.</summary>
        static Mix Platform()
        {
            const int bars = 16;
            const double bpm = 76;
            var beat = 60.0 / bpm;
            var bar = 4 * beat;
            var loop = bars * bar;
            var mix = new Mix(loop + 6.0);
            var random = new System.Random(1717);

            // D major: Dmaj7, Bm7, Gmaj7, A6, F#m7, Bm7, Em7, Asus4.
            var roots = new[] { 50, 47, 43, 45, 42, 47, 40, 45 };
            var shapes = new[]
            {
                new[] { 12, 16, 19, 23 }, new[] { 12, 15, 19, 22 }, new[] { 12, 16, 19, 23 }, new[] { 12, 16, 19, 21 },
                new[] { 12, 15, 19, 22 }, new[] { 12, 15, 19, 22 }, new[] { 12, 15, 19, 22 }, new[] { 12, 17, 19, 24 },
            };

            for (var b = 0; b < bars; b++)
            {
                var at = b * bar;
                var chord = b % 8;
                Chord7(mix, roots[chord], shapes[chord], at, bar, 0.08f);
                mix.Note(Voice.Bass, at, roots[chord] - 12, bar * 0.9, 0.28f, 0f);

                // Vibes rising through the chord in eighths, softly.
                for (var e = 0; e < 8; e++)
                {
                    var interval = shapes[chord][e % 4] + (e >= 4 ? 12 : 0);
                    mix.Note(Voice.Vibes, at + e * beat * 0.5, roots[chord] + interval, beat * 0.9, e == 0 ? 0.11f : 0.07f, (e % 4 - 1.5f) * 0.25f);
                }

                // A music-box answer every fourth bar.
                if (b % 4 == 3)
                {
                    var top = roots[chord] + shapes[chord][3] + 12;
                    for (var n = 0; n < 3; n++)
                        mix.Note(Voice.MusicBox, at + (1 + n) * beat, top - n * (random.Next(0, 2) == 0 ? 2 : 3), beat * 0.9, 0.12f, 0.4f - n * 0.4f);
                }
            }

            mix.Reverb(0.38f);
            mix.WrapLoop(loop);
            return mix;
        }

        // ------------------------------------------------------------------ the synthesiser

        /// <summary>A stereo buffer that notes are added into.</summary>
        sealed class Mix
        {
            public float[] Left;
            public float[] Right;
            readonly System.Random m_Noise = new System.Random(99);

            public Mix(double seconds)
            {
                var samples = (int)(seconds * Rate);
                Left = new float[samples];
                Right = new float[samples];
            }

            /// <summary>One note: <paramref name="pan"/> from −1 (left) to 1 (right).</summary>
            public void Note(Voice voice, double at, int midi, double duration, float velocity, float pan)
            {
                var frequency = 440.0 * Math.Pow(2.0, (midi - 69) / 12.0);
                var ring = Ring(voice, duration);
                var start = (int)(at * Rate);
                var count = (int)(ring * Rate);
                var gainL = velocity * (float)Math.Sqrt(0.5 * (1.0 - pan));
                var gainR = velocity * (float)Math.Sqrt(0.5 * (1.0 + pan));

                for (var i = 0; i < count; i++)
                {
                    var index = start + i;
                    if (index >= Left.Length) break;
                    var t = i / (double)Rate;
                    var sample = (float)(Sample(voice, frequency, t, duration) * Release(t, duration, voice));
                    Left[index] += sample * gainL;
                    Right[index] += sample * gainR;
                }
            }

            /// <summary>How long a note sounds past its written length: bells ring, the bass and pad stop.</summary>
            static double Ring(Voice voice, double duration)
            {
                switch (voice)
                {
                    case Voice.Bell: return duration + 1.6;
                    case Voice.Vibes: return duration + 1.4;
                    case Voice.MusicBox: return duration + 1.0;
                    case Voice.EPiano: return duration + 0.8;
                    case Voice.Marimba: return duration + 0.6;
                    case Voice.Pad: return duration + 0.9;
                    case Voice.Bass: return duration + 0.15;
                    default: return duration + 0.02;
                }
            }

            /// <summary>After the written length, a fade over the ring time, so long tails do not smear the next note.</summary>
            static double Release(double t, double duration, Voice voice)
            {
                if (t <= duration) return 1.0;
                var tail = Ring(voice, duration) - duration;
                var x = (t - duration) / tail;
                return x >= 1.0 ? 0.0 : (1.0 - x) * (1.0 - x);
            }

            double Sample(Voice voice, double f, double t, double duration)
            {
                var w = 2.0 * Math.PI * f * t;
                var attack = Math.Min(1.0, t / 0.004);
                switch (voice)
                {
                    case Voice.Bell:
                        // Station chime: a clean fundamental, a strong octave and slightly stretched upper partials.
                        return attack * (Math.Sin(w) * Math.Exp(-t * 1.2)
                                         + 0.45 * Math.Sin(2.0 * w) * Math.Exp(-t * 2.0)
                                         + 0.22 * Math.Sin(3.0 * w) * Math.Exp(-t * 3.5)
                                         + 0.14 * Math.Sin(4.16 * w) * Math.Exp(-t * 5.0)
                                         + 0.08 * Math.Sin(5.43 * w) * Math.Exp(-t * 7.0));
                    case Voice.EPiano:
                    {
                        // Two-operator FM, its brightness falling away like a tine piano's.
                        var index = 1.8 * Math.Exp(-t * 5.0) + 0.25;
                        return attack * Math.Sin(w + index * Math.Sin(w)) * Math.Exp(-t * 1.1);
                    }
                    case Voice.Marimba:
                        return attack * (Math.Sin(w) * Math.Exp(-t * 2.6)
                                         + 0.35 * Math.Sin(4.0 * w) * Math.Exp(-t * 9.0)
                                         + 0.12 * Math.Sin(9.2 * w) * Math.Exp(-t * 18.0));
                    case Voice.MusicBox:
                        return attack * (Math.Sin(w) * Math.Exp(-t * 2.2)
                                         + 0.5 * Math.Sin(2.0 * w) * Math.Exp(-t * 3.0)
                                         + 0.2 * Math.Sin(3.0 * w) * Math.Exp(-t * 5.0));
                    case Voice.Vibes:
                    {
                        var tremolo = 1.0 - 0.22 * (0.5 + 0.5 * Math.Sin(2.0 * Math.PI * 5.5 * t));
                        return attack * tremolo * (Math.Sin(w) * Math.Exp(-t * 0.9) + 0.25 * Math.Sin(4.0 * w) * Math.Exp(-t * 6.0));
                    }
                    case Voice.Pad:
                    {
                        // Three slightly detuned voices and a little harmonic body, swelling in.
                        var swell = Math.Min(1.0, t / 0.45);
                        var body = Math.Sin(w) + Math.Sin(w * 1.003) + Math.Sin(w * 0.997);
                        body += 0.3 * Math.Sin(2.0 * w) + 0.12 * Math.Sin(3.0 * w * 1.002);
                        return swell * body / 3.2;
                    }
                    case Voice.Bass:
                    {
                        var pluck = Math.Min(1.0, t / 0.01);
                        return pluck * (Math.Sin(w) + 0.25 * Math.Sin(2.0 * w)) * Math.Exp(-t * 0.8);
                    }
                    case Voice.Tick:
                        return (m_Noise.NextDouble() * 2.0 - 1.0) * Math.Exp(-t * 120.0);
                    default:
                        return 0.0;
                }
            }

            /// <summary>
            /// A small Schroeder reverb: four combs and two all-passes per side, with different delays left and right
            /// for width. <paramref name="wet"/> is mixed over the dry signal.
            /// </summary>
            public void Reverb(float wet)
            {
                Left = Reverberate(Left, new[] { 1557, 1617, 1491, 1422 }, new[] { 225, 556 }, wet);
                Right = Reverberate(Right, new[] { 1580, 1640, 1514, 1445 }, new[] { 248, 579 }, wet);
            }

            static float[] Reverberate(float[] dry, int[] combs, int[] allpasses, float wet)
            {
                var length = dry.Length;
                var sum = new float[length];
                foreach (var delay in combs)
                {
                    var d = delay * 2;   // the classic delays are for 22 kHz
                    var buffer = new float[d];
                    var filter = 0f;
                    for (var i = 0; i < length; i++)
                    {
                        var output = buffer[i % d];
                        filter = output * 0.7f + filter * 0.3f;   // damping
                        buffer[i % d] = dry[i] * 0.015f + filter * 0.84f;
                        sum[i] += output;
                    }
                }

                foreach (var delay in allpasses)
                {
                    var d = delay * 2;
                    var buffer = new float[d];
                    for (var i = 0; i < length; i++)
                    {
                        var buffered = buffer[i % d];
                        var output = -sum[i] + buffered;
                        buffer[i % d] = sum[i] + buffered * 0.5f;
                        sum[i] = output;
                    }
                }

                var result = new float[length];
                for (var i = 0; i < length; i++) result[i] = dry[i] + sum[i] * wet * 3f;
                return result;
            }

            /// <summary>Folds everything past <paramref name="loopSeconds"/> back onto the start, and cuts there.</summary>
            public void WrapLoop(double loopSeconds)
            {
                var loop = (int)(loopSeconds * Rate);
                Left = Wrap(Left, loop);
                Right = Wrap(Right, loop);
            }

            static float[] Wrap(float[] samples, int loop)
            {
                var result = new float[loop];
                Array.Copy(samples, result, Math.Min(loop, samples.Length));
                for (var i = loop; i < samples.Length; i++) result[(i - loop) % loop] += samples[i];
                return result;
            }

            public void FadeOut(double seconds)
            {
                var count = (int)(seconds * Rate);
                for (var i = 0; i < count && i < Left.Length; i++)
                {
                    var g = i / (float)count;
                    Left[Left.Length - 1 - i] *= g;
                    Right[Right.Length - 1 - i] *= g;
                }
            }

            public void Normalise(float peak)
            {
                var max = 1e-6f;
                for (var i = 0; i < Left.Length; i++) max = Mathf.Max(max, Mathf.Abs(Left[i]), Mathf.Abs(Right[i]));
                var gain = peak / max;
                for (var i = 0; i < Left.Length; i++)
                {
                    Left[i] *= gain;
                    Right[i] *= gain;
                }
            }
        }

        // ------------------------------------------------------------------ output

        static void Write(string name, Mix mix, float peak)
        {
            mix.Normalise(peak);
            var path = Path.Combine(OutputFolder, name + ".wav");
            using (var stream = new FileStream(path, FileMode.Create))
            using (var writer = new BinaryWriter(stream))
            {
                var samples = mix.Left.Length;
                var dataBytes = samples * 2 * 2;
                writer.Write(new[] { 'R', 'I', 'F', 'F' });
                writer.Write(36 + dataBytes);
                writer.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)2);
                writer.Write(Rate);
                writer.Write(Rate * 4);
                writer.Write((short)4);
                writer.Write((short)16);
                writer.Write(new[] { 'd', 'a', 't', 'a' });
                writer.Write(dataBytes);
                for (var i = 0; i < samples; i++)
                {
                    writer.Write((short)Mathf.Clamp(Mathf.RoundToInt(mix.Left[i] * 32767f), -32768, 32767));
                    writer.Write((short)Mathf.Clamp(Mathf.RoundToInt(mix.Right[i] * 32767f), -32768, 32767));
                }
            }
        }

        /// <summary>Loops stream from disk; jingles are short and decompress on load so they start on the frame.</summary>
        static void ConfigureImporters()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { OutputFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is AudioImporter importer)) continue;
                var loop = Path.GetFileName(path).StartsWith("Bgm-", StringComparison.Ordinal);
                var settings = importer.defaultSampleSettings;
                settings.loadType = loop ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.7f;
                settings.preloadAudioData = !loop;
                importer.defaultSampleSettings = settings;
                importer.loadInBackground = loop;
                importer.forceToMono = false;
                importer.SaveAndReimport();
            }
        }
    }
}
