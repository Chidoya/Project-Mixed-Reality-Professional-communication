using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace ProCommVR.Hospital.AiTalk.Testing
{
    /// <summary>
    /// Automatische test (ProCommVR.Editor.AiTalkCapture): speelt wav-bestanden af als studentbeurten via
    /// dezelfde weg als PushToTalk na het loslaten (samples -> WavUtility -> AiNpc.SendStudentAudio), wacht tot
    /// het personage uitgesproken is, schrijft een log en maakt een screenshot terwijl het personage antwoordt.
    /// Vervangt alleen de microfoon en de knop.
    /// </summary>
    public class AiTalkTestRunner : MonoBehaviour
    {
        public List<string> WavFiles = new();
        public string ScreenshotPath;
        [Tooltip("Na welke beurt (1 = eerste) het screenshot genomen wordt; 0 = de laatste.")]
        public int ScreenshotTurn;
        public string LogPath;
        [Tooltip("Leeg = geen nabespreking. Anders: gesprek beëindigen (zoals de toets E), wachten op de nabespreking en een screenshot.")]
        public string DebriefShotPath;
        public float PauseBetweenTurns = 1f;
        public Action<bool> Finished;

        private readonly StringBuilder _log = new();

        private async void Start()
        {
            var ok = false;
            try
            {
                ok = await Run();
            }
            catch (Exception e)
            {
                Debug.LogError("[AiTalkTest] " + e);
            }
            if (!string.IsNullOrEmpty(LogPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                File.WriteAllText(LogPath, _log.ToString());
            }
            Finished?.Invoke(ok);
        }

        private async Awaitable<bool> Run()
        {
            var npc = FindAnyObjectByType<AiNpc>();
            if (npc == null)
            {
                Line("Geen AiNpc in de scene.");
                return false;
            }
            // Opening: wachten tot de sessie loopt en de openingszin uitgesproken is
            if (!await WaitUntil(() => npc.LastTurn != null && !npc.Busy, 30f))
            {
                Line("Geen openingszin binnen 30 s.");
                return false;
            }
            var opening = npc.LastTurn;
            Line($"OPENING  [{opening.emotion}/{opening.animation}] {opening.reply}  (anger {opening.anger}, trust {opening.trust}, band {opening.band}, {opening.roundTripMs} ms)");
            if (opening.localFallback)
                return false;
            await WaitUntil(() => !npc.IsSpeaking, 20f);

            var shotTurn = ScreenshotTurn > 0 ? ScreenshotTurn : WavFiles.Count;
            for (var i = 0; i < WavFiles.Count; i++)
            {
                await Awaitable.WaitForSecondsAsync(PauseBetweenTurns);
                var wav = ReadAsMicrophone(WavFiles[i]);
                var turn = await npc.SendStudentAudio(wav);
                if (turn == null)
                {
                    Line($"TURN {i + 1}: niet verstuurd (gesprek afgelopen of bezig).");
                    break;
                }
                var t = turn.timings_ms ?? new AiTimings();
                Line($"TURN {i + 1}  heard: \"{turn.transcript}\"");
                Line($"         labels: {(turn.labels is { Length: > 0 } ? string.Join(", ", turn.labels) : "-")}  quote: \"{turn.evidence_quote}\"");
                Line($"         anger {turn.before?.anger} -> {turn.anger}, trust {turn.before?.trust} -> {turn.trust}, band {turn.band}, phase {turn.phase}{(turn.HasEnded ? ", ENDED " + turn.ended : "")}");
                Line($"         SARAH [{turn.emotion}/{turn.animation}/{turn.pose}]: {turn.reply}{(turn.localFallback ? "  (lokaal reserveantwoord: " + (turn.retry ? "retry" : turn.error) + ")" : "")}");
                Line($"         ms: stt {t.stt_ms}, classify {t.classify_ms}, actor {t.actor_ms}, tts {t.tts_ms}, server {t.request_total_ms}, round trip {turn.roundTripMs}");

                if (i + 1 == shotTurn && !string.IsNullOrEmpty(ScreenshotPath))
                {
                    // Midden in het antwoord: mond open, gezicht en houding van dit antwoord
                    await WaitUntil(() => npc.IsSpeaking, 2f);
                    await Awaitable.WaitForSecondsAsync(0.6f);
                    Capture(ScreenshotPath, 1600, 900);
                    Line($"         screenshot: {ScreenshotPath}  (TalkLevel op dat moment zichtbaar in het gezicht)");
                }
                await WaitUntil(() => !npc.IsSpeaking, 30f);
                if (npc.HasEnded)
                {
                    Line($"GESPREK AFGELOPEN: {turn.ended}");
                    break;
                }
            }
            Line($"EINDE  anger {npc.Anger}, trust {npc.Trust}, band {npc.Band}, phase {npc.Phase}");
            if (string.IsNullOrEmpty(DebriefShotPath))
                return true;

            var panel = FindAnyObjectByType<AiDebriefPanel>();
            if (panel == null)
            {
                Line("Geen AiDebriefPanel in de scene.");
                return false;
            }
            if (!npc.HasEnded)
            {
                Line("Gesprek niet afgelopen: beëindigen zoals de testtoets E.");
                npc.EndNow("stopped");
            }
            var started = Time.realtimeSinceStartup;
            await WaitUntil(() => panel.State != AiDebriefPanel.PanelState.Hidden, 10f);
            if (Camera.main != null)
                Camera.main.fieldOfView = 72f; // testcamera: het hele paneel in beeld (de Quest ziet breder)
            await Awaitable.WaitForSecondsAsync(1f);
            Capture(DebriefShotPath.Replace(".png", "_wachten.png"), 1600, 900);
            if (!await WaitUntil(() => panel.State is AiDebriefPanel.PanelState.Ready or AiDebriefPanel.PanelState.Failed, 300f))
            {
                Line("Nabespreking niet klaar binnen 300 s.");
                return false;
            }
            var waited = Time.realtimeSinceStartup - started;
            await Awaitable.WaitForSecondsAsync(0.5f);
            Capture(DebriefShotPath, 2560, 1440);
            var d = panel.Result;
            if (d.Failed)
            {
                Line("NABESPREKING MISLUKT: " + d.error);
                return false;
            }
            Line($"NABESPREKING {d.total_score}/{d.max_score} ({d.end_reason}), gewacht {waited:0.0} s, server {d.timings_ms?.total_ms} ms, runs [{string.Join(", ", d.timings_ms?.runs_ms ?? Array.Empty<int>())}] ms");
            Line("   " + d.summary);
            foreach (var item in d.items)
                Line($"   {item.id,2}. {item.criterion_nl,-45} {item.score} [{string.Join(",", item.scores ?? Array.Empty<int>())}]{(item.uncertain ? " ONZEKER" : "")}  \"{item.evidence_quote}\"  | {item.feedback} | Beter: {item.better_phrase}");
            foreach (var m in d.key_moments)
                Line($"   moment beurt {m.turn_index}: {m.what_happened} ({m.effect})");
            Line($"   boosheid: {string.Join(" ", d.anger_curve)}");
            Line($"   screenshot: {DebriefShotPath}");
            return true;
        }

        /// <summary>Wav van schijf naar samples en terug, zoals PushToTalk met een microfoonopname doet.</summary>
        private static byte[] ReadAsMicrophone(string path)
        {
            var clip = WavUtility.ToAudioClip(File.ReadAllBytes(path), "test");
            var samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);
            var wav = WavUtility.ToWav16kMono(samples, clip.channels, clip.frequency);
            Destroy(clip);
            return wav;
        }

        private static async Awaitable<bool> WaitUntil(Func<bool> condition, float timeout)
        {
            var end = Time.realtimeSinceStartup + timeout;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > end)
                    return false;
                await Awaitable.NextFrameAsync();
            }
            return true;
        }

        private static void Capture(string path, int width, int height)
        {
            var cam = Camera.main;
            if (cam == null)
                return;
            var rt = new RenderTexture(width, height, 24) { antiAliasing = 8 };
            var previous = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            cam.targetTexture = previous;
            RenderTexture.active = null;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Destroy(tex);
            rt.Release();
            Destroy(rt);
        }

        private void Line(string text)
        {
            _log.AppendLine(text);
            Debug.Log("[AiTalkTest] " + text);
        }
    }
}
