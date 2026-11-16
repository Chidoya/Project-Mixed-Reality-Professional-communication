using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using ProCommVR.Hospital.AiTalk;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace ProCommVR.Editor
{
    /// <summary>
    /// Start bij Play automatisch Ollama en Tools/AiServer/server.py als ze nog niet draaien
    /// (AiTalkSettings.autoStartServerInEditor). De server draait in een geminimaliseerd consolevenster,
    /// blijft draaien tussen Play-sessies (opwarmen gebeurt maar één keer) en stopt als Unity sluit.
    /// AiTalkClient.StartSession wacht op /health. Handmatig: ProCommVR > AI Talk > Start/Stop AI Server.
    /// </summary>
    [InitializeOnLoad]
    public static class AiServerLauncher
    {
        private const string LogPrefix = "[AiTalk] ";
        private const string PidKey = "ProCommVR.AiServerLauncher.Pid";
        private const int OllamaPort = 11434;

        static AiServerLauncher()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.quitting += StopServer;
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.ExitingEditMode)
                return;
            var settings = FindSettings();
            if (settings != null && settings.autoStartServerInEditor)
                StartServer(settings);
        }

        [MenuItem("ProCommVR/AI Talk/Start AI Server")]
        private static void StartServerMenu()
        {
            var settings = FindSettings();
            if (settings == null)
                Debug.LogWarning(LogPrefix + "Geen AiTalkSettings.asset gevonden; standaardpoort 8765.");
            StartServer(settings);
        }

        [MenuItem("ProCommVR/AI Talk/Stop AI Server")]
        private static void StopServerMenu() => StopServer();

        private static void StartServer(AiTalkSettings settings)
        {
            var uri = new Uri(settings != null ? settings.serverUrl : "http://127.0.0.1:8765");
            if (!uri.IsLoopback)
                return; // externe server: niet ons werk
            if (IsListening(uri.Port))
                return; // draait al (zelf gestart of door ons bij een vorige Play)

            var serverDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Tools", "AiServer"));
            var python = Path.Combine(serverDir, ".venv", "Scripts", "python.exe");
            if (!File.Exists(python))
            {
                Debug.LogError($"{LogPrefix}Kan de AI-server niet starten: {python} ontbreekt. Zie Tools/AiServer/README.md (Install).");
                return;
            }

            EnsureOllama();

            try
            {
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = python,
                    Arguments = "server.py",
                    WorkingDirectory = serverDir,
                    UseShellExecute = true, // eigen consolevenster met de serverlog
                    WindowStyle = ProcessWindowStyle.Minimized,
                });
                if (process != null)
                {
                    SessionState.SetInt(PidKey, process.Id);
                    Debug.Log($"{LogPrefix}AI-server gestart (pid {process.Id}), opwarmen duurt enkele seconden. " +
                              "Het consolevenster staat geminimaliseerd in de taakbalk.");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"{LogPrefix}AI-server starten mislukt: {e.Message}");
            }
        }

        /// <summary>Stopt alleen de server die deze Editor zelf startte; Ollama blijft draaien.</summary>
        private static void StopServer()
        {
            var pid = SessionState.GetInt(PidKey, 0);
            if (pid == 0)
                return;
            SessionState.EraseInt(PidKey);
            try
            {
                using var process = Process.GetProcessById(pid);
                if (!process.HasExited && process.ProcessName.StartsWith("python", StringComparison.OrdinalIgnoreCase))
                {
                    process.Kill();
                    Debug.Log($"{LogPrefix}AI-server gestopt (pid {pid}).");
                }
            }
            catch (ArgumentException)
            {
                // al gestopt (venster gesloten)
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{LogPrefix}AI-server stoppen mislukt: {e.Message}");
            }
        }

        private static void EnsureOllama()
        {
            if (IsListening(OllamaPort))
                return;
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Ollama");
            var app = Path.Combine(dir, "ollama app.exe");
            try
            {
                if (File.Exists(app))
                    Process.Start(new ProcessStartInfo { FileName = app, UseShellExecute = true }); // tray-app, start zelf "ollama serve"
                else
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "ollama", // via PATH
                        Arguments = "serve",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    });
                // Kort wachten, anders mislukt het opwarmen van de server (de eerste beurt laadt het model dan alsnog)
                var watch = Stopwatch.StartNew();
                while (!IsListening(OllamaPort) && watch.Elapsed.TotalSeconds < 15)
                    System.Threading.Thread.Sleep(250);
                Debug.Log($"{LogPrefix}Ollama gestart ({watch.Elapsed.TotalSeconds:F1} s).");
            }
            catch (Exception e)
            {
                Debug.LogError($"{LogPrefix}Ollama starten mislukt ({e.Message}). Start de Ollama-app zelf; de server wacht er niet op.");
            }
        }

        private static bool IsListening(int port)
        {
            try
            {
                using var client = new TcpClient();
                return client.ConnectAsync("127.0.0.1", port).Wait(300) && client.Connected;
            }
            catch
            {
                return false;
            }
        }

        private static AiTalkSettings FindSettings()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:AiTalkSettings"))
            {
                var settings = AssetDatabase.LoadAssetAtPath<AiTalkSettings>(AssetDatabase.GUIDToAssetPath(guid));
                if (settings != null)
                    return settings;
            }
            return null;
        }
    }
}
