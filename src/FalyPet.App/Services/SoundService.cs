using System;
using System.Collections.Generic;
using System.IO;
using System.Media;

namespace FalyPet.App.Services;

internal enum SoundEffect { Poke, Eat, Drink, Play, Wash, Sleep, Grow, Crack, Coin, Refuse }

/// <summary>
/// Ses efektleri. Dalga formu KODDA üretiliyor, dosyadan okunmuyor.
///
/// Sebebi ikisi birden: (1) uygulama hiçbir ses dosyasına bağımlı kalmıyor, kurulum
/// paketi büyümüyor; (2) kare dalga + zarf zaten 8-bit sesidir ve pixel-art bir
/// pet'e dosyadan gelen "gerçek" seslerden daha doğru oturur.
///
/// KURAL: ses yalnızca KULLANICININ başlattığı eylemlerde çalar. Kendiliğinden
/// çıkan ses (ihtiyaç bildirimi, gezinme) yok — 7/24 açık duran bir uygulamanın
/// habersiz ses çıkarması onu sildiren şeydir.
/// </summary>
internal sealed class SoundService : IDisposable
{
    private const int SampleRate = 22050;

    private readonly Dictionary<SoundEffect, byte[]> _cache = [];
    private readonly SoundPlayer _player = new();

    public bool Enabled { get; set; } = true;

    public void Play(SoundEffect effect)
    {
        if (!Enabled) return;

        try
        {
            if (!_cache.TryGetValue(effect, out var wav))
            {
                wav = BuildWav(ToneSequence(effect));
                _cache[effect] = wav;
            }

            _player.Stream = new MemoryStream(wav);
            _player.Play();
        }
        catch (Exception e) when (e is InvalidOperationException or IOException or TimeoutException)
        {
            // Ses aygıtı yoksa, meşgulse ya da uzak masaüstündeysek sessizce geç.
            // Bir pet uygulaması ses yüzünden asla durmamalı.
        }
    }

    /// <summary>Bir efektin notaları: (frekans Hz, süre ms, ses seviyesi 0-1).</summary>
    private static (double Freq, int Ms, double Gain)[] ToneSequence(SoundEffect effect) => effect switch
    {
        // Okşama: kısa, yukarı çıkan iki nota — "merhaba" tonlaması.
        SoundEffect.Poke => [(660, 45, 0.30), (880, 55, 0.26)],

        // Yeme: alçak ve tok iki ısırık.
        SoundEffect.Eat => [(300, 55, 0.28), (0, 25, 0), (260, 60, 0.26)],

        // İçme: aşağı inen üç kısa yudum.
        SoundEffect.Drink => [(520, 35, 0.22), (440, 35, 0.22), (360, 45, 0.22)],

        // Oyun: neşeli yukarı arpej.
        SoundEffect.Play => [(523, 50, 0.26), (659, 50, 0.26), (784, 70, 0.28)],

        // Yıkama: yumuşak, yukarı süpüren iki nota.
        SoundEffect.Wash => [(880, 60, 0.18), (1046, 80, 0.16)],

        // Uyku: aşağı inen, sönümlenen iki nota.
        SoundEffect.Sleep => [(392, 90, 0.22), (294, 140, 0.18)],

        // Büyüme: dört notalı kutlama. En uzun ve en belirgin ses — aşama
        // atlamak oyunun en büyük anı, kulakla da fark edilmeli.
        SoundEffect.Grow => [(523, 70, 0.30), (659, 70, 0.30), (784, 70, 0.30), (1046, 160, 0.32)],

        // Yumurta çatlaması: kısa ve sert.
        SoundEffect.Crack => [(180, 40, 0.34), (140, 55, 0.28)],

        // Coin: klasik iki notalı "para" sesi.
        SoundEffect.Coin => [(988, 40, 0.24), (1319, 90, 0.24)],

        // Reddetme: alçak, tek, kısa — "olmaz".
        SoundEffect.Refuse => [(220, 90, 0.22)],

        _ => [(440, 60, 0.2)],
    };

    private static byte[] BuildWav((double Freq, int Ms, double Gain)[] tones)
    {
        var samples = new List<short>();

        foreach (var (freq, ms, gain) in tones)
        {
            var count = SampleRate * ms / 1000;

            for (var i = 0; i < count; i++)
            {
                if (freq <= 0 || gain <= 0) { samples.Add(0); continue; }

                // Kare dalga: 8-bit karakterin kaynağı.
                var phase = i * freq / SampleRate;
                var square = (phase % 1.0) < 0.5 ? 1.0 : -1.0;

                // Zarf: hızlı açılış + sürekli sönüm. Bu olmadan her nota başında
                // ve sonunda duyulur bir "tık" oluyor.
                var t = i / (double)count;
                var attack = Math.Min(1.0, t / 0.06);
                var decay = Math.Pow(1.0 - t, 1.4);

                samples.Add((short)(square * gain * attack * decay * short.MaxValue));
            }
        }

        return Encode(samples);
    }

    /// <summary>16-bit mono PCM WAV başlığı + veri.</summary>
    private static byte[] Encode(List<short> samples)
    {
        using var stream = new MemoryStream();
        using var w = new BinaryWriter(stream);

        var dataBytes = samples.Count * 2;

        w.Write("RIFF"u8.ToArray());
        w.Write(36 + dataBytes);
        w.Write("WAVE"u8.ToArray());

        w.Write("fmt "u8.ToArray());
        w.Write(16);                        // alt parça boyutu
        w.Write((short)1);                  // PCM
        w.Write((short)1);                  // mono
        w.Write(SampleRate);
        w.Write(SampleRate * 2);            // bayt/saniye
        w.Write((short)2);                  // blok hizası
        w.Write((short)16);                 // bit derinliği

        w.Write("data"u8.ToArray());
        w.Write(dataBytes);
        foreach (var s in samples) w.Write(s);

        w.Flush();
        return stream.ToArray();
    }

    public void Dispose() => _player.Dispose();
}
