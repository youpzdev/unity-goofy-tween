using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class TweenCoreTests
{
    private readonly List<Tween> tweens = new();
    private readonly List<UnityEngine.Object> objects = new();

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        yield return new EnterPlayMode();
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        foreach (Tween tween in tweens) if (tween.IsActive()) tween.Kill();
        foreach (UnityEngine.Object value in objects) if (value != null) UnityEngine.Object.DestroyImmediate(value);
        tweens.Clear();
        objects.Clear();
        yield return new ExitPlayMode();
    }

    [Test]
    public void CreateReturnsPausedTweenWithoutMutatingTarget()
    {
        Transform target = Target();
        target.localPosition = new Vector3(2, 3, 4);
        var preset = Preset(TweenEffect.Move);
        preset.Vector = Vector3.right * 10;
        Tween tween = Track(GoofyTween.Create(target, preset));

        Assert.That(tween.IsPlaying(), Is.False);
        AssertVector(target.localPosition, new Vector3(2, 3, 4));
        tween.Goto(preset.Duration);
        AssertVector(target.localPosition, new Vector3(12, 3, 4));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void RelativeMoveUsesSelectedCoordinateSpace(bool localSpace)
    {
        Transform parent = Target();
        parent.position = new Vector3(10, 20, 0);
        parent.rotation = Quaternion.Euler(0, 0, 90);
        Transform target = Target();
        target.SetParent(parent, false);
        target.localPosition = Vector3.right * 2;
        Vector3 start = localSpace ? target.localPosition : target.position;
        var preset = Preset(TweenEffect.Move);
        preset.LocalSpace = localSpace;
        preset.Vector = new Vector3(3, 4, 0);
        Tween tween = Track(GoofyTween.Create(target, preset));

        tween.Goto(preset.Duration);
        AssertVector(localSpace ? target.localPosition : target.position, start + preset.Vector);
    }

    [Test]
    public void SharedScalePresetMultipliesEachTargetsOwnBaselineWithoutAssetChanges()
    {
        Transform first = Target();
        Transform second = Target();
        first.localScale = new Vector3(2, 3, 4);
        second.localScale = new Vector3(5, 6, 7);
        var preset = Preset(TweenEffect.Scale);
        preset.Vector = new Vector3(2, 0.5f, 3);
        string before = JsonUtility.ToJson(preset);
        Tween a = Track(GoofyTween.Create(first, preset));
        Tween b = Track(GoofyTween.Create(second, preset));

        a.Goto(preset.Duration);
        b.Goto(preset.Duration);
        AssertVector(first.localScale, new Vector3(4, 1.5f, 12));
        AssertVector(second.localScale, new Vector3(10, 3, 21));
        Assert.That(JsonUtility.ToJson(preset), Is.EqualTo(before), "Shared preset contents must not change during creation or playback");
    }

    [Test]
    public void AbsoluteScaleUsesExactVector()
    {
        Transform target = Target();
        target.localScale = Vector3.one * 10;
        var preset = Preset(TweenEffect.Scale);
        preset.Relative = false;
        preset.Vector = new Vector3(2, 3, 4);
        Track(GoofyTween.Create(target, preset)).Goto(preset.Duration);
        AssertVector(target.localScale, preset.Vector);
    }

    [Test]
    public void SequentialRelativeMovesUseTheBaselineAtEachStepsStart()
    {
        Transform target = Target();
        var preset = Preset(TweenEffect.Move);
        preset.Vector = Vector3.right * 10;
        string before = JsonUtility.ToJson(preset);
        Tween first = Track(GoofyTween.Create(target, preset));
        Tween second = Track(GoofyTween.Create(target, preset));
        Tween sequence = Track(DOTween.Sequence().Append(first).Append(second).Pause());

        sequence.Goto(preset.Duration * 2);
        AssertVector(target.localPosition, Vector3.right * 20);
        Assert.That(JsonUtility.ToJson(preset), Is.EqualTo(before));
    }

    [Test]
    public void SequentialRelativeScalesMultiplyTheBaselineAtEachStepsStart()
    {
        Transform target = Target();
        var preset = Preset(TweenEffect.Scale);
        preset.Vector = Vector3.one * 2;
        string before = JsonUtility.ToJson(preset);
        Tween first = Track(GoofyTween.Create(target, preset));
        Tween second = Track(GoofyTween.Create(target, preset));
        Tween sequence = Track(DOTween.Sequence().Append(first).Append(second).Pause());

        sequence.Goto(preset.Duration * 2);
        AssertVector(target.localScale, Vector3.one * 4);
        Assert.That(JsonUtility.ToJson(preset), Is.EqualTo(before));
    }

    [TestCase(TweenEffect.Rotate)]
    [TestCase(TweenEffect.Spin)]
    public void SequentialRelativeRotationsUseEachStepsStartingRotation(TweenEffect effect)
    {
        Transform target = Target();
        var preset = Preset(effect);
        preset.Vector = new Vector3(0, 0, 60);
        Tween first = Track(GoofyTween.Create(target, preset));
        Tween second = Track(GoofyTween.Create(target, preset));
        Tween sequence = Track(DOTween.Sequence().Append(first).Append(second).Pause());

        sequence.Goto(preset.Duration * 2);
        Assert.That(Quaternion.Angle(target.localRotation, Quaternion.Euler(0, 0, 120)), Is.LessThan(0.01f));
    }

    [TestCase(TweenEffect.Jump)]
    [TestCase(TweenEffect.Path)]
    public void SequentialRelativeJumpAndPathUseEachStepsStartingPosition(TweenEffect effect)
    {
        Transform target = Target();
        target.localPosition = new Vector3(2, 3, 4);
        var preset = Preset(effect);
        preset.Vector = Vector3.right * 3;
        preset.Points = new[] {Vector3.right, Vector3.right * 3};
        preset.PathType = PathType.Linear;
        Tween first = Track(GoofyTween.Create(target, preset));
        Tween second = Track(GoofyTween.Create(target, preset));
        Tween sequence = Track(DOTween.Sequence().Append(first).Append(second).Pause());

        sequence.Goto(preset.Duration * 2);
        AssertVector(target.localPosition, new Vector3(8, 3, 4));
    }

    [TestCase(TweenEffect.Pulse)]
    [TestCase(TweenEffect.Float)]
    [TestCase(TweenEffect.Wobble)]
    [TestCase(TweenEffect.Heartbeat)]
    public void CyclesCaptureAndRestoreBaselineAtPlaybackInsteadOfCreation(TweenEffect effect)
    {
        Transform target = Target();
        var preset = Preset(effect);
        preset.Vector = effect == TweenEffect.Pulse || effect == TweenEffect.Heartbeat ? Vector3.one * 1.5f : new Vector3(3, 6, 12);
        Tween tween = Track(GoofyTween.Create(target, preset));
        Vector3 position = target.localPosition = new Vector3(20, 30, 40);
        Vector3 scale = target.localScale = new Vector3(2, 3, 4);
        Quaternion rotation = target.localRotation = Quaternion.Euler(10, 20, 30);

        tween.Goto(preset.Duration * 0.2f);
        float change = (target.localPosition - position).sqrMagnitude + (target.localScale - scale).sqrMagnitude + Quaternion.Angle(target.localRotation, rotation);
        Assert.That(change, Is.GreaterThan(0.001f));
        tween.Goto(preset.Duration);
        AssertVector(target.localPosition, position);
        AssertVector(target.localScale, scale);
        Assert.That(Quaternion.Angle(target.localRotation, rotation), Is.LessThan(0.01f));
    }

    [Test]
    public void RelativeScaleCapturesChangesMadeAfterCreationBeforePlayback()
    {
        Transform target = Target();
        var preset = Preset(TweenEffect.Scale);
        preset.Vector = Vector3.one * 2;
        Tween tween = Track(GoofyTween.Create(target, preset));
        target.localScale = new Vector3(3, 4, 5);

        tween.Goto(preset.Duration);
        AssertVector(target.localScale, new Vector3(6, 8, 10));
    }

    [TestCase(TweenEffect.Pulse)]
    [TestCase(TweenEffect.Float)]
    [TestCase(TweenEffect.Wobble)]
    [TestCase(TweenEffect.Heartbeat)]
    public void CompositeEffectsMoveAndReturnToTheirOwnBaseline(TweenEffect effect)
    {
        Transform target = Target();
        Vector3 position = target.localPosition = new Vector3(2, 3, 4);
        Quaternion rotation = target.localRotation = Quaternion.Euler(10, 20, 30);
        Vector3 scale = target.localScale = new Vector3(2, 3, 4);
        var preset = Preset(effect);
        preset.Vector = effect == TweenEffect.Pulse || effect == TweenEffect.Heartbeat ? Vector3.one * 1.5f : new Vector3(5, 8, 12);
        Tween tween = Track(GoofyTween.Create(target, preset));
        tween.Goto(preset.Duration * 0.2f);
        float change = (target.localPosition - position).sqrMagnitude + (target.localScale - scale).sqrMagnitude + Quaternion.Angle(target.localRotation, rotation);
        Assert.That(change, Is.GreaterThan(0.001f), "The effect must visibly change its channel before returning");
        tween.Goto(preset.Duration);
        AssertVector(target.localPosition, position);
        AssertVector(target.localScale, scale);
        Assert.That(Quaternion.Angle(target.localRotation, rotation), Is.LessThan(0.01f));
    }

    [TestCase(TweenEffect.Rotate)]
    [TestCase(TweenEffect.Spin)]
    public void RotationUsesRelativeEulerOffset(TweenEffect effect)
    {
        Transform target = Target();
        target.localRotation = Quaternion.Euler(10, 20, 30);
        Vector3 start = target.localEulerAngles;
        var preset = Preset(effect);
        preset.Vector = new Vector3(0, 0, 90);
        Track(GoofyTween.Create(target, preset)).Goto(preset.Duration);
        Assert.That(Quaternion.Angle(target.localRotation, Quaternion.Euler(start + preset.Vector)), Is.LessThan(0.01f));
    }

    [Test]
    public void JumpAndPathReachConfiguredEndpointAndDoNotMutateWaypoints()
    {
        Transform target = Target();
        target.localPosition = Vector3.one * 2;
        var jump = Preset(TweenEffect.Jump);
        jump.Vector = new Vector3(3, 0, 0);
        jump.Amount = 2;
        Track(GoofyTween.Create(target, jump)).Goto(jump.Duration);
        AssertVector(target.localPosition, new Vector3(5, 2, 2));

        var path = Preset(TweenEffect.Path);
        path.PathType = PathType.Linear;
        path.Points = new[] {Vector3.right, Vector3.up * 4};
        Vector3[] before = (Vector3[])path.Points.Clone();
        Tween tween = Track(GoofyTween.Create(target, path));
        tween.Goto(path.Duration);
        AssertVector(target.localPosition, new Vector3(5, 6, 2));
        CollectionAssert.AreEqual(before, path.Points);
    }

    [TestCase(TweenEffect.PunchScale)]
    [TestCase(TweenEffect.ShakePosition)]
    public void NativeImpulseEffectsRestoreBaseline(TweenEffect effect)
    {
        Transform target = Target();
        Vector3 position = target.localPosition = new Vector3(2, 3, 4);
        Vector3 scale = target.localScale = new Vector3(2, 3, 4);
        var preset = Preset(effect);
        preset.Vector = Vector3.one * 0.2f;
        Track(GoofyTween.Create(target, preset)).Goto(preset.Duration);
        AssertVector(target.localPosition, position);
        AssertVector(target.localScale, scale);
    }

    [TestCase(TweenEffect.PunchScale)]
    [TestCase(TweenEffect.ShakePosition)]
    [TestCase(TweenEffect.Jump)]
    [TestCase(TweenEffect.Path)]
    public void ZeroDurationSpecialEffectsReturnPausedTweenAndCompleteToTheirFinalState(TweenEffect effect)
    {
        Transform target = Target();
        Vector3 position = target.localPosition = new Vector3(2, 3, 4);
        Vector3 scale = target.localScale = new Vector3(2, 3, 4);
        var preset = Preset(effect);
        preset.Duration = 0;
        preset.Vector = Vector3.right;
        preset.Points = new[] {Vector3.right, Vector3.up * 2};
        preset.PathType = PathType.Linear;
        Tween tween = null;

        Assert.DoesNotThrow(() => tween = Track(GoofyTween.Create(target, preset)), $"Zero duration {effect} must still produce a usable native tween");
        Assert.That(tween, Is.Not.Null);
        Assert.That(tween.IsPlaying(), Is.False);
        AssertVector(target.localPosition, position);
        AssertVector(target.localScale, scale);
        tween.Complete();
        Vector3 expected = effect == TweenEffect.Jump ? position + preset.Vector : effect == TweenEffect.Path ? position + preset.Points[1] : position;
        AssertVector(target.localPosition, expected);
        AssertVector(target.localScale, scale);
    }

    [TestCase(true, true)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(false, false)]
    public void ZeroDurationJumpHonorsRelativeAndCoordinateSpace(bool relative, bool localSpace)
    {
        Transform parent = Target();
        parent.position = new Vector3(10, 20, 30);
        parent.rotation = Quaternion.Euler(0, 0, 90);
        Transform target = Target();
        target.SetParent(parent, false);
        target.localPosition = new Vector3(2, 3, 4);
        Vector3 start = localSpace ? target.localPosition : target.position;
        var preset = Preset(TweenEffect.Jump);
        preset.Duration = 0;
        preset.Vector = new Vector3(1, 2, 3);
        preset.Amount = 50;
        preset.Relative = relative;
        preset.LocalSpace = localSpace;

        Track(GoofyTween.Create(target, preset)).Complete();
        AssertVector(localSpace ? target.localPosition : target.position, relative ? start + preset.Vector : preset.Vector);
    }

    [Test]
    public void FadeSupportsCanvasGroupGraphicAndSpriteWithoutChangingColorChannels()
    {
        var preset = Preset(TweenEffect.Fade);
        preset.Amount = 0.2f;
        Transform groupTarget = Target();
        CanvasGroup group = groupTarget.gameObject.AddComponent<CanvasGroup>();
        Track(GoofyTween.Create(groupTarget, preset)).Goto(preset.Duration);
        Assert.That(group.alpha, Is.EqualTo(0.2f).Within(0.001f));
        Transform graphicTarget = UiTarget();
        Image image = graphicTarget.gameObject.AddComponent<Image>();
        image.color = new Color(0.3f, 0.4f, 0.5f, 1);
        Track(GoofyTween.Create(graphicTarget, preset)).Goto(preset.Duration);
        AssertColor(image.color, new Color(0.3f, 0.4f, 0.5f, 0.2f));
        Transform spriteTarget = Target();
        SpriteRenderer sprite = spriteTarget.gameObject.AddComponent<SpriteRenderer>();
        sprite.color = new Color(0.7f, 0.6f, 0.5f, 1);
        Track(GoofyTween.Create(spriteTarget, preset)).Goto(preset.Duration);
        AssertColor(sprite.color, new Color(0.7f, 0.6f, 0.5f, 0.2f));
    }

    [Test]
    public void ColorSupportsGraphicAndSprite()
    {
        var preset = Preset(TweenEffect.Color);
        preset.Color = new Color(0.2f, 0.4f, 0.6f, 0.8f);
        Transform graphicTarget = UiTarget();
        Image image = graphicTarget.gameObject.AddComponent<Image>();
        Transform spriteTarget = Target();
        SpriteRenderer sprite = spriteTarget.gameObject.AddComponent<SpriteRenderer>();
        Track(GoofyTween.Create(graphicTarget, preset)).Goto(preset.Duration);
        Track(GoofyTween.Create(spriteTarget, preset)).Goto(preset.Duration);
        AssertColor(image.color, preset.Color);
        AssertColor(sprite.color, preset.Color);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void ManualUpdateHonorsSelectedClockAndDelay(bool unscaledTime)
    {
        Transform target = Target();
        var preset = Preset(TweenEffect.Move);
        preset.Duration = 1;
        preset.Delay = 0.25f;
        preset.UnscaledTime = unscaledTime;
        preset.Vector = Vector3.right * 10;
        Tween tween = Track(GoofyTween.Create(target, preset));
        tween.Play();

        tween.ManualUpdate(0, 0.25f);
        AssertVector(target.localPosition, Vector3.zero);
        tween.ManualUpdate(0, 0.5f);
        AssertVector(target.localPosition, unscaledTime ? Vector3.right * 5 : Vector3.zero);
        tween.ManualUpdate(0.75f, 0);
        AssertVector(target.localPosition, Vector3.right * 5);
    }

    [Test]
    public void NativeYoyoAndInfiniteLoopsHonorPresetSettings()
    {
        Transform target = Target();
        var preset = Preset(TweenEffect.Move);
        preset.Duration = 1;
        preset.Vector = Vector3.right * 10;
        preset.Loops = 2;
        preset.LoopType = LoopType.Yoyo;
        Tween yoyo = Track(GoofyTween.Create(target, preset));
        yoyo.Goto(1);
        AssertVector(target.localPosition, Vector3.right * 10);
        yoyo.Goto(2);
        AssertVector(target.localPosition, Vector3.zero);
        yoyo.Kill();

        preset.Loops = -1;
        preset.LoopType = LoopType.Restart;
        Tween infinite = Track(GoofyTween.Create(target, preset));
        infinite.Goto(2.5f);
        AssertVector(target.localPosition, Vector3.right * 5);
        Assert.That(infinite.IsActive(), Is.True);
    }

    [Test]
    public void KillingOneTweenDoesNotCancelAnotherOrAnExternalTween()
    {
        Transform first = Target();
        Transform second = Target();
        Transform externalTarget = Target();
        var preset = Preset(TweenEffect.Move);
        preset.Duration = 1;
        preset.Vector = Vector3.right * 10;
        Tween a = Track(GoofyTween.Play(first, preset));
        Tween b = Track(GoofyTween.Play(second, preset));
        Tween external = Track(externalTarget.DOMove(Vector3.up * 10, 1).Pause());
        a.Kill();

        b.Goto(1);
        external.Goto(1);
        AssertVector(first.position, Vector3.zero);
        AssertVector(second.position, Vector3.right * 10);
        AssertVector(externalTarget.position, Vector3.up * 10);
    }

    [Test]
    public void PlayLinksLifetimeWhileCreateRemainsCallerOwned()
    {
        Transform linked = Target();
        Transform unlinked = Target();
        var preset = Preset(TweenEffect.Move);
        Tween a = Track(GoofyTween.Play(linked, preset));
        Tween b = Track(GoofyTween.Create(unlinked, preset));
        a.SetUpdate(UpdateType.Manual, true);
        linked.gameObject.SetActive(false);
        unlinked.gameObject.SetActive(false);
        DOTween.ManualUpdate(0.01f, 0.01f);

        Assert.That(a.IsActive(), Is.False, "Play must kill on target disable");
        Assert.That(b.IsActive(), Is.True, "Raw Create is owned by its caller");
    }

    [TestCase("Duration")]
    [TestCase("Delay")]
    [TestCase("Vector.x")]
    [TestCase("Amount")]
    [TestCase("Color.a")]
    [TestCase("Elasticity")]
    [TestCase("Randomness")]
    [TestCase("Points[0].x")]
    public void NonFiniteSettingsFailBeforeCreatingTweenOrMutatingTarget(string field)
    {
        Transform target = Target();
        Vector3 start = target.position = new Vector3(2, 3, 4);
        var preset = Preset(TweenEffect.Move);
        switch (field)
        {
            case "Duration": preset.Duration = float.NaN; break;
            case "Delay": preset.Delay = float.PositiveInfinity; break;
            case "Vector.x": preset.Vector = new Vector3(float.NaN, 0, 0); break;
            case "Amount": preset.Amount = float.NaN; break;
            case "Color.a": preset.Color = new Color(1, 1, 1, float.NaN); break;
            case "Elasticity": preset.Elasticity = float.NaN; break;
            case "Randomness": preset.Randomness = float.NaN; break;
            case "Points[0].x": preset.Points = new[] {new Vector3(float.NaN, 0, 0)}; break;
        }
        int before = DOTween.TotalActiveTweens();
        var error = Assert.Throws<ArgumentException>(() => GoofyTween.Create(target, preset));
        Assert.That(error.ParamName, Is.EqualTo(field));
        Assert.That(DOTween.TotalActiveTweens(), Is.EqualTo(before), "Validation must not leak an active sequence or child tween");
        AssertVector(target.position, start);
    }

    [TestCase(TweenEffect.Fade)]
    [TestCase(TweenEffect.Color)]
    public void MissingVisualChannelIsRejectedWithoutCreatingTween(TweenEffect effect)
    {
        var preset = Preset(effect);
        int before = DOTween.TotalActiveTweens();
        Assert.Throws<ArgumentException>(() => GoofyTween.Create(Target(), preset));
        Assert.That(DOTween.TotalActiveTweens(), Is.EqualTo(before));
    }

    [Test]
    public void MissingAndMalformedSettingsHaveNamedErrors()
    {
        Transform target = Target();
        var preset = Preset(TweenEffect.Path);
        Assert.Throws<ArgumentNullException>(() => GoofyTween.Create(null, preset));
        Assert.Throws<ArgumentNullException>(() => GoofyTween.Create(target, null));
        Assert.That(Assert.Throws<ArgumentException>(() => GoofyTween.Create(target, preset)).ParamName, Is.EqualTo("Points"));
        preset.Effect = (TweenEffect)999;
        Assert.That(Assert.Throws<ArgumentException>(() => GoofyTween.Create(target, preset)).ParamName, Is.EqualTo("Effect"));
        preset.Effect = TweenEffect.Pulse;
        preset.LoopType = LoopType.Incremental;
        Assert.That(Assert.Throws<ArgumentException>(() => GoofyTween.Create(target, preset)).ParamName, Is.EqualTo("LoopType"));
        preset.LoopType = LoopType.Restart;
        preset.Loops = 0;
        Assert.That(Assert.Throws<ArgumentException>(() => GoofyTween.Create(target, preset)).ParamName, Is.EqualTo("Loops"));
        preset.Loops = -1;
        preset.Duration = 0;
        Assert.That(Assert.Throws<ArgumentException>(() => GoofyTween.Create(target, preset)).ParamName, Is.EqualTo("Loops"));
    }

    [Test]
    public void PresetChangesAfterCreateDoNotRetargetExistingTween()
    {
        Transform target = Target();
        var preset = Preset(TweenEffect.Move);
        preset.Vector = Vector3.right * 3;
        Tween tween = Track(GoofyTween.Create(target, preset));
        preset.Vector = Vector3.up * 20;
        preset.Duration = 10;
        tween.Goto(0.25f);
        AssertVector(target.localPosition, Vector3.right * 3);
    }

    private Tween Track(Tween tween) {tweens.Add(tween); return tween.SetAutoKill(false);}

    private TweenPreset Preset(TweenEffect effect)
    {
        var preset = ScriptableObject.CreateInstance<TweenPreset>();
        preset.Effect = effect;
        preset.Ease = Ease.Linear;
        objects.Add(preset);

        return preset;
    }

    private Transform Target()
    {
        var target = new GameObject("Tween core target");
        objects.Add(target);

        return target.transform;
    }

    private Transform UiTarget()
    {
        var target = new GameObject("Tween core UI target", typeof(RectTransform));
        objects.Add(target);

        return target.transform;
    }

    private static void AssertVector(Vector3 actual, Vector3 expected) {Assert.That(Vector3.Distance(actual, expected), Is.LessThan(0.001f), $"Expected {expected}, got {actual}");}
    private static void AssertColor(Color actual, Color expected) {Assert.That(Vector4.Distance(actual, expected), Is.LessThan(0.001f), $"Expected {expected}, got {actual}");}
}
