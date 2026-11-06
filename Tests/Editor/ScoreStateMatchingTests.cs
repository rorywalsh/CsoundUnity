/*
Copyright (c) 2015 Rory Walsh and the CsoundUnity contributors

This file is part of CsoundUnity: https://github.com/rorywalsh/CsoundUnity

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"),
to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense,
and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR
ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH
THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

#if USE_TIMELINES

using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Csound.Unity.Timelines;

using ScoreBehaviour = Csound.Unity.Timelines.CsoundUnityScorePlayableBehaviour;
using ScoreMode = Csound.Unity.Timelines.CsoundUnityScorePlayableBehaviour.ScoreMode;

namespace Csound.Unity.Tests
{
    /// <summary>
    /// Which saved state a <see cref="CsoundUnityScoreState"/> writes onto which clip.
    /// <para>
    /// A snapshot applied to the wrong clip is heard as one sequencer playing another's pattern, and
    /// looks like nothing at all in the inspector, so the cases that decide it are pinned here: clips
    /// renamed, reordered, duplicated, added and deleted. The tempo stands in for the state, because
    /// it goes through the same JSON as everything else and reads back as one number.
    /// </para>
    /// </summary>
    public class ScoreStateMatchingTests
    {
        private CsoundUnityScoreState _snapshot;

        [SetUp]
        public void SetUp()
        {
            _snapshot = ScriptableObject.CreateInstance<CsoundUnityScoreState>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_snapshot != null) Object.DestroyImmediate(_snapshot);
        }

        private static ScoreBehaviour Clip(string name, float bpm, string id = null, int assetId = 0,
                                           ScoreMode mode = ScoreMode.Step)
        {
            var b = new ScoreBehaviour
            {
                clipName    = name,
                clipId      = id,
                clipAssetId = assetId,
                bpm         = bpm
            };
            b.scoreInfo.mode = mode;
            return b;
        }

        private void Save(params ScoreBehaviour[] clips)
        {
            _snapshot.CaptureFrom(new List<ScoreBehaviour>(clips), null);
        }

        private static List<ScoreBehaviour> Live(params ScoreBehaviour[] clips)
            => new List<ScoreBehaviour>(clips);

        [Test]
        public void An_id_survives_a_reorder_and_a_rename()
        {
            Save(Clip("Drum", 100f, id: "aaa"), Clip("Bass", 110f, id: "bbb"));

            var bass = Clip("Bass renamed", 1f, id: "bbb");
            var drum = Clip("Drum", 2f, id: "aaa");

            Assert.AreEqual(2, _snapshot.ApplyTo(Live(bass, drum)));
            Assert.AreEqual(110f, bass.bpm, 0.001f);
            Assert.AreEqual(100f, drum.bpm, 0.001f);
        }

        [Test]
        public void An_asset_id_survives_a_graph_rebuild()
        {
            Save(Clip("Drum", 100f, assetId: 11), Clip("Drum", 110f, assetId: 22));

            var second = Clip("Drum", 1f, assetId: 22);
            var first  = Clip("Drum", 2f, assetId: 11);

            Assert.AreEqual(2, _snapshot.ApplyTo(Live(second, first)));
            Assert.AreEqual(110f, second.bpm, 0.001f);
            Assert.AreEqual(100f, first.bpm, 0.001f);
        }

        // The pass for ids runs to the end before any name is looked at, so a clip added since the
        // snapshot cannot take the state of the clip that owns it.
        [Test]
        public void A_clip_with_an_id_is_served_before_a_new_clip_of_the_same_name()
        {
            Save(Clip("Step Loop", 100f, id: "aaa"));

            var fresh = Clip("Step Loop", 1f);
            var owner = Clip("Step Loop", 2f, id: "aaa");

            Assert.AreEqual(1, _snapshot.ApplyTo(Live(fresh, owner)));
            Assert.AreEqual(100f, owner.bpm, 0.001f, "the clip that owns the state did not get it");
            Assert.AreEqual(1f, fresh.bpm, 0.001f, "a clip with no id took a state that was not its own");
        }

        [Test]
        public void An_id_that_matches_nothing_is_not_guessed_from_the_name()
        {
            Save(Clip("Drum", 100f, id: "aaa"));

            var other = Clip("Drum", 1f, id: "zzz");

            Assert.AreEqual(0, _snapshot.ApplyTo(Live(other)));
            Assert.AreEqual(1f, other.bpm, 0.001f);
        }

        // Duplicating a clip copies the serialized field, id and all, so an id two clips carry is not an
        // identity and is not matched on.
        [Test]
        public void A_duplicated_id_writes_nothing()
        {
            Save(Clip("Drum", 100f, id: "aaa"), Clip("Drum", 110f, id: "bbb"));

            var one = Clip("Drum", 1f, id: "aaa");
            var two = Clip("Drum", 2f, id: "aaa");

            Assert.AreEqual(0, _snapshot.ApplyTo(Live(one, two)));
            Assert.AreEqual(1f, one.bpm, 0.001f);
            Assert.AreEqual(2f, two.bpm, 0.001f);
        }

        [Test]
        public void A_name_of_its_own_is_matched_when_no_side_has_ids()
        {
            Save(Clip("Drum", 100f), Clip("Bass", 110f));

            var bass = Clip("Bass", 1f);
            var drum = Clip("Drum", 2f);

            Assert.AreEqual(2, _snapshot.ApplyTo(Live(bass, drum)));
            Assert.AreEqual(110f, bass.bpm, 0.001f);
            Assert.AreEqual(100f, drum.bpm, 0.001f);
        }

        // Two clips of the same name are what the sample timelines are full of. A name does not say which
        // is which, and neither does the order they sit in: swapping them leaves the same list of names.
        [Test]
        public void Two_clips_of_one_name_are_left_alone()
        {
            Save(Clip("Step Loop", 100f), Clip("Step Loop", 110f));

            var one = Clip("Step Loop", 1f);
            var two = Clip("Step Loop", 2f);

            Assert.AreEqual(0, _snapshot.ApplyTo(Live(one, two)));
            Assert.AreEqual(1f, one.bpm, 0.001f);
            Assert.AreEqual(2f, two.bpm, 0.001f);
        }

        [Test]
        public void Two_clips_of_one_name_are_written_in_order_only_when_asked()
        {
            Save(Clip("Step Loop", 100f), Clip("Step Loop", 110f));

            var one = Clip("Step Loop", 1f);
            var two = Clip("Step Loop", 2f);

            Assert.AreEqual(2, _snapshot.ApplyTo(Live(one, two), true, true));
            Assert.AreEqual(100f, one.bpm, 0.001f);
            Assert.AreEqual(110f, two.bpm, 0.001f);
        }

        // Even asked for, position is refused when the set no longer lines up: the names at those
        // positions are not the names saved there, so something was reordered or replaced.
        [Test]
        public void Position_is_refused_when_the_names_no_longer_line_up()
        {
            Save(Clip("Drum", 100f), Clip("Step Loop", 110f), Clip("Step Loop", 120f));

            var a = Clip("Step Loop", 1f);
            var b = Clip("Drum", 2f);
            var c = Clip("Step Loop", 3f);

            _snapshot.ApplyTo(Live(a, b, c), true, true);
            Assert.AreEqual(1f, a.bpm, 0.001f);
            Assert.AreEqual(3f, c.bpm, 0.001f);
            Assert.AreEqual(100f, b.bpm, 0.001f, "the clip with a name of its own should still be matched");
        }

        [Test]
        public void Position_is_refused_when_the_counts_disagree()
        {
            Save(Clip("Step Loop", 100f), Clip("Step Loop", 110f));

            var one   = Clip("Step Loop", 1f);
            var two   = Clip("Step Loop", 2f);
            var three = Clip("Step Loop", 3f);

            Assert.AreEqual(0, _snapshot.ApplyTo(Live(one, two, three), true, true));
            Assert.AreEqual(1f, one.bpm, 0.001f);
            Assert.AreEqual(2f, two.bpm, 0.001f);
            Assert.AreEqual(3f, three.bpm, 0.001f);
        }

        // The fields that carry a pattern mean different things in each mode, so a state is never applied
        // to a clip running another one.
        [Test]
        public void A_state_of_another_mode_is_refused()
        {
            Save(Clip("Drum", 100f, id: "aaa", mode: ScoreMode.Pattern));

            var step = Clip("Drum", 1f, id: "aaa", mode: ScoreMode.Step);

            Assert.AreEqual(0, _snapshot.ApplyTo(Live(step)));
            Assert.AreEqual(1f, step.bpm, 0.001f);
        }

        // The outer mode field is what ApplyTo checks; the payload carries its own, which is what a
        // hand-edited or pasted snapshot can disagree on.
        [Test]
        public void A_payload_that_says_another_mode_is_refused()
        {
            Save(Clip("Drum", 100f, id: "aaa", mode: ScoreMode.Pattern));
            _snapshot.clips[0].mode = ScoreMode.Step;

            var step = Clip("Drum", 1f, id: "aaa", mode: ScoreMode.Step);

            Assert.AreEqual(0, _snapshot.ApplyTo(Live(step)));
            Assert.AreEqual(1f, step.bpm, 0.001f);
        }

        [Test]
        public void A_malformed_payload_writes_nothing()
        {
            Save(Clip("Drum", 100f, id: "aaa"));
            _snapshot.clips[0].behaviourJson = "{\"bpm\": 100, ";

            var clip = Clip("Drum", 1f, id: "aaa");

            Assert.AreEqual(0, _snapshot.ApplyTo(Live(clip)));
            Assert.AreEqual(1f, clip.bpm, 0.001f);
        }

        [Test]
        public void An_empty_payload_writes_nothing()
        {
            Save(Clip("Drum", 100f, id: "aaa"));
            _snapshot.clips[0].behaviourJson = string.Empty;

            var clip = Clip("Drum", 1f, id: "aaa");

            Assert.AreEqual(0, _snapshot.ApplyTo(Live(clip)));
            Assert.AreEqual(1f, clip.bpm, 0.001f);
        }

        [Test]
        public void A_newer_schema_writes_nothing()
        {
            Save(Clip("Drum", 100f, id: "aaa"));
            _snapshot.schemaVersion = CsoundUnityScoreState.CurrentSchemaVersion + 1;

            // A schema nobody can read is an error and not a warning, and the runner wants it declared.
            LogAssert.Expect(LogType.Error, new Regex("was written with schema"));

            var clip = Clip("Drum", 1f, id: "aaa");

            Assert.AreEqual(0, _snapshot.ApplyTo(Live(clip)));
            Assert.AreEqual(1f, clip.bpm, 0.001f);
        }

        [Test]
        public void A_deleted_clip_costs_the_others_nothing()
        {
            Save(Clip("Drum", 100f, id: "aaa"), Clip("Bass", 110f, id: "bbb"));

            var bass = Clip("Bass", 1f, id: "bbb");

            Assert.AreEqual(1, _snapshot.ApplyTo(Live(bass)));
            Assert.AreEqual(110f, bass.bpm, 0.001f);
        }

        // One clip, chosen by having it in hand. A snapshot saved from a single clip is a preset: it
        // carries no identity, and writing it onto another clip is the point, not an accident.
        [Test]
        public void One_state_is_written_onto_a_clip_it_never_came_from()
        {
            Save(Clip("Drum", 100f, id: "aaa", assetId: 11));
            _snapshot.ForgetIdentity();

            var other = Clip("Something else", 1f, id: "zzz", assetId: 22);

            Assert.IsTrue(_snapshot.ApplyToOne(other));
            Assert.AreEqual(100f, other.bpm, 0.001f);
        }

        [Test]
        public void One_state_of_another_mode_is_refused()
        {
            Save(Clip("Drum", 100f, mode: ScoreMode.Pattern));

            var step = Clip("Drum", 1f, mode: ScoreMode.Step);

            Assert.IsFalse(_snapshot.ApplyToOne(step));
            Assert.AreEqual(1f, step.bpm, 0.001f);
        }

        [Test]
        public void One_state_of_another_mode_is_written_when_asked()
        {
            Save(Clip("Drum", 100f, mode: ScoreMode.Pattern));

            var step = Clip("Drum", 1f, mode: ScoreMode.Step);

            Assert.IsTrue(_snapshot.ApplyToOne(step, 0, true, allowOtherMode: true));
            Assert.AreEqual(100f, step.bpm, 0.001f);
            Assert.AreEqual(ScoreMode.Pattern, step.scoreInfo.mode);
        }

        [Test]
        public void A_merge_keeps_what_was_already_held()
        {
            Save(Clip("Drum", 100f, id: "aaa"));

            _snapshot.MergeFrom(Live(Clip("Bass", 110f, id: "bbb")), null);

            Assert.AreEqual(2, _snapshot.clips.Count);

            var drum = Clip("Drum", 1f, id: "aaa");
            var bass = Clip("Bass", 2f, id: "bbb");

            Assert.AreEqual(2, _snapshot.ApplyTo(Live(drum, bass)));
            Assert.AreEqual(100f, drum.bpm, 0.001f);
            Assert.AreEqual(110f, bass.bpm, 0.001f);
        }

        [Test]
        public void A_merge_replaces_the_clip_it_already_holds()
        {
            Save(Clip("Drum", 100f, id: "aaa"));

            _snapshot.MergeFrom(Live(Clip("Drum", 130f, id: "aaa")), null);

            Assert.AreEqual(1, _snapshot.clips.Count);

            var drum = Clip("Drum", 1f, id: "aaa");

            Assert.AreEqual(1, _snapshot.ApplyTo(Live(drum)));
            Assert.AreEqual(130f, drum.bpm, 0.001f);
        }

        [Test]
        public void A_state_that_is_not_there_is_refused()
        {
            Save(Clip("Drum", 100f));

            var clip = Clip("Drum", 1f);

            LogAssert.Expect(LogType.Error, new Regex("state 5 was asked for"));
            Assert.IsFalse(_snapshot.ApplyToOne(clip, 5));
            Assert.AreEqual(1f, clip.bpm, 0.001f);
        }

        [Test]
        public void One_state_from_a_newer_schema_is_refused()
        {
            Save(Clip("Drum", 100f));
            _snapshot.schemaVersion = CsoundUnityScoreState.CurrentSchemaVersion + 1;

            var clip = Clip("Drum", 1f);

            LogAssert.Expect(LogType.Error, new Regex("was written with schema"));
            Assert.IsFalse(_snapshot.ApplyToOne(clip));
            Assert.AreEqual(1f, clip.bpm, 0.001f);
        }

        // Which of a timeline's saved states could go onto the clip you are looking at.
        [Test]
        public void The_states_of_one_mode_are_listed()
        {
            Save(Clip("A", 100f, mode: ScoreMode.Step),
                 Clip("B", 110f, mode: ScoreMode.Pattern),
                 Clip("C", 120f, mode: ScoreMode.Step));

            Assert.AreEqual(new[] { 0, 2 }, _snapshot.StatesForMode(ScoreMode.Step));
            Assert.AreEqual(new[] { 1 },    _snapshot.StatesForMode(ScoreMode.Pattern));

            var pattern = Clip("B", 1f, mode: ScoreMode.Pattern);
            Assert.IsTrue(_snapshot.ApplyToOne(pattern, _snapshot.StatesForMode(ScoreMode.Pattern)[0]));
            Assert.AreEqual(110f, pattern.bpm, 0.001f);
        }

        [Test]
        public void Forgetting_the_identity_keeps_what_the_clip_plays()
        {
            var a = ScriptableObject.CreateInstance<UnityEngine.Timeline.TimelineAsset>();
            a.name = "A";

            try
            {
                _snapshot.CaptureFrom(new List<ScoreBehaviour>
                    { Clip("Drum", 97.5f, id: "aaa", assetId: 11) }, a);

                _snapshot.ForgetIdentity();

                Assert.IsNull(_snapshot.timeline);
                Assert.IsEmpty(_snapshot.timelineAssetName);
                Assert.IsEmpty(_snapshot.clips[0].clipId);
                Assert.AreEqual(0, _snapshot.clips[0].clipAssetId);
                Assert.IsFalse(_snapshot.HasUsableClipIds);

                var clip = Clip("Anything", 1f);
                Assert.IsTrue(_snapshot.ApplyToOne(clip));
                Assert.AreEqual(97.5f, clip.bpm, 0.0001f, "the state itself must survive");
            }
            finally
            {
                Object.DestroyImmediate(a);
            }
        }

        // How a change made in a runtime panel is told apart from a Timeline curve moving a value. The
        // whole state cannot be the test: Timeline animates the behaviour's public fields, so a clip with
        // any curve would look changed at every frame and the autosave would fire for nothing.
        [Test]
        public void An_animated_field_is_not_a_change()
        {
            var a = Clip("Drum", 100f);
            var b = Clip("Drum", 140f);   // as if a curve had moved the tempo

            Assert.AreEqual(CsoundUnityScoreState.ChangeKey(a), CsoundUnityScoreState.ChangeKey(b));
            Assert.IsFalse(a.stateSetAtRuntime, "nothing called the setter");
        }

        [Test]
        public void A_tempo_someone_set_is_a_change()
        {
            var clip = Clip("Drum", 100f);
            clip.SetBpm(140f);

            Assert.IsTrue(clip.stateSetAtRuntime, "a panel goes through the setter, a curve does not");
            Assert.AreEqual(140f, clip.bpm, 0.001f);
        }

        // Every setter raises the flag, not just the tempo one: each of them writes a field Timeline can
        // also animate, so the comparison key cannot see any of them.
        [Test]
        public void Any_setter_is_a_change()
        {
            var clip = Clip("Drum", 100f);
            clip.SetPitchBase(220f);

            Assert.IsTrue(clip.stateSetAtRuntime);
            Assert.AreEqual(220f, clip.pitchBase, 0.001f);
        }

        // Applying a state writes the animated fields straight from the JSON, past every setter. Without
        // this the state someone just applied would not be held when the graph is rebuilt, and the clips
        // would come back with the values the timeline asset carries.
        [Test]
        public void An_applied_state_is_a_change()
        {
            Save(Clip("Drum", 97f, id: "aaa"));

            var target = Clip("Drum", 120f, id: "aaa");
            Assert.AreEqual(1, _snapshot.ApplyTo(Live(target)));

            Assert.AreEqual(97f, target.bpm, 0.001f);
            Assert.IsTrue(target.stateSetAtRuntime, "the applied state must survive a rebuild");
        }

        [Test]
        public void A_step_turned_on_is_a_change()
        {
            var clip = Clip("Drum", 100f);
            var key  = CsoundUnityScoreState.ChangeKey(clip);

            clip.scoreInfo.stepLanes[0].steps[4].enabled = true;

            Assert.AreNotEqual(key, CsoundUnityScoreState.ChangeKey(clip));
        }

        // verboseLog is public, so it goes into the JSON like everything else, and it is not what the clip
        // plays: a preset saved while debugging must not turn the logs on wherever it lands.
        [Test]
        public void Applying_a_state_leaves_verbose_log_alone()
        {
            Save(Clip("Drum", 100f, id: "aaa"));

            var target = Clip("Drum", 1f, id: "aaa");
            target.verboseLog = true;

            Assert.AreEqual(1, _snapshot.ApplyTo(Live(target)));
            Assert.AreEqual(100f, target.bpm, 0.001f, "the state still arrives");
            Assert.IsTrue(target.verboseLog, "and the logging switch is the clip's own");
        }

        // What a snapshot is allowed to be applied to. The autosave leans on this: the state read from the
        // behaviours that are going away is labelled with the timeline they came from, not with the one the
        // director plays next, so it cannot be written onto another timeline's clips.
        [Test]
        public void A_snapshot_belongs_to_the_timeline_it_was_taken_from()
        {
            var a = ScriptableObject.CreateInstance<UnityEngine.Timeline.TimelineAsset>();
            var b = ScriptableObject.CreateInstance<UnityEngine.Timeline.TimelineAsset>();
            a.name = "A";
            b.name = "B";

            try
            {
                _snapshot.CaptureFrom(new List<ScoreBehaviour> { Clip("Drum", 100f, id: "aaa") }, a);

                Assert.IsTrue(_snapshot.BelongsTo(a));
                Assert.IsFalse(_snapshot.BelongsTo(b));
                Assert.IsTrue(_snapshot.BelongsTo(null), "nothing to compare against says nothing either way");

                // A snapshot that came in as JSON has the name and not the reference.
                _snapshot.timeline = null;
                Assert.IsTrue(_snapshot.BelongsTo(a));
                Assert.IsFalse(_snapshot.BelongsTo(b));

                _snapshot.timelineAssetName = string.Empty;
                Assert.IsTrue(_snapshot.BelongsTo(b), "a snapshot that names no timeline is not refused");
            }
            finally
            {
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
            }
        }

        [Test]
        public void A_snapshot_knows_whether_its_clips_can_be_found_again()
        {
            Save(Clip("Drum", 100f), Clip("Bass", 110f));
            Assert.IsFalse(_snapshot.HasUsableClipIds, "no clip carries an id");

            Save(Clip("Drum", 100f, id: "aaa"), Clip("Bass", 110f, id: "aaa"));
            Assert.IsFalse(_snapshot.HasUsableClipIds, "the only id belongs to both clips");

            Save(Clip("Drum", 100f, id: "aaa"), Clip("Bass", 110f, id: "bbb"));
            Assert.IsTrue(_snapshot.HasUsableClipIds);
        }
    }
}

#endif
