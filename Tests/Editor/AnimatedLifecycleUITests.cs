using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using DG.Tweening;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class AnimatedLifecycleUITests
{
    private readonly List<Object> objects = new();
    private readonly List<Tween> tweens = new();
    private bool initialAutoKill;
    private bool initialRecyclable;
    private AutoPlay initialAutoPlay;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        yield return new EnterPlayMode();
        DOTween.Init();
        initialAutoKill = DOTween.defaultAutoKill;
        initialRecyclable = DOTween.defaultRecyclable;
        initialAutoPlay = DOTween.defaultAutoPlay;
        DOTween.ClearCachedTweens();
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        DOTween.defaultAutoKill = initialAutoKill;
        DOTween.defaultRecyclable = initialRecyclable;
        DOTween.defaultAutoPlay = initialAutoPlay;
        for (int i = tweens.Count - 1; i >= 0; i--)
            if (tweens[i] != null && tweens[i].IsActive()) tweens[i].Kill();
        tweens.Clear();
        for (int i = objects.Count - 1; i >= 0; i--) if (objects[i] != null) Object.DestroyImmediate(objects[i]);
        objects.Clear();
        DOTween.ClearCachedTweens();
        yield return new ExitPlayMode();
    }

    [Test]
    public void RecycledButtonScaleCannotBecomeForeignTweenKilledOnDisable()
    {
        DOTween.defaultRecyclable = true;
        DOTween.defaultAutoKill = true;
        DOTween.defaultAutoPlay = AutoPlay.All;
        var button = CreateButton<TweenButton>();
        button.HoverPreset = Preset(TweenEffect.Scale);
        button.OnPointerEnter(new PointerEventData(null));
        var previous = Capture(button.CurrentTween);
        previous.Complete();
        var foreignTarget = Target("Foreign position target");
        var foreign = Capture(foreignTarget.DOMove(Vector3.one * 10, 10).Pause());
        bool exactReuse = ReferenceEquals(previous, foreign);

        button.enabled = false;

        Assert.That(foreign.IsActive(), Is.True, "Button killed an unrelated Vector3 tween; exact pooled reuse=" + exactReuse);
        Assert.That(exactReuse, Is.False, "Owned button handle must not enter the recyclable pool");
    }

    [Test]
    public void RecycledButtonClickCannotBecomeForeignSequenceKilledOnDisable()
    {
        DOTween.defaultRecyclable = true;
        DOTween.defaultAutoKill = true;
        DOTween.defaultAutoPlay = AutoPlay.All;
        var button = CreateButton<TweenButton>();
        button.ClickPreset = Preset(TweenEffect.Scale);
        button.OnSubmit(new BaseEventData(null));
        var previous = Capture(button.CurrentTween);
        previous.Complete();
        var foreignTarget = Target("Foreign sequence target");
        var foreign = Capture(DOTween.Sequence().Append(foreignTarget.DOMove(Vector3.one * 10, 10)).Pause());
        bool exactReuse = ReferenceEquals(previous, foreign);

        button.enabled = false;

        Assert.That(foreign.IsActive(), Is.True, "Button killed an unrelated Sequence; exact pooled reuse=" + exactReuse);
        Assert.That(exactReuse, Is.False, "Owned click sequence must not enter the recyclable pool");
    }

    [Test]
    public void PanelCompletionReleasesHandleWhenGlobalAutoKillIsDisabled()
    {
        DOTween.defaultAutoKill = false;
        var obj = new GameObject("Panel lifecycle target", typeof(RectTransform), typeof(CanvasGroup));
        objects.Add(obj);
        var panel = obj.AddComponent<TweenPanel>();
        panel.HidePreset = Preset(TweenEffect.Fade);
        panel.Hide();
        var previous = Capture(panel.CurrentTween);

        previous.Complete();

        Assert.That(previous.IsActive(), Is.False, "Completed panel sequence remained active after the component dropped its handle");
        Assert.That(panel.CurrentTween, Is.Null);
    }

    [Test]
    public void LegacyFeedbackCompletionReleasesHandlesWhenGlobalAutoKillIsDisabled()
    {
        DOTween.defaultAutoKill = false;
        var button = CreateButton<AnimatedButton>();
        button.PlayClickAnimation();
        var punch = Capture(button.CurrentTween);
        punch.Complete();
        typeof(AnimatedButton).GetField("_enableShakeAnimation", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(button, true);
        button.PlayShakeAnimation();
        var shake = Capture(button.CurrentShake);
        shake.Complete();

        Assert.That(!punch.IsActive() && !shake.IsActive(), Is.True,
            "Completed feedback remained active without an owner: Punch=" + punch.IsActive() + ", Shake=" + shake.IsActive());
        Assert.That(button.CurrentTween, Is.Null);
        Assert.That(button.CurrentShake, Is.Null);
    }

    [Test]
    public void ButtonStartsOwnedAnimationWhenGlobalAutoPlayIsDisabled()
    {
        DOTween.defaultAutoPlay = AutoPlay.None;
        var button = CreateButton<TweenButton>();
        button.HoverPreset = Preset(TweenEffect.Scale);

        button.OnPointerEnter(new PointerEventData(null));
        var animation = Capture(button.CurrentTween);

        Assert.That(animation.IsPlaying(), Is.True, "Button hover silently inherited AutoPlay.None");
    }

    [Test]
    public void PanelStartsOwnedAnimationWhenGlobalAutoPlayIsDisabled()
    {
        DOTween.defaultAutoPlay = AutoPlay.None;
        var obj = new GameObject("Panel autoplay target", typeof(RectTransform), typeof(CanvasGroup));
        objects.Add(obj);
        var panel = obj.AddComponent<TweenPanel>();
        panel.HidePreset = Preset(TweenEffect.Fade);

        panel.Hide();
        var animation = Capture(panel.CurrentTween);

        Assert.That(animation.IsPlaying(), Is.True, "Panel silently inherited AutoPlay.None");
    }

    private T CreateButton<T>() where T : TweenButton
    {
        var obj = new GameObject("Button lifecycle target", typeof(RectTransform), typeof(Button));
        objects.Add(obj);
        obj.SetActive(false);
        obj.transform.localScale = new Vector3(2, 3, 1);
        var button = obj.AddComponent<T>();
        obj.SetActive(true);

        return button;
    }

    private Transform Target(string name)
    {
        var obj = new GameObject(name);
        objects.Add(obj);

        return obj.transform;
    }

    private TweenPreset Preset(TweenEffect effect)
    {
        var preset = ScriptableObject.CreateInstance<TweenPreset>();
        objects.Add(preset);
        preset.Effect = effect;
        preset.Duration = 0.2f;
        preset.Vector = Vector3.one * 1.1f;
        preset.Ease = Ease.Linear;

        return preset;
    }

    private Tween Capture(Tween tween)
    {
        Assert.That(tween, Is.Not.Null);
        tweens.Add(tween);

        return tween;
    }
}
