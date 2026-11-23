using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProCommVR.Emotions;
using ProCommVR.Hospital.AiTalk;
using ProCommVR.Hospital.Debrief.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProCommVR.Hospital.Debrief.Tests
{
    /// <summary>
    /// The data of the doctor-led debrief without Unity's Play mode: key moments mapped to the recorded turns by turn
    /// index, the critical error first, at most 8 turns kept (the biggest changes stay), the timeline, the points the doctor
    /// talks about, and the assets (every doctor line has a clip).
    /// Run: Unity -batchmode -runTests -testPlatform EditMode -testFilter ProCommVR.Hospital.Debrief.Tests
    /// </summary>
    public class DebriefDataTests
    {
        private static RecordedTurn Turn(int index, Emotion? emotion, float trust, float angerChange = 0f) => new()
        {
            Index = index,
            StudentText = $"sentence {index}",
            PatientText = $"reply {index}",
            Emotion = emotion.HasValue ? EmotionState.Zero.With(emotion.Value, 0.6f) : EmotionState.Zero,
            Trust = trust,
            AngerChange = angerChange,
        };

        private static AiDebrief Debrief(bool critical)
        {
            var d = new AiDebrief
            {
                end_reason = critical ? "critical_error" : "stopped",
                total_score = 6,
                max_score = 14,
                trust_curve = new[] { 20, 25, 30, 22, 40, 45 },
                anger_curve = new[] { 60, 55, 50, 62, 40, 38 },
                items = new[]
                {
                    new AiDebriefItem { id = 1, criterion = "Acknowledges his emotion", score = 2, evidence_quote = "I can see you're sad", evidence_turn = 4, feedback = "nl", feedback_en = "You named his sadness.", better_phrase = "b1" },
                    new AiDebriefItem { id = 2, criterion = "Stays within the nursing role", score = critical ? 0 : 2, evidence_turn = critical ? 3 : 0, feedback = "nl", feedback_en = "You promised.", better_phrase = "b2" },
                    new AiDebriefItem { id = 4, criterion = "Motivates", score = 0, feedback = "nl alleen", better_phrase = "b4" },
                    new AiDebriefItem { id = 7, criterion = "Asks", score = 1, evidence_turn = 2, feedback = "nl", feedback_en = "Closed question.", better_phrase = "b7" },
                },
                key_moments = new[]
                {
                    new AiKeyMoment { turn_index = 1, what_happened = "a", effect = "anger -5" },
                    new AiKeyMoment { turn_index = 3, what_happened = "b", effect = "anger +12" },
                    new AiKeyMoment { turn_index = 9, what_happened = "out of range", effect = "anger -2" },
                },
            };
            if (critical)
                d.critical = new AiCritical { label = "false_discharge_promise", turn = 3, quote = "you can go home", title = "Vals", title_en = "False promise of discharge", feedback = "nl", feedback_en = "Only the doctor decides." };
            return d;
        }

        private static List<RecordedTurn> FourTurns() => new()
        {
            Turn(1, Emotion.Sadness, 25), Turn(2, Emotion.Sadness, 30), Turn(3, Emotion.Anger, 22), Turn(4, null, 40),
        };

        [Test]
        public void KeyMomentsMapToRecordedTurnsByTurnIndex()
        {
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("No recorded turn 9"));
            var data = DebriefData.Build(Debrief(false), FourTurns(), Emotion.Sadness);
            Assert.AreEqual(3, data.Moments.Count);
            Assert.AreEqual("sentence 1", data.Moments[0].Turn.StudentText);
            Assert.AreEqual("sentence 3", data.Moments[1].Turn.StudentText);
            Assert.IsNull(data.Moments[2].Turn, "a key moment outside the recorded turns keeps its text without audio");
            Assert.AreEqual("out of range", data.Moments[2].WhatHappened);
        }

        [Test]
        public void CriticalMomentComesFirstAndIsNotDuplicated()
        {
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("No recorded turn 9"));
            var data = DebriefData.Build(Debrief(true), FourTurns(), Emotion.Anger);
            Assert.IsTrue(data.HasCritical);
            Assert.IsTrue(data.Moments[0].IsCritical);
            Assert.AreEqual(3, data.Moments[0].TurnIndex);
            Assert.AreEqual("anger +12", data.Moments[0].Effect, "the key moment of the same turn adds its effect to the critical moment");
            Assert.AreEqual(1, data.Moments.Count(m => m.TurnIndex == 3));
            Assert.AreEqual("False promise of discharge", data.Critical.Title, "English title when the server sends one");
            Assert.AreEqual(DebriefOutcome.Critical, data.Outcome(0.7f));
            Assert.AreSame(data.Moments[0], data.MostImportant());
        }

        [Test]
        public void ItemsUseEnglishFeedbackAndScoresStayTheSame()
        {
            LogAssert.ignoreFailingMessages = true;
            var source = Debrief(true);
            var data = DebriefData.Build(source, FourTurns(), Emotion.Anger);
            Assert.AreEqual(source.total_score, data.TotalScore);
            Assert.AreEqual(source.max_score, data.MaxScore);
            CollectionAssert.AreEqual(source.items.Select(i => i.score), data.Items.Select(i => i.Score));
            Assert.AreEqual("You named his sadness.", data.Items[0].Feedback);
            Assert.IsFalse(data.Items[2].FeedbackIsEnglish, "without feedback_en the Dutch text is kept (older server)");
            // lowest: item 2 (0, but that is the critical error itself) is skipped, so item 4; praise: item 1 (2, with a quote)
            Assert.AreEqual(4, data.Lowest().Id);
            Assert.AreEqual(1, data.Highest(data.Lowest()).Id);
        }

        [Test]
        public void TimelineHasOnePointPerRecordedTurn()
        {
            LogAssert.ignoreFailingMessages = true;
            var data = DebriefData.Build(Debrief(true), FourTurns(), Emotion.Anger);
            Assert.AreEqual(4, data.Timeline.Count);
            Assert.AreEqual(Emotion.Anger, data.Timeline[2].Dominant);
            Assert.IsNull(data.Timeline[3].Dominant, "no strong feeling: the grey sphere");
            Assert.IsTrue(data.Timeline[2].IsCritical);
            Assert.IsTrue(data.Timeline[0].IsKey);
            Assert.AreEqual(22f, data.Timeline[2].Trust);
            Assert.AreEqual(0.02f, EmotionTimeline.PostHeightFor(0f, new Vector2(0.02f, 0.25f)), 0.0001f);
            Assert.AreEqual(0.25f, EmotionTimeline.PostHeightFor(100f, new Vector2(0.02f, 0.25f)), 0.0001f);
        }

        [Test]
        public void WithoutRecordingsTheTimelineUsesTheTrustCurve()
        {
            var data = DebriefData.Build(Debrief(false), new List<RecordedTurn>(), null);
            Assert.AreEqual(5, data.Timeline.Count);
            Assert.AreEqual(1, data.Timeline[0].TurnIndex);
            Assert.AreEqual(45f, data.Timeline[4].Trust);
            Assert.IsFalse(data.HasFinalEmotion);
        }

        [Test]
        public void AtMostEightTurnsAndTheBiggestChangesStay()
        {
            var turns = new List<RecordedTurn>();
            for (var i = 1; i <= 12; i++)
                turns.Add(Turn(i, Emotion.Sadness, 30, i == 2 ? 30f : i == 3 ? -25f : i == 4 ? 20f : 1f));
            ConversationRecorder.Trim(turns, 8, 3);
            Assert.AreEqual(8, turns.Count);
            CollectionAssert.Contains(turns.Select(t => t.Index), 2);
            CollectionAssert.Contains(turns.Select(t => t.Index), 3);
            CollectionAssert.Contains(turns.Select(t => t.Index), 4);
            CollectionAssert.DoesNotContain(turns.Select(t => t.Index), 1, "the oldest unimportant turn is dropped first");
            CollectionAssert.Contains(turns.Select(t => t.Index), 12);
        }

        [Test]
        public void ClearEmptiesTheRecorder()
        {
            var go = new GameObject("Recorder test");
            try
            {
                var recorder = go.AddComponent<ConversationRecorder>();
                recorder.AddForTest(Turn(1, Emotion.Joy, 70));
                recorder.AddForTest(Turn(2, Emotion.Joy, 75));
                Assert.AreEqual(2, recorder.Turns.Count);
                recorder.Clear();
                Assert.AreEqual(0, recorder.Turns.Count);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void OutcomeGoodAndMixed()
        {
            var d = Debrief(false);
            d.total_score = 10;
            Assert.AreEqual(DebriefOutcome.Good, DebriefData.Build(d, null, Emotion.Joy).Outcome(0.7f));
            d.total_score = 9;
            Assert.AreEqual(DebriefOutcome.Mixed, DebriefData.Build(d, null, Emotion.Joy).Outcome(0.7f));
        }

        [Test]
        public void EveryDoctorLineHasAClip()
        {
            var script = AssetDatabase.LoadAssetAtPath<DebriefScript>(DebriefAssets.ScriptPath);
            Assert.IsNotNull(script, "run ProCommVR > Liam > Debrief > Import Doctor Lines");
            var missing = script.Lines.Concat(script.Voices.Entries).Where(l => l.Clip == null).Select(l => l.Key).ToList();
            CollectionAssert.IsEmpty(missing, "lines without a voice clip");
            foreach (var key in new[] { "greeting", "reflection_question", "timeline_intro", "memory_intro", "memory_look", "memory_better",
                         "pick_moment", "choice", "closing_good", "closing_mixed", "closing_critical", "retry", "done" })
                Assert.IsNotNull(script.Line(key), key);
            foreach (Emotion picked in System.Enum.GetValues(typeof(Emotion)))
            {
                Assert.IsNotNull(script.Reveal(picked, null), $"reveal {picked} / none");
                foreach (Emotion real in System.Enum.GetValues(typeof(Emotion)))
                    Assert.IsNotNull(script.Reveal(picked, real), $"reveal {picked} / {real}");
            }
            for (var item = 1; item <= 7; item++)
            {
                Assert.IsNotNull(script.NoteFor(item, true));
                Assert.LessOrEqual(script.NoteFor(item, false).Split(' ').Length, 8, $"note of item {item} has at most 8 words");
                Assert.IsNotNull(script.Voices.Find($"s1_item{item}_better"));
                for (var score = 0; score <= 2; score++)
                    Assert.IsNotNull(script.Voices.Find($"s1_item{item}_score{score}"), $"item {item} score {score}");
            }
            Assert.IsNotNull(script.Voices.Find("s1_critical_false_discharge_promise"));
            Assert.IsNotNull(script.PagerClip);
            Assert.IsNotNull(script.FootstepsClip);
        }

        [Test]
        public void VoiceLibraryDropsTheClipWhenTheServerTextChanged()
        {
            var library = ScriptableObject.CreateInstance<DebriefVoiceLibrary>();
            var clip = AudioClip.Create("x", 100, 1, 16000, false);
            library.Entries.Add(new DebriefLine("k", "You named  his sadness.", clip));
            Assert.AreSame(clip, library.For("k", "you named his sadness.").Clip);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("recorded for another text"));
            var changed = library.For("k", "Something else.");
            Assert.IsNull(changed.Clip);
            Assert.AreEqual("Something else.", changed.Text);
            Object.DestroyImmediate(clip);
            Object.DestroyImmediate(library);
        }
    }
}
