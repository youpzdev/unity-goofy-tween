using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using DG.Tweening;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class AnimatedUITests
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
    public void AllLegacyPanelStylesPreserveBaselineAndFinishHidden()
    {
        foreach (AnimatedPanel.PanelAnimationType style in Enum.GetValues(typeof(AnimatedPanel.PanelAnimationType)))
        {
            var panel = CreatePanel();
            panel.AnimationType = style;
            var rect = (RectTransform)panel.transform;
            Vector3 scale = rect.localScale;
            Vector2 position = rect.anchoredPosition;
            Quaternion rotation = rect.localRotation;
            panel.Show();
            Assert.That(panel.IsTransitioning, Is.True, style.ToString());
            Assert.That(panel.Group.alpha, Is.Zero, style.ToString());
            if (style == AnimatedPanel.PanelAnimationType.FlipHorizontal || style == AnimatedPanel.PanelAnimationType.FlipVertical || style == AnimatedPanel.PanelAnimationType.RotateFadeIn || style == AnimatedPanel.PanelAnimationType.SwingIn)
                Assert.That(Quaternion.Angle(rect.localRotation, rotation), Is.GreaterThan(1), style.ToString());
            if (style == AnimatedPanel.PanelAnimationType.SlideFromBottom || style == AnimatedPanel.PanelAnimationType.SlideFromTop || style == AnimatedPanel.PanelAnimationType.SlideFromLeft || style == AnimatedPanel.PanelAnimationType.SlideFromRight)
                Assert.That(Vector2.Distance(rect.anchoredPosition, position), Is.GreaterThan(1), style.ToString());
            panel.CurrentTween.Complete();
            Assert.That(rect.localScale, Is.EqualTo(scale), style.ToString());
            Assert.That(rect.anchoredPosition, Is.EqualTo(position), style.ToString());
            Assert.That(Quaternion.Angle(rect.localRotation, rotation), Is.LessThan(0.01f), style.ToString());
            Assert.That(panel.Group.interactable && panel.Group.blocksRaycasts, Is.True, style.ToString());
            panel.Hide();
            Assert.That(panel.gameObject.activeSelf, Is.True, style.ToString());
            panel.CurrentTween.Complete();
            Assert.That(panel.gameObject.activeSelf, Is.False, style.ToString());
            Assert.That(AnimatedPanel.CountOpen, Is.Zero, style.ToString());
        }
    }

    [Test]
    public void PanelStackDoesNotDuplicateAndBackOnlyClosesTopmostOnce()
    {
        var first = CreatePanel();
        var second = CreatePanel();
        first.ReducedMotion = true;
        second.ReducedMotion = true;
        int firstBack = 0;
        int secondBack = 0;
        first.OnBackPressed.AddListener(() => firstBack++);
        second.OnBackPressed.AddListener(() => {secondBack++; Assert.That(second.Back(), Is.False);});
        first.Show();
        second.Show();
        second.Show();
        Assert.That(AnimatedPanel.CountOpen, Is.EqualTo(2));
        Assert.That(AnimatedPanel.Topmost, Is.SameAs(second));
        Assert.That(first.Back(), Is.False);
        Assert.That(AnimatedPanel.BackTopmost(), Is.True);
        Assert.That(secondBack, Is.EqualTo(1));
        Assert.That(firstBack, Is.Zero);
        Assert.That(second.gameObject.activeSelf, Is.False);
        Assert.That(AnimatedPanel.Topmost, Is.SameAs(first));
        Assert.That(AnimatedPanel.BackTopmost(), Is.True);
        Assert.That(firstBack, Is.EqualTo(1));
        Assert.That(AnimatedPanel.CountOpen, Is.Zero);
    }

    [Test]
    public void PanelReopenInterruptsHideWithoutStaleDeactivation()
    {
        var panel = CreatePanel();
        int hidden = 0;
        panel.OnHidden.AddListener(() => hidden++);
        panel.Show();
        panel.CurrentTween.Complete();
        panel.Hide();
        var hiding = panel.CurrentTween;
        hiding.Goto(0.1f, true);
        float interruptedAlpha = panel.Group.alpha;
        panel.Show();
        Assert.That(hiding.IsActive(), Is.False);
        Assert.That(panel.Group.alpha, Is.EqualTo(interruptedAlpha));
        panel.CurrentTween.Complete();

        Assert.That(panel.gameObject.activeSelf, Is.True);
        Assert.That(panel.Group.alpha, Is.EqualTo(1));
        Assert.That(hidden, Is.Zero);
        Assert.That(AnimatedPanel.CountOpen, Is.EqualTo(1));
    }

    [Test]
    public void ReopenFromHiddenCompletionIsNotDeactivatedByOldCallback()
    {
        var panel = CreatePanel();
        panel.Show();
        panel.CurrentTween.Complete();
        panel.OnHidden.AddListener(panel.Show);
        panel.Hide();
        panel.CurrentTween.Complete();
        Assert.That(panel.gameObject.activeSelf, Is.True);
        Assert.That(panel.IsShown, Is.True);
        panel.CurrentTween.Complete();

        Assert.That(panel.Group.alpha, Is.EqualTo(1));
        Assert.That(AnimatedPanel.Topmost, Is.SameAs(panel));
    }

    [Test]
    public void DisabledDestroyedAndResetPanelsLeaveNoStaleStackEntries()
    {
        var first = CreatePanel();
        var second = CreatePanel();
        first.Show();
        second.Show();
        second.enabled = false;
        Assert.That(AnimatedPanel.Topmost, Is.SameAs(first));
        Object.DestroyImmediate(first.gameObject);
        Assert.That(AnimatedPanel.CountOpen, Is.Zero);
        second.enabled = true;
        second.Show();
        var reset = typeof(AnimatedPanel).GetMethod("ResetStack", BindingFlags.NonPublic | BindingFlags.Static);
        var attribute = (RuntimeInitializeOnLoadMethodAttribute)Attribute.GetCustomAttribute(reset, typeof(RuntimeInitializeOnLoadMethodAttribute));
        Assert.That(attribute.loadType, Is.EqualTo(RuntimeInitializeLoadType.SubsystemRegistration));
        reset.Invoke(null, null);
        Assert.That(AnimatedPanel.CountOpen, Is.Zero);
        second.Show();

        Assert.That(AnimatedPanel.CountOpen, Is.EqualTo(1));
    }

    [Test]
    public void UIPanelLegacyEnumMapsIntoAnimatedPanelEngine()
    {
        var obj = PanelObject();
        var panel = obj.AddComponent<UIPanel>();
        panel.AnimationType = UIPanel.PanelAnimationType.FlipVertical;
        panel.Duration = 0.2f;
        Assert.That(((AnimatedPanel)panel).AnimationType, Is.EqualTo(AnimatedPanel.PanelAnimationType.FlipVertical));
        panel.Show();
        panel.CurrentTween.Complete();
        panel.Hide();
        panel.CurrentTween.Complete();

        Assert.That(obj.activeSelf, Is.False);
        Assert.That(AnimatedPanel.CountOpen, Is.Zero);
    }

    [Test]
    public void AnimatedButtonLegacyFallbackHandlesPointerAndSubmitWithNonUnitScale()
    {
        var button = CreateButton();
        Vector3 baseline = button.transform.localScale;
        var pointer = new PointerEventData(null) {button = PointerEventData.InputButton.Left};
        ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
        button.CurrentTween.Complete();
        Assert.That(button.transform.localScale, Is.EqualTo(baseline * 1.06f));
        ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
        button.CurrentTween.Complete();
        Assert.That(button.transform.localScale, Is.EqualTo(baseline * 0.92f));
        ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        button.CurrentTween.Complete();
        Assert.That(button.transform.localScale, Is.EqualTo(baseline * 1.06f));
        ExecuteEvents.Execute(button.gameObject, new BaseEventData(null), ExecuteEvents.submitHandler);
        button.CurrentTween.Complete();
        Assert.That(button.transform.localScale, Is.EqualTo(baseline * 1.06f));
        button.enabled = false;

        Assert.That(button.transform.localScale, Is.EqualTo(baseline));
    }

    [Test]
    public void ButtonPunchShakeUseOwnChannelsAndSharedPresetsRemainUnchanged()
    {
        var button = CreateButton();
        button.PunchPreset = Preset(TweenEffect.PunchScale, new Vector3(0.1f, 0.15f, 0));
        button.ShakePreset = Preset(TweenEffect.ShakePosition, new Vector3(5, 0, 0));
        Vector3 scale = button.transform.localScale;
        Vector3 position = button.transform.localPosition;
        var foreign = button.transform.DOLocalRotate(new Vector3(0, 0, 100), 10).Pause();
        try
        {
            button.PlayClickAnimation();
            var punch = button.CurrentTween;
            button.PlayShakeAnimation();
            Assert.That(punch.IsActive(), Is.True);
            Assert.That(button.CurrentShake.IsActive(), Is.True);
            button.CurrentTween.Complete();
            button.CurrentShake.Complete();
            Assert.That(button.transform.localScale, Is.EqualTo(scale));
            Assert.That(button.transform.localPosition, Is.EqualTo(position));
            button.PlayClickAnimation();
            button.PlayShakeAnimation();
            button.enabled = false;
            Assert.That(button.transform.localScale, Is.EqualTo(scale));
            Assert.That(button.transform.localPosition, Is.EqualTo(position));
            Assert.That(foreign.IsActive(), Is.True);
            Assert.That(button.PunchPreset.Vector, Is.EqualTo(new Vector3(0.1f, 0.15f, 0)));
            Assert.That(button.ShakePreset.Vector, Is.EqualTo(new Vector3(5, 0, 0)));
            Assert.That(button.PunchPreset.Loops, Is.EqualTo(1));
        }
        finally {foreign.Kill();}
    }

    [Test]
    public void DisabledAndReducedMotionButtonsDoNotStartFeedback()
    {
        var button = CreateButton();
        button.ShakePreset = Preset(TweenEffect.ShakePosition, new Vector3(5, 0, 0));
        button.GetComponent<Selectable>().interactable = false;
        button.PlayClickAnimation();
        button.PlayShakeAnimation();
        Assert.That(button.CurrentTween, Is.Null);
        Assert.That(button.CurrentShake, Is.Null);
        button.GetComponent<Selectable>().interactable = true;
        button.PlayShakeAnimation();
        var shake = button.CurrentShake;
        button.ReducedMotion = true;
        Assert.That(shake.IsActive(), Is.False);
        button.PlayClickAnimation();
        button.PlayShakeAnimation();

        Assert.That(button.CurrentTween, Is.Null);
        Assert.That(button.CurrentShake, Is.Null);
    }

    [Test]
    public void InvalidHidePresetKeepsShownPanelInNavigationStack()
    {
        var panel = CreatePanel();
        panel.Show();
        panel.CurrentTween.Complete();
        var invalid = Preset(TweenEffect.Fade, Vector3.one);
        invalid.Duration = -1;
        panel.HidePreset = invalid;
        LogAssert.Expect(LogType.Error, new Regex("Duration"));

        panel.Hide();

        Assert.That(panel.IsShown, Is.True);
        Assert.That(panel.gameObject.activeSelf, Is.True);
        Assert.That(AnimatedPanel.Topmost, Is.SameAs(panel));
        Assert.That(AnimatedPanel.CountOpen, Is.EqualTo(1));
    }

    [Test]
    public void InvalidShowPresetLeavesInactivePanelInactive()
    {
        var panel = CreatePanel();
        var invalid = Preset(TweenEffect.Fade, Vector3.one);
        invalid.Duration = -1;
        panel.ShowPreset = invalid;
        LogAssert.Expect(LogType.Error, new Regex("Duration"));

        panel.Show();

        Assert.That(panel.gameObject.activeSelf, Is.False);
        Assert.That(panel.IsShown, Is.False);
        Assert.That(AnimatedPanel.CountOpen, Is.Zero);
    }

    [Test]
    public void ScaleKillCallbackKeepsLatestReentrantButtonRequest()
    {
        var button = CreateButton();
        button.HoverPreset = Preset(TweenEffect.Scale, Vector3.one * 1.1f);
        button.PressPreset = Preset(TweenEffect.Scale, Vector3.one * 0.9f);
        var pointer = new PointerEventData(null) {button = PointerEventData.InputButton.Left};
        button.OnPointerEnter(pointer);
        var previous = button.CurrentTween;
        previous.Goto(0.1f, true);
        Tween replacement = null;
        bool replacing = false;
        previous.OnKill(() =>
        {
            if (replacing) return;
            replacing = true;
            button.OnPointerDown(pointer);
            replacement = button.CurrentTween;
        });

        button.OnPointerExit(pointer);

        Assert.That(replacement, Is.Not.Null);
        Assert.That(replacement.IsActive(), Is.True);
        Assert.That(button.CurrentTween, Is.SameAs(replacement), "Outer request replaced the newer OnKill request");
    }

    [Test]
    public void PanelKillCallbackKeepsLatestReentrantShowRequest()
    {
        var panel = CreatePanel();
        panel.Show();
        panel.CurrentTween.Complete();
        panel.Hide();
        var previous = panel.CurrentTween;
        previous.Goto(0.1f, true);
        Tween replacement = null;
        bool replacing = false;
        previous.OnKill(() =>
        {
            if (replacing) return;
            replacing = true;
            panel.Show();
            replacement = panel.CurrentTween;
        });

        panel.Hide();

        Assert.That(replacement, Is.Not.Null);
        Assert.That(replacement.IsActive(), Is.True);
        Assert.That(panel.CurrentTween, Is.SameAs(replacement), "Outer Hide replaced the newer OnKill Show");
        Assert.That(panel.IsShown, Is.True);
        Assert.That(AnimatedPanel.Topmost, Is.SameAs(panel));
    }

    [Test]
    public void ShakeKillCallbackKeepsLatestReentrantShakeRequest()
    {
        var button = CreateButton();
        button.ShakePreset = Preset(TweenEffect.ShakePosition, new Vector3(5, 0, 0));
        button.PlayShakeAnimation();
        var previous = button.CurrentShake;
        previous.Goto(0.1f, true);
        Tween replacement = null;
        bool replacing = false;
        previous.OnKill(() =>
        {
            if (replacing) return;
            replacing = true;
            button.PlayShakeAnimation();
            replacement = button.CurrentShake;
        });

        button.PlayShakeAnimation();

        Assert.That(replacement, Is.Not.Null);
        Assert.That(replacement.IsActive(), Is.True);
        Assert.That(button.CurrentShake, Is.SameAs(replacement), "Outer request replaced the newer OnKill shake");
    }

    private GameObject PanelObject()
    {
        var obj = new GameObject("Animated panel test", typeof(RectTransform), typeof(CanvasGroup));
        objects.Add(obj);
        obj.SetActive(false);
        var rect = (RectTransform)obj.transform;
        rect.localScale = new Vector3(2, 3, 1);
        rect.anchoredPosition = new Vector2(30, 40);
        rect.localRotation = Quaternion.Euler(13, 17, 21);

        return obj;
    }

    private AnimatedPanel CreatePanel()
    {
        var panel = PanelObject().AddComponent<AnimatedPanel>();
        panel.Duration = 0.2f;

        return panel;
    }

    private AnimatedButton CreateButton()
    {
        var obj = new GameObject("Animated button test", typeof(RectTransform), typeof(Button));
        objects.Add(obj);
        obj.SetActive(false);
        obj.transform.localScale = new Vector3(2, 3, 1);
        var button = obj.AddComponent<AnimatedButton>();
        obj.SetActive(true);

        return button;
    }

    private TweenPreset Preset(TweenEffect effect, Vector3 vector)
    {
        var preset = ScriptableObject.CreateInstance<TweenPreset>();
        objects.Add(preset);
        preset.Duration = 0.2f;
        preset.Effect = effect;
        preset.Vector = vector;
        preset.Ease = Ease.Linear;

        return preset;
    }
}
