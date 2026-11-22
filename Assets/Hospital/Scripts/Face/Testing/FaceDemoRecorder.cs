using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;

namespace ProCommVR.Hospital.Face.Testing
{
    /// <summary>
    /// Neemt in Play mode beelden op van de testscene liam_face_test (gestart door
    /// ProCommVR.Editor.FaceCapture.RunBatch): stilstaande beelden per uitdrukking en de frames voor
    /// twee video's (close-up van Visitor_Daughter en een raster met alle personages).
    /// Draait in LateUpdate na alle gezichtsscripts, met een vaste framerate van 30.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class FaceDemoRecorder : MonoBehaviour
    {
        public string OutputFolder;
        public string ScreenshotFolder;
        public Action<bool> Finished;

        private const int Fps = 30;
        private const int GridCols = 6, GridRows = 3, TileW = 320, TileH = 360;

        private static readonly string[] Dutch =
            { "Neutraal", "Blij", "Verdrietig", "Boos", "Bezorgd", "Verrast", "Pijn", "Huilend" };

        private sealed class Character
        {
            public string Name;
            public FaceController Face;
            public HeadLookAt Look;
            public Animator Animator;
            public Transform Head;
            public Transform Root;
            public Transform Viewer;
            public TextMeshPro Label;
            public float Scale;
        }

        private readonly List<Character> _chars = new();
        private IEnumerator _script;
        private Camera _cam;
        private RenderTexture _tileRt, _wideRt;
        private Texture2D _gridTex, _wideTex, _tileTex;

        private void Start()
        {
            Time.captureFramerate = Fps;
            _script = Run();
        }

        private void LateUpdate()
        {
            if (_script == null)
                return;
            bool more;
            try
            {
                more = _script.MoveNext();
            }
            catch (Exception e)
            {
                Debug.LogError("[FaceDemoRecorder] " + e);
                _script = null;
                Finished?.Invoke(false);
                return;
            }
            if (!more)
            {
                _script = null;
                Debug.Log("[FaceDemoRecorder] Klaar.");
                Finished?.Invoke(true);
            }
        }

        private IEnumerator Run()
        {
            Collect();
            var floor = GameObject.Find("Floor");
            if (floor != null)
                floor.SetActive(false);
            Directory.CreateDirectory(OutputFolder);
            _cam = new GameObject("RecorderCamera").AddComponent<Camera>();
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.80f, 0.83f, 0.86f);
            _cam.nearClipPlane = 0.02f;
            _cam.farClipPlane = 50f;
            _cam.enabled = false;
            _tileRt = new RenderTexture(TileW, TileH, 24) { antiAliasing = 8 };
            _wideRt = new RenderTexture(1280, 720, 24) { antiAliasing = 8 };
            _gridTex = new Texture2D(TileW * GridCols, TileH * GridRows, TextureFormat.RGB24, false);
            _wideTex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            _tileTex = new Texture2D(512, 512, TextureFormat.RGB24, false);

            for (var i = 0; i < 15; i++)
                yield return null;

            // --- 1. Stilstaande beelden per uitdrukking ---------------------------------
            foreach (var c in _chars)
                c.Face.AutoBlink = false;
            var daughter = _chars.Find(c => c.Name == "Visitor_Daughter") ?? _chars[0];
            var sheet = new Texture2D(512 * 4, 512 * 2, TextureFormat.RGB24, false);
            var tileRt512 = new RenderTexture(512, 512, 24) { antiAliasing = 8 };
            for (var e = 0; e < Dutch.Length; e++)
            {
                SetAll((FaceExpression)e, Dutch[e]);
                var wait = (FaceExpression)e == FaceExpression.Crying ? 4.0f : 1.5f;
                for (var f = 0; f < wait * Fps; f++)
                    yield return null;
                RenderGrid();
                Save(_gridTex, Path.Combine(OutputFolder, $"unity_alle_{e + 1}_{Dutch[e].ToLowerInvariant()}.png"));
                if (!string.IsNullOrEmpty(ScreenshotFolder) && (e == 0 || e == 3))
                {
                    Directory.CreateDirectory(ScreenshotFolder);
                    Save(_gridTex, Path.Combine(ScreenshotFolder, e == 0 ? "faces_neutral.png" : "faces_angry.png"));
                }
                AimTile(daughter, 0.62f, 26f);
                Render(tileRt512, _tileTex, 0, 0);
                sheet.SetPixels(512 * (e % 4), 512 * (1 - e / 4), 512, 512, _tileTex.GetPixels());
            }
            Save(sheet, Path.Combine(OutputFolder, "unity_dochter_8_uitdrukkingen.png"));
            foreach (var c in _chars)
                c.Face.AutoBlink = true;

            // --- 2. Video: close-up Visitor_Daughter ----------------------------------------
            var frames = Path.Combine(OutputFolder, "frames_dochter");
            ResetFolder(frames);
            var frame = 0;
            SetAll(FaceExpression.Neutral, "Neutraal");
            foreach (var c in _chars)
                c.Face.Snap();
            var order = new[] { 0, 1, 2, 3, 4, 5, 6, 7, 0 };
            foreach (var e in order)
            {
                daughter.Face.SetExpression((FaceExpression)e, 1f, 0.35f);
                daughter.Label.text = Dutch[e];
                var seconds = e == 7 ? 6f : 2.3f;
                for (var f = 0; f < seconds * Fps; f++)
                {
                    CaptureWide(daughter, frames, frame++);
                    yield return null;
                }
            }
            // Animator-state Angry: FaceFromAnimator zet zelf het boze gezicht
            daughter.Label.text = "Animator-state: Angry";
            daughter.Animator.CrossFadeInFixedTime("Angry", 0.3f);
            for (var f = 0; f < 4.5f * Fps; f++)
            {
                CaptureWide(daughter, frames, frame++);
                yield return null;
            }
            daughter.Label.text = "Animator-state: Idle";
            daughter.Animator.CrossFadeInFixedTime("Idle", 0.3f);
            for (var f = 0; f < 2.0f * Fps; f++)
            {
                CaptureWide(daughter, frames, frame++);
                yield return null;
            }
            // Hoofd volgt de speler
            daughter.Label.text = "Kijkt naar de speler";
            var basePos = daughter.Viewer.position;
            var right = Vector3.Cross(Vector3.up, Vector3.forward).normalized;
            for (var f = 0; f < 5f * Fps; f++)
            {
                var t = f / (5f * Fps);
                daughter.Viewer.position = basePos + right * (Mathf.Sin(t * Mathf.PI * 2f) * 1.0f) + Vector3.up * (Mathf.Sin(t * Mathf.PI * 4f) * 0.25f);
                CaptureWide(daughter, frames, frame++);
                yield return null;
            }
            daughter.Viewer.position = basePos;
            daughter.Label.text = "Kijkt weg (AvoidGaze)";
            daughter.Look.Mode = HeadLookAt.LookMode.AvoidGaze;
            daughter.Face.SetExpression(FaceExpression.Sad);
            for (var f = 0; f < 3f * Fps; f++)
            {
                CaptureWide(daughter, frames, frame++);
                yield return null;
            }
            daughter.Look.Mode = HeadLookAt.LookMode.LookAtPlayer;

            // --- 3. Video: alle personages ---------------------------------------------------
            frames = Path.Combine(OutputFolder, "frames_alle");
            ResetFolder(frames);
            frame = 0;
            SetAll(FaceExpression.Neutral, "Neutraal");
            foreach (var e in order)
            {
                SetAll((FaceExpression)e, Dutch[e]);
                var seconds = e == 7 ? 4.5f : 2.0f;
                for (var f = 0; f < seconds * Fps; f++)
                {
                    RenderGrid();
                    Save(_gridTex, Path.Combine(frames, $"f{frame++:D5}.png"));
                    yield return null;
                }
            }
        }

        // --- Hulpfuncties ------------------------------------------------------------------

        private void Collect()
        {
            foreach (var face in FindObjectsByType<FaceController>(FindObjectsSortMode.None))
            {
                var anim = face.GetComponentInChildren<Animator>();
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate; // opname-camera rendert handmatig
                var c = new Character
                {
                    Name = face.name,
                    Face = face,
                    Look = face.GetComponent<HeadLookAt>(),
                    Animator = anim,
                    Head = anim.GetBoneTransform(HumanBodyBones.Head),
                    Root = face.transform,
                };
                c.Scale = Mathf.Clamp((c.Head.position.y - c.Root.position.y) / 1.5f, 0.88f, 1.15f);
                c.Viewer = new GameObject($"Viewer_{c.Name}").transform;
                c.Viewer.position = HeadCenter(c) + Vector3.forward * 1.2f;
                if (c.Look != null)
                    c.Look.Target = c.Viewer;
                c.Label = CreateLabel(c);
                _chars.Add(c);
            }
            _chars.Sort((a, b) => a.Root.position.x.CompareTo(b.Root.position.x));
        }

        private static Vector3 HeadCenter(Character c) => c.Head.position + Vector3.up * (0.085f * c.Scale);

        private static TextMeshPro CreateLabel(Character c)
        {
            var go = new GameObject($"Label_{c.Name}");
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = "Neutraal";
            tmp.fontSize = 0.24f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.12f, 0.12f, 0.14f);
            tmp.rectTransform.sizeDelta = new Vector2(0.5f, 0.06f);
            PlaceLabel(go.transform, HeadCenter(c), c.Scale, false);
            go.transform.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
            return tmp;
        }

        private static void PlaceLabel(Transform label, Vector3 headCenter, float scale, bool tile)
        {
            label.position = headCenter + Vector3.forward * (tile ? 0.13f : 0.2f) + Vector3.down * ((tile ? 0.125f : 0.215f) * scale);
            label.localScale = Vector3.one * (tile ? 0.8f : 1f);
        }

        private void SetAll(FaceExpression e, string label)
        {
            foreach (var c in _chars)
            {
                c.Face.SetExpression(e, 1f, 0.35f);
                c.Label.text = label;
            }
        }

        private void AimTile(Character c, float distance, float fov)
        {
            var center = HeadCenter(c) + Vector3.down * (0.03f * c.Scale);
            _cam.fieldOfView = fov;
            _cam.transform.position = center + Vector3.forward * (distance * c.Scale) + Vector3.up * 0.02f;
            _cam.transform.LookAt(center);
        }

        private void RenderGrid()
        {
            for (var i = 0; i < _chars.Count && i < GridCols * GridRows; i++)
            {
                var c = _chars[i];
                c.Label.text = $"{c.Label.text.Split('\n')[0]}\n<size=60%>{c.Name}</size>";
                AimTile(c, 0.62f, 28f);
                PlaceLabel(c.Label.transform, HeadCenter(c), c.Scale, true);
                Render(_tileRt, _gridTex, TileW * (i % GridCols), TileH * (GridRows - 1 - i / GridCols));
                c.Label.text = c.Label.text.Split('\n')[0];
                PlaceLabel(c.Label.transform, HeadCenter(c), c.Scale, false);
            }
        }

        private void CaptureWide(Character c, string folder, int frame)
        {
            var center = HeadCenter(c) + Vector3.down * (0.08f * c.Scale);
            _cam.fieldOfView = 30f;
            _cam.transform.position = center + Vector3.forward * (0.85f * c.Scale) + Vector3.up * 0.03f;
            _cam.transform.LookAt(center);
            Render(_wideRt, _wideTex, 0, 0);
            Save(_wideTex, Path.Combine(folder, $"f{frame:D5}.png"));
        }

        private void Render(RenderTexture rt, Texture2D into, int x, int y)
        {
            var prev = RenderTexture.active;
            _cam.targetTexture = rt;
            _cam.aspect = (float)rt.width / rt.height;
            _cam.Render();
            RenderTexture.active = rt;
            into.ReadPixels(new Rect(0, 0, rt.width, rt.height), x, y);
            RenderTexture.active = prev;
            _cam.targetTexture = null;
        }

        private static void Save(Texture2D tex, string path) => File.WriteAllBytes(path, tex.EncodeToPNG());

        private static void ResetFolder(string folder)
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
            Directory.CreateDirectory(folder);
        }
    }
}
