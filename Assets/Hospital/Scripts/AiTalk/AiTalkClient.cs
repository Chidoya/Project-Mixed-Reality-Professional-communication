using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

namespace ProCommVR.Hospital.AiTalk
{
    /// <summary>
    /// Praat met Tools/AiServer: /session/start, /turn/audio en /session/{id}/end.
    /// Gooit nooit een exception naar buiten: bij een fout komt er een lokaal antwoord terug
    /// (settings.fallbackReply) met localFallback = true en de fout in error.
    /// </summary>
    public class AiTalkClient
    {
        private const string LogPrefix = "[AiTalk] ";
        private readonly AiTalkSettings _settings;

        public string SessionId { get; private set; }
        public bool HasSession => !string.IsNullOrEmpty(SessionId);

        private readonly string _scenarioId;

        /// <summary>scenarioId leeg = settings.scenarioId.</summary>
        public AiTalkClient(AiTalkSettings settings, string scenarioId = null)
        {
            _settings = settings;
            _scenarioId = string.IsNullOrEmpty(scenarioId) ? settings.scenarioId : scenarioId;
        }

        private string Url(string path) => _settings.serverUrl.TrimEnd('/') + path;
        private int Timeout => Mathf.CeilToInt(_settings.requestTimeoutSeconds);

        /// <summary>Start een sessie. Geeft de openingszin terug, of een lokaal antwoord bij een fout.</summary>
        public async Awaitable<AiTurn> StartSession()
        {
#if UNITY_EDITOR
            if (_settings.autoStartServerInEditor)
                await WaitForServer(_settings.serverStartWaitSeconds);
#endif
            var body = JsonUtility.ToJson(new AiStartRequest { scenario_id = _scenarioId });
            using var request = new UnityWebRequest(Url("/session/start"), UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body)),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = Timeout,
            };
            request.SetRequestHeader("Content-Type", "application/json");
            var (ok, ms) = await Send(request);
            if (!ok)
                return Fallback(Describe(request));
            try
            {
                var response = JsonUtility.FromJson<AiStartResponse>(request.downloadHandler.text);
                if (response == null || string.IsNullOrEmpty(response.session_id) || response.opening == null)
                    return Fallback("onverwacht antwoord op /session/start");
                SessionId = response.session_id;
                IsEnded = false;
                response.opening.roundTripMs = ms;
                Debug.Log($"{LogPrefix}Sessie {SessionId} gestart ({_scenarioId}) in {ms} ms.");
                return response.opening;
            }
            catch (Exception e)
            {
                return Fallback($"JSON van /session/start onleesbaar: {e.Message}");
            }
        }

        /// <summary>Stuurt één opname (wav, 16 kHz mono) en geeft de beurt terug. Retry of fout: lokaal antwoord.</summary>
        public async Awaitable<AiTurn> SendAudio(byte[] wav)
        {
            if (!HasSession)
                return Fallback("geen sessie (server niet bereikbaar bij de start?)");
            var form = new List<IMultipartFormSection>
            {
                new MultipartFormDataSection("session_id", SessionId),
                new MultipartFormFileSection("audio", wav, "speech.wav", "audio/wav"),
            };
            using var request = UnityWebRequest.Post(Url("/turn/audio"), form);
            request.timeout = Timeout;
            var (ok, ms) = await Send(request);
            if (!ok)
                return Fallback(Describe(request));
            try
            {
                var turn = JsonUtility.FromJson<AiTurn>(request.downloadHandler.text);
                if (turn == null)
                    return Fallback("leeg antwoord op /turn/audio");
                turn.roundTripMs = ms;
                LogTiming(turn);
                if (turn.retry)
                {
                    var fallback = Fallback(null);
                    fallback.retry = true;
                    fallback.transcript = turn.transcript;
                    fallback.timings_ms = turn.timings_ms;
                    fallback.roundTripMs = ms;
                    Debug.Log($"{LogPrefix}Niet verstaan ('{turn.transcript}'), vraag opnieuw.");
                    return fallback;
                }
                return turn;
            }
            catch (Exception e)
            {
                return Fallback($"JSON van /turn/audio onleesbaar: {e.Message}");
            }
        }

        /// <summary>
        /// Sluit de sessie (reden bv. "time_up"); de server bewaart het transcript in Tools/AiServer/sessions.
        /// De sessie-id blijft bewaard, zodat RequestDebrief daarna nog werkt.
        /// </summary>
        public async Awaitable EndSession(string reason = null)
        {
            if (!HasSession || IsEnded)
                return;
            IsEnded = true;
            var id = SessionId;
            var body = JsonUtility.ToJson(new AiEndRequest { reason = reason });
            using var request = new UnityWebRequest(Url($"/session/{id}/end"), UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body)),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = 5,
            };
            request.SetRequestHeader("Content-Type", "application/json");
            var (ok, _) = await Send(request);
            if (ok)
                Debug.Log($"{LogPrefix}Sessie {id} afgesloten: {request.downloadHandler.text}");
            else
                Debug.LogWarning($"{LogPrefix}Sessie {id} afsluiten mislukt: {Describe(request)}");
        }

        public bool IsEnded { get; private set; }

        /// <summary>Beoordeling met de rubric (duurt 20 tot 40 s). Bij een fout: AiDebrief met error.</summary>
        public async Awaitable<AiDebrief> RequestDebrief()
        {
            if (!HasSession)
                return new AiDebrief { error = "geen sessie" };
            using var request = new UnityWebRequest(Url($"/session/{SessionId}/debrief"), UnityWebRequest.kHttpVerbPOST)
            {
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = Mathf.CeilToInt(_settings.debriefTimeoutSeconds),
            };
            var (ok, ms) = await Send(request);
            if (!ok)
            {
                var error = Describe(request);
                Debug.LogWarning(LogPrefix + "Nabespreking mislukt: " + error);
                return new AiDebrief { error = error };
            }
            try
            {
                var debrief = JsonUtility.FromJson<AiDebrief>(request.downloadHandler.text);
                debrief.roundTripMs = ms;
                Debug.Log($"{LogPrefix}Nabespreking: {debrief.total_score}/{debrief.max_score} in {ms} ms.");
                return debrief;
            }
            catch (Exception e)
            {
                return new AiDebrief { error = "JSON van /debrief onleesbaar: " + e.Message };
            }
        }

        /// <summary>
        /// Wacht tot /health antwoordt (de server luistert pas na het opwarmen). Gebruikt in de Editor als
        /// AiServerLauncher de server net gestart heeft. Geeft false na maxSeconds.
        /// </summary>
        public async Awaitable<bool> WaitForServer(float maxSeconds)
        {
            var watch = Stopwatch.StartNew();
            var logged = false;
            while (watch.Elapsed.TotalSeconds < maxSeconds)
            {
                using (var request = UnityWebRequest.Get(Url("/health")))
                {
                    request.timeout = 2;
                    var (ok, _) = await Send(request);
                    if (ok)
                    {
                        if (logged)
                            Debug.Log($"{LogPrefix}AI-server klaar na {watch.Elapsed.TotalSeconds:F1} s.");
                        return true;
                    }
                }
                if (!logged)
                {
                    Debug.Log($"{LogPrefix}Wachten op de AI-server ({_settings.serverUrl})...");
                    logged = true;
                }
                await Awaitable.WaitForSecondsAsync(0.5f);
            }
            Debug.LogWarning($"{LogPrefix}AI-server na {maxSeconds:F0} s nog niet klaar.");
            return false;
        }

        private static async Awaitable<(bool ok, int ms)> Send(UnityWebRequest request)
        {
            var watch = Stopwatch.StartNew();
            var op = request.SendWebRequest();
            while (!op.isDone)
                await Awaitable.NextFrameAsync();
            return (request.result == UnityWebRequest.Result.Success, (int)watch.ElapsedMilliseconds);
        }

        /// <summary>Eén regel per beurt in de Console; een waarschuwing als de beurt traag was (> 10 s).</summary>
        private static void LogTiming(AiTurn turn)
        {
            var t = turn.timings_ms;
            if (t == null)
                return;
            var line = $"{LogPrefix}Beurt: totaal {turn.roundTripMs} ms (server {t.request_total_ms}: " +
                       $"spraak->tekst {t.stt_ms}, classifier {t.classify_ms} ms/{t.classify_calls}x, " +
                       $"actor {t.actor_ms} ms/{t.actor_calls}x, tekst->spraak {t.tts_ms})";
            if (turn.roundTripMs > 10000)
                Debug.LogWarning(line + " -- TRAAG");
            else
                Debug.Log(line);
        }

        private string Describe(UnityWebRequest request)
        {
            if (request.result == UnityWebRequest.Result.ConnectionError && request.error == "Request timeout")
                return $"AI-server antwoordde niet binnen {Timeout} s (te traag, niet onbereikbaar). " +
                       "Kijk in het serverconsole naar 'slow turn' / 'NOT fully on the GPU', of verhoog requestTimeoutSeconds.";
            if (request.result == UnityWebRequest.Result.ConnectionError)
                return $"AI-server niet bereikbaar op {_settings.serverUrl} ({request.error}). Start Tools/AiServer/server.py.";
            var text = request.downloadHandler?.text;
            return $"HTTP {request.responseCode}: {request.error} {text}";
        }

        private AiTurn Fallback(string error)
        {
            if (!string.IsNullOrEmpty(error))
                Debug.LogWarning(LogPrefix + error);
            return new AiTurn
            {
                reply = _settings.fallbackReply,
                emotion = "",
                animation = "",
                pose = "Unchanged",
                labels = Array.Empty<string>(),
                localFallback = true,
                error = error,
            };
        }
    }
}
