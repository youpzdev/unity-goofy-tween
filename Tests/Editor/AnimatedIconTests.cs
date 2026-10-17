using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using DG.Tweening;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class AnimatedIconTests
{
    private readonly List<UnityEngine.Object> objects = new();

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        yield return new EnterPlayMode();
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        foreach (UnityEngine.Object value in objects) if (value is GameObject target && target != null) UnityEngine.Object.DestroyImmediate(target);
        foreach (UnityEngine.Object value in objects) if (value != null) UnityEngine.Object.DestroyImmediate(value);
        objects.Clear();
        yield return new ExitPlayMode();
    }

    [Test]
    public void SwitchingAllSevenAnimationsRestoresBaselineAndPreservesSharedPresets()
    {
        AnimatedIcon icon = Icon();
        var before = new string[icon.Bindings.Length];
        for (int i = 0; i < before.Length; i++) before[i] = JsonUtility.ToJson(icon.Bindings[i].Preset);
        Vector3 position = icon.transform.localPosition;
        Vector3 scale = icon.transform.localScale;
        Quaternion rotation = icon.transform.localRotation;
        Tween previous = null;

        foreach (AnimatedIcon.AnimationBinding binding in icon.Bindings)
        {
            icon.PlayAnimation(binding.Animation);
            if (previous != null) Assert.That(previous.IsActive(), Is.False, "Switching must kill only the previous icon tween");
            Assert.That(icon.SelectedAnimation, Is.EqualTo(binding.Animation));
            Assert.That(icon.CurrentTween, Is.Not.Null);
            Assert.That(icon.CurrentTween.IsActive(), Is.True);
            icon.CurrentTween.Goto(binding.Preset.Duration * 0.37f);
            float change = (icon.transform.localPosition - position).sqrMagnitude + (icon.transform.localScale - scale).sqrMagnitude + Quaternion.Angle(icon.transform.localRotation, rotation);
            Assert.That(change, Is.GreaterThan(0.0001f), $"Animation {binding.Animation} did not change its target");
            previous = icon.CurrentTween;
        }

        icon.StopAnimation();
        AssertBaseline(icon.transform, position, scale, rotation);
        Assert.That(icon.CurrentTween, Is.Null);
        for (int i = 0; i < before.Length; i++) Assert.That(JsonUtility.ToJson(icon.Bindings[i].Preset), Is.EqualTo(before[i]), $"Shared asset {i} was changed");
    }

    [Test]
    public void StopAndNoneRestoreNonUnitScaleAndAllowReuseWithoutDrift()
    {
        AnimatedIcon icon = Icon();
        Vector3 position = icon.transform.localPosition;
        Vector3 scale = icon.transform.localScale;
        Quaternion rotation = icon.transform.localRotation;
        icon.PlayAnimation(AnimatedIcon.IdleAnimation.Pulse);
        icon.CurrentTween.Goto(0.25f);
        icon.StopAnimation();
        AssertBaseline(icon.transform, position, scale, rotation);
        icon.PlayAnimation(AnimatedIcon.IdleAnimation.Pulse);
        icon.CurrentTween.Goto(0.25f);
        Assert.That(Vector3.Distance(icon.transform.localScale, scale * 1.5f), Is.LessThan(0.001f));
        icon.PlayAnimation(AnimatedIcon.IdleAnimation.None);
        Assert.That(icon.SelectedAnimation, Is.EqualTo(AnimatedIcon.IdleAnimation.None));
        Assert.That(icon.CurrentTween, Is.Null);
        AssertBaseline(icon.transform, position, scale, rotation);
    }

    [Test]
    public void MissingDuplicateAndMalformedBindingsKeepCurrentPlayback()
    {
        AnimatedIcon icon = Icon();
        icon.PlayAnimation(AnimatedIcon.IdleAnimation.Pulse);
        Tween previous = icon.CurrentTween;
        TweenPreset previousPreset = icon.Player.Preset;
        AnimatedIcon.AnimationBinding[] all = icon.Bindings;
        icon.Bindings = new[] {all[1]};
        LogAssert.Expect(LogType.Error, new Regex("AnimatedIcon .*Bindings.*Spin"));
        icon.PlayAnimation(AnimatedIcon.IdleAnimation.Spin);
        Assert.That(icon.CurrentTween, Is.SameAs(previous));
        Assert.That(previous.IsActive(), Is.True);
        Assert.That(icon.Player.Preset, Is.SameAs(previousPreset));
        icon.Bindings = new[] {all[1], all[1]};
        LogAssert.Expect(LogType.Error, new Regex("AnimatedIcon .*Bindings.*повтор.*Pulse"));
        icon.PlayAnimation(AnimatedIcon.IdleAnimation.Pulse);
        Assert.That(icon.CurrentTween, Is.SameAs(previous));
        icon.Bindings = all;
        all[6].Preset.Duration = float.NaN;
        LogAssert.Expect(LogType.Error, new Regex("AnimatedIcon .*Duration"));
        icon.PlayAnimation(AnimatedIcon.IdleAnimation.Heartbeat);
        Assert.That(icon.CurrentTween, Is.SameAs(previous));
        Assert.That(previous.IsActive(), Is.True);
        Assert.That(icon.SelectedAnimation, Is.EqualTo(AnimatedIcon.IdleAnimation.Pulse));
    }

    [Test]
    public void DisableRestoresBaselineAndEnableRestartsSelectedSharedPreset()
    {
        AnimatedIcon icon = Icon();
        icon.PlayOnEnable = true;
        Vector3 position = icon.transform.localPosition;
        Vector3 scale = icon.transform.localScale;
        Quaternion rotation = icon.transform.localRotation;
        icon.PlayAnimation(AnimatedIcon.IdleAnimation.Wobble);
        Tween previous = icon.CurrentTween;
        previous.Goto(0.2f);
        icon.gameObject.SetActive(false);
        Assert.That(previous.IsActive(), Is.False);
        Assert.That(icon.CurrentTween, Is.Null);
        AssertBaseline(icon.transform, position, scale, rotation);
        icon.gameObject.SetActive(true);
        Assert.That(icon.CurrentTween, Is.Not.Null);
        Assert.That(icon.CurrentTween.IsActive(), Is.True);
        Assert.That(icon.SelectedAnimation, Is.EqualTo(AnimatedIcon.IdleAnimation.Wobble));
        Assert.That(icon.Player.Preset, Is.SameAs(icon.Bindings[2].Preset));
    }

    [Test]
    public void ExplicitTargetAndDestroyedTargetStopSafely()
    {
        AnimatedIcon icon = Icon();
        var target = new GameObject("Animated icon external target");
        objects.Add(target);
        target.transform.localScale = new Vector3(3, 4, 5);
        icon.Target = target.transform;
        icon.PlayAnimation(AnimatedIcon.IdleAnimation.Pulse);
        icon.CurrentTween.Goto(0.25f);
        Assert.That(Vector3.Distance(target.transform.localScale, new Vector3(4.5f, 6, 7.5f)), Is.LessThan(0.001f));
        icon.StopAnimation();
        Assert.That(Vector3.Distance(target.transform.localScale, new Vector3(3, 4, 5)), Is.LessThan(0.001f));
        icon.PlayAnimation(AnimatedIcon.IdleAnimation.Float);
        UnityEngine.Object.DestroyImmediate(target);
        Assert.DoesNotThrow(icon.StopAnimation);
        LogAssert.Expect(LogType.Error, new Regex("AnimatedIcon .*объект не задан"));
        icon.PlayAnimation(AnimatedIcon.IdleAnimation.Pulse);
        Assert.That(icon.CurrentTween, Is.Null);
    }

    [Test]
    public void SpeedAffectsOnlyOwnedTweenAndInvalidSpeedPreservesPlayback()
    {
        AnimatedIcon icon = Icon();
        string before = JsonUtility.ToJson(icon.Bindings[1].Preset);
        icon.PlayAnimation(AnimatedIcon.IdleAnimation.Pulse);
        Tween tween = icon.CurrentTween;
        icon.Speed = 2;
        Assert.That(tween.timeScale, Is.EqualTo(2));
        Assert.Throws<ArgumentOutOfRangeException>(() => icon.Speed = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => icon.Speed = float.NaN);
        Assert.That(icon.Speed, Is.EqualTo(2));
        Assert.That(icon.CurrentTween, Is.SameAs(tween));
        Assert.That(JsonUtility.ToJson(icon.Bindings[1].Preset), Is.EqualTo(before));
    }

    [Test]
    public void SharedAssetsCanAnimateTwoIndependentIcons()
    {
        AnimatedIcon first = Icon();
        AnimatedIcon second = Icon();
        second.Bindings = first.Bindings;
        first.PlayAnimation(AnimatedIcon.IdleAnimation.Pulse);
        second.PlayAnimation(AnimatedIcon.IdleAnimation.Pulse);
        Tween independent = second.CurrentTween;
        first.StopAnimation();
        Assert.That(independent.IsActive(), Is.True);
        independent.Goto(0.25f);
        Assert.That(Vector3.Distance(second.transform.localScale, new Vector3(3, 4.5f, 6)), Is.LessThan(0.001f));
    }

    [Test]
    public void ReentrantSwitchFromOnKillKeepsTheLatestRequest()
    {
        AnimatedIcon icon = Icon();
        icon.PlayAnimation(AnimatedIcon.IdleAnimation.Pulse);
        icon.CurrentTween.OnKill(() => icon.PlayAnimation(AnimatedIcon.IdleAnimation.Float));
        icon.PlayAnimation(AnimatedIcon.IdleAnimation.Spin);
        Assert.That(icon.SelectedAnimation, Is.EqualTo(AnimatedIcon.IdleAnimation.Float));
        Assert.That(icon.Player.Preset, Is.SameAs(icon.Bindings[3].Preset));
        Assert.That(icon.CurrentTween.IsActive(), Is.True);
    }

    private AnimatedIcon Icon()
    {
        var target = new GameObject("Animated icon test");
        objects.Add(target);
        target.transform.localPosition = new Vector3(2, 4, 6);
        target.transform.localScale = new Vector3(2, 3, 4);
        target.transform.localRotation = Quaternion.Euler(10, 20, 30);
        var icon = target.AddComponent<AnimatedIcon>();
        icon.PlayOnEnable = false;
        icon.Bindings = new[]
        {
            Binding(AnimatedIcon.IdleAnimation.Bounce, TweenEffect.Float, Vector3.up),
            Binding(AnimatedIcon.IdleAnimation.Pulse, TweenEffect.Pulse, Vector3.one * 1.5f),
            Binding(AnimatedIcon.IdleAnimation.Wobble, TweenEffect.Wobble, new Vector3(0, 0, 10)),
            Binding(AnimatedIcon.IdleAnimation.Float, TweenEffect.Float, Vector3.up * 0.5f),
            Binding(AnimatedIcon.IdleAnimation.Shake, TweenEffect.ShakePosition, Vector3.one * 0.2f),
            Binding(AnimatedIcon.IdleAnimation.Spin, TweenEffect.Spin, new Vector3(0, 0, 360)),
            Binding(AnimatedIcon.IdleAnimation.Heartbeat, TweenEffect.Heartbeat, Vector3.one * 1.5f)
        };

        return icon;
    }

    private AnimatedIcon.AnimationBinding Binding(AnimatedIcon.IdleAnimation animation, TweenEffect effect, Vector3 vector)
    {
        var preset = ScriptableObject.CreateInstance<TweenPreset>();
        objects.Add(preset);
        preset.Effect = effect;
        preset.Vector = vector;
        preset.Duration = 0.5f;
        preset.Ease = Ease.Linear;
        preset.Loops = -1;

        return new AnimatedIcon.AnimationBinding {Animation = animation, Preset = preset};
    }

    private static void AssertBaseline(Transform target, Vector3 position, Vector3 scale, Quaternion rotation)
    {
        Assert.That(Vector3.Distance(target.localPosition, position), Is.LessThan(0.001f));
        Assert.That(Vector3.Distance(target.localScale, scale), Is.LessThan(0.001f));
        Assert.That(Quaternion.Angle(target.localRotation, rotation), Is.LessThan(0.01f));
    }
}
