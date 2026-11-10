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

        public AiTalkClient(AiTalkSettings settings)
        {
            _settings = settings;
        }

        private string Url(string path) => _settings.serverUrl.TrimEnd('/') + path;
        private int Timeout => Mathf.CeilToInt(_settings.requestTimeoutSeconds);

        /// <summary>Start een sessie. Geeft de openingszin terug, of een lokaal antwoord bij een fout.</summary>
        public async Awaitable<AiTurn> StartSession()
        {
            var body = JsonUtility.ToJson(new AiStartRequest { scenario_id = _settings.scenarioId });
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
                response.opening.roundTripMs = ms;
                Debug.Log($"{LogPrefix}Sessie {SessionId} gestart ({_settings.scenarioId}) in {ms} ms.");
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

        /// <summary>Sluit de sessie; de server bewaart het transcript in Tools/AiServer/sessions.</summary>
        public async Awaitable EndSession()
        {
            if (!HasSession)
                return;
            var id = SessionId;
            SessionId = null;
            using var request = new UnityWebRequest(Url($"/session/{id}/end"), UnityWebRequest.kHttpVerbPOST)
            {
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = 5,
            };
            var (ok, _) = await Send(request);
            if (ok)
                Debug.Log($"{LogPrefix}Sessie {id} afgesloten: {request.downloadHandler.text}");
            else
                Debug.LogWarning($"{LogPrefix}Sessie {id} afsluiten mislukt: {Describe(request)}");
        }

        private static async Awaitable<(bool ok, int ms)> Send(UnityWebRequest request)
        {
            var watch = Stopwatch.StartNew();
            var op = request.SendWebRequest();
            while (!op.isDone)
                await Awaitable.NextFrameAsync();
            return (request.result == UnityWebRequest.Result.Success, (int)watch.ElapsedMilliseconds);
        }

        private string Describe(UnityWebRequest request)
        {
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
