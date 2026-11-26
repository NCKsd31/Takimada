using UnityEngine;

namespace Takimada
{
    /// <summary>
    /// Kısa ses efektleri. Ses dosyası gerektirmez: klipler açılışta koddan üretilir.
    /// Ana ses ve "Sesler: Kapalı" ayarı AudioListener üzerinden bunlara da uygulanır.
    /// </summary>
    public class Sfx
    {
        const int Rate = 44100;
        readonly AudioSource src;
        readonly AudioClip place;

        public Sfx(GameObject host)
        {
            src = host.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            src.spatialBlend = 0f;
            src.priority = 64;
            place = MakePlaceClip();
            notify = MakeNotifyClip();
            ui = host.AddComponent<AudioSource>(); // perdesi değişmeyen ayrı kaynak (taş sesinin perdesi her çalışta oynar)
            ui.playOnAwake = false;
            ui.spatialBlend = 0f;
            ui.priority = 64;
        }

        readonly AudioClip notify;
        readonly AudioSource ui;

        /// <summary>Gelen ticaret teklifi: yumuşak, iki notalı kısa çan.</summary>
        public void Notify(float volume = 0.55f)
        {
            ui.PlayOneShot(notify, Mathf.Clamp01(volume));
        }

        static AudioClip MakeNotifyClip()
        {
            int n = (int)(Rate * 0.5f);
            var data = new float[n];
            // (başlangıç s, frekans Hz): yükselen dörtlü aralık
            float[,] notes = { { 0f, 880f }, { 0.11f, 1174.66f } };
            float peak = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate, s = 0f;
                for (int k = 0; k < notes.GetLength(0); k++)
                {
                    float tt = t - notes[k, 0];
                    if (tt < 0) continue;
                    float f = notes[k, 1];
                    float env = Mathf.Clamp01(tt / 0.004f) * Mathf.Exp(-tt / 0.14f);
                    s += env * (Mathf.Sin(2f * Mathf.PI * f * tt) + 0.25f * Mathf.Sin(2f * Mathf.PI * 2f * f * tt) * Mathf.Exp(-tt / 0.05f));
                }
                s *= Mathf.Clamp01((n - i) / (Rate * 0.03f));
                data[i] = s;
                peak = Mathf.Max(peak, Mathf.Abs(s));
            }
            if (peak > 0f) for (int i = 0; i < n; i++) data[i] *= 0.8f / peak;
            var clip = AudioClip.Create("TradeNotify", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Satranç taşının tahtaya konuşu: tahtada kısa, kuru bir "tok".</summary>
        public void Place(float volume = 1f)
        {
            src.pitch = Random.Range(0.93f, 1.07f); // art arda konan taşlar birbirinin aynısı duyulmasın
            src.PlayOneShot(place, Mathf.Clamp01(volume));
        }

        /// <summary>
        /// Tahta taşın tahtaya vuruşu: birkaç sönümlü, harmonik olmayan tını (ahşap gövde),
        /// alçak bir gövde vuruşu ve en başta çok kısa bir sürtünme/çarpma gürültüsü.
        /// </summary>
        static AudioClip MakePlaceClip()
        {
            int n = (int)(Rate * 0.16f);
            var data = new float[n];
            // (frekans Hz, genlik, sönüm süresi s)
            float[,] modes =
            {
                { 920f, 0.55f, 0.030f },
                { 1480f, 0.35f, 0.022f },
                { 2610f, 0.22f, 0.012f },
                { 3870f, 0.12f, 0.007f },
            };
            var rng = new System.Random(1234);
            float lp = 0f, peak = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float s = 0f;
                for (int k = 0; k < modes.GetLength(0); k++)
                    s += modes[k, 1] * Mathf.Exp(-t / modes[k, 2]) * Mathf.Sin(2f * Mathf.PI * modes[k, 0] * t);
                // gövde: alçak, hızla düşen perdeli vuruş
                float f = 150f + 110f * Mathf.Exp(-t / 0.01f);
                s += 0.6f * Mathf.Exp(-t / 0.035f) * Mathf.Sin(2f * Mathf.PI * f * t);
                // çarpma anı: ilk ~3 ms'lik yumuşatılmış gürültü
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += 0.35f * (noise - lp);
                s += 0.5f * lp * Mathf.Exp(-t / 0.0025f);
                // tıkırtısız başlangıç (0.3 ms) ve sona doğru sıfıra iniş
                s *= Mathf.Clamp01(t / 0.0003f) * Mathf.Clamp01((n - i) / (Rate * 0.02f));
                data[i] = s;
                peak = Mathf.Max(peak, Mathf.Abs(s));
            }
            if (peak > 0f) for (int i = 0; i < n; i++) data[i] *= 0.9f / peak;
            var clip = AudioClip.Create("PiecePlace", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
