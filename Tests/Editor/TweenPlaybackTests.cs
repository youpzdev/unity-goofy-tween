using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class TweenPlaybackTests
{
    private readonly List<Object> objects = new();

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        yield return new EnterPlayMode();
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        for (int i = objects.Count - 1; i >= 0; i--) if (objects[i] != null) Object.DestroyImmediate(objects[i]);
        objects.Clear();
        yield return new ExitPlayMode();
    }

    [Test]
    public void PlayerReplacesItsOwnTweenAndRestoresNonUnitScale()
    {
        var target = Target();
        target.localScale = new Vector3(2, 3, 4);
        var player = target.gameObject.AddComponent<TweenPlayer>();
        player.CaptureInitialState();
        player.Preset = Preset(TweenEffect.Scale, Vector3.one * 2);
        var foreign = target.DOMove(Vector3.one * 10, 3).Pause();
        try
        {
            var first = player.Play();
            first.Goto(0.5f);
            Vector3 visible = target.localScale;
            var second = player.Play();
            Assert.That(first.IsActive(), Is.False);
            Assert.That(target.localScale, Is.EqualTo(visible));
            second.Goto(1);
            player.Stop(TweenStopMode.RestoreInitial);
            Assert.That(target.localScale, Is.EqualTo(new Vector3(2, 3, 4)));
            Assert.That(foreign.IsActive(), Is.True);
        }
        finally {foreign.Kill();}
    }

    [Test]
    public void InvalidPresetDoesNotCancelRunningPlayer()
    {
        var player = Target().gameObject.AddComponent<TweenPlayer>();
        player.Preset = Preset(TweenEffect.Move, Vector3.right * 10);
        var tween = player.Play();
        tween.Goto(0.25f);
        player.Preset = null;
        Assert.Throws<ArgumentNullException>(() => player.Play());
        Assert.That(tween.IsActive(), Is.True);
        tween.Goto(1);
        Assert.That(player.transform.localPosition.x, Is.EqualTo(10).Within(0.001f));
        player.Stop();
    }

    [Test]
    public void PlayerDisableRestoresOnlyItsOwnedPropertyAndCanReplay()
    {
        var target = Target();
        target.localPosition = new Vector3(2, 0, 0);
        var player = target.gameObject.AddComponent<TweenPlayer>();
        player.CaptureInitialState();
        player.Preset = Preset(TweenEffect.Move, Vector3.right * 10);
        var tween = player.Play();
        tween.Goto(0.5f);
        target.localScale = Vector3.one * 3;
        player.enabled = false;
        Assert.That(tween.IsActive(), Is.False);
        Assert.That(target.localPosition.x, Is.EqualTo(2).Within(0.001f));
        Assert.That(target.localScale, Is.EqualTo(Vector3.one * 3));
        player.enabled = true;
        player.Play().Goto(1);
        Assert.That(target.localPosition.x, Is.EqualTo(12).Within(0.001f));
        player.Stop();
    }

    [Test]
    public void ReducedMotionFinishesActiveAndNewPlayerTransitions()
    {
        var player = Target().gameObject.AddComponent<TweenPlayer>();
        player.Preset = Preset(TweenEffect.Move, Vector3.right * 10);
        var active = player.Play();
        active.Goto(0.2f);
        player.ReducedMotion = true;
        Assert.That(active.IsActive(), Is.False);
        Assert.That(player.transform.localPosition.x, Is.EqualTo(10).Within(0.001f));
        player.Stop(TweenStopMode.RestoreInitial);
        var next = player.Play();
        Assert.That(next.IsActive(), Is.False);
        Assert.That(player.transform.localPosition.x, Is.EqualTo(10).Within(0.001f));
    }

    [Test]
    public void SequenceAppendsJoinsAndAppliesStagger()
    {
        var sequence = Target().gameObject.AddComponent<TweenSequence>();
        var first = Target();
        var second = Target();
        var third = Target();
        sequence.Stagger = 0.1f;
        sequence.Entries = new[] {Entry(first), Entry(second, true), Entry(third)};
        var tween = sequence.Play();
        Assert.That(tween.Duration(false), Is.EqualTo(2.2f).Within(0.001f));
        tween.Goto(0.5f);
        Assert.That(first.localPosition.x, Is.EqualTo(5).Within(0.001f));
        Assert.That(second.localPosition.x, Is.EqualTo(4).Within(0.001f));
        Assert.That(third.localPosition.x, Is.Zero);
        tween.Complete();
        Assert.That(third.localPosition.x, Is.EqualTo(10).Within(0.001f));
        sequence.Stop(TweenStopMode.RestoreInitial);
        Assert.That(first.localPosition, Is.EqualTo(Vector3.zero));
        Assert.That(second.localPosition, Is.EqualTo(Vector3.zero));
        Assert.That(third.localPosition, Is.EqualTo(Vector3.zero));
    }

    [Test]
    public void InvalidSequenceEntryDoesNotLeakOrCancelExistingPlayback()
    {
        var sequence = Target().gameObject.AddComponent<TweenSequence>();
        var entry = Entry(Target());
        sequence.Entries = new[] {entry};
        var running = sequence.Play();
        running.Goto(0.1f);
        int count = DOTween.TotalActiveTweens();
        sequence.Entries = new[] {entry, new TweenSequenceEntry()};
        var error = Assert.Throws<ArgumentException>(() => sequence.Play());
        Assert.That(error.Message, Does.Contain("entries[1]"));
        Assert.That(running.IsActive(), Is.True);
        Assert.That(DOTween.TotalActiveTweens(), Is.EqualTo(count));
        sequence.Stop(TweenStopMode.RestoreInitial);
    }

    [Test]
    public void SequenceReplayKeepsInitialRestoreStateAndReducedMotionFinishes()
    {
        var sequence = Target().gameObject.AddComponent<TweenSequence>();
        var entry = Entry(Target());
        sequence.Entries = new[] {entry};
        sequence.Play().Goto(0.5f);
        var replacement = sequence.Play();
        replacement.Goto(0.25f);
        sequence.Stop(TweenStopMode.RestoreInitial);
        Assert.That(entry.Target.localPosition, Is.EqualTo(Vector3.zero));
        sequence.ReducedMotion = true;
        var immediate = sequence.Play();
        Assert.That(immediate.IsActive(), Is.False);
        Assert.That(entry.Target.localPosition.x, Is.EqualTo(10).Within(0.001f));
    }

    [Test]
    public void CompleteDoesNotKillPlaybackStartedByItsCompletionCallback()
    {
        var player = Target().gameObject.AddComponent<TweenPlayer>();
        player.Preset = Preset(TweenEffect.Move, Vector3.right * 10);
        Tween replacement = null;
        player.Play().OnComplete(() => replacement = player.Play());
        player.Stop(TweenStopMode.Complete);
        Assert.That(replacement, Is.Not.Null);
        Assert.That(replacement.IsActive(), Is.True, "Completion must not cancel a new playback created by the callback");
        Assert.That(player.CurrentTween, Is.SameAs(replacement));
        player.Stop();
    }

    [Test]
    public void SequenceCompleteDoesNotKillPlaybackStartedByItsCompletionCallback()
    {
        var sequence = Target().gameObject.AddComponent<TweenSequence>();
        sequence.Entries = new[] {Entry(Target())};
        Sequence replacement = null;
        sequence.Play().OnComplete(() => replacement = sequence.Play());
        sequence.Stop(TweenStopMode.Complete);
        Assert.That(replacement, Is.Not.Null);
        Assert.That(replacement.IsActive(), Is.True, "Completion must not cancel a new sequence created by the callback");
        Assert.That(sequence.CurrentSequence, Is.SameAs(replacement));
        sequence.Stop();
    }

    [Test]
    public void InfiniteChildIsRejectedWithoutTargetChanges()
    {
        var sequence = Target().gameObject.AddComponent<TweenSequence>();
        var entry = Entry(Target());
        entry.Preset.Loops = -1;
        sequence.Entries = new[] {entry};
        Assert.Throws<ArgumentException>(() => sequence.Play());
        Assert.That(entry.Target.localPosition, Is.EqualTo(Vector3.zero));
    }

    private Transform Target()
    {
        var instance = new GameObject("TweenPlaybackTests_Target");
        objects.Add(instance);

        return instance.transform;
    }

    private TweenPreset Preset(TweenEffect effect, Vector3 vector)
    {
        var preset = ScriptableObject.CreateInstance<TweenPreset>();
        preset.Effect = effect;
        preset.Vector = vector;
        preset.Duration = 1;
        preset.Ease = Ease.Linear;
        objects.Add(preset);

        return preset;
    }

    private TweenSequenceEntry Entry(Transform target, bool join = false)
    {return new TweenSequenceEntry {Target = target, Preset = Preset(TweenEffect.Move, Vector3.right * 10), JoinPrevious = join};}
}
