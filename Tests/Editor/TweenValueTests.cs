using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using DG.Tweening;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class TweenValueTests
{
    private readonly List<Object> objects = new();
    private readonly List<Tween> tweens = new();
    private TweenPreset preset;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        yield return new EnterPlayMode();
        DOTween.Init(false, true, LogBehaviour.ErrorsOnly);
        preset = ScriptableObject.CreateInstance<TweenPreset>();
        preset.Duration = 1;
        preset.Delay = 0;
        preset.Ease = Ease.Linear;
        objects.Add(preset);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        foreach (var tween in tweens) if (tween != null && tween.IsActive()) tween.Kill();
        tweens.Clear();
        for (var i = objects.Count - 1; i >= 0; i--) if (objects[i]) Object.DestroyImmediate(objects[i]);
        objects.Clear();
        yield return new ExitPlayMode();
    }

    [Test]
    public void SliderRetargetsFromVisibleValueAndOnlyKillsItsOwnTween()
    {
        var component = SliderComponent();
        component.Target.maxValue = 10;
        component.SetImmediate(0);
        var first = Track(component.SetValue(10));
        first.Goto(0.4f);
        Assert.That(component.Target.value, Is.EqualTo(4).Within(0.001f));
        float unrelatedValue = 0;
        var unrelated = Track(DOTween.To(() => unrelatedValue, value => unrelatedValue = value, 10, 1).SetTarget(component));
        var replacement = Track(component.SetValue(8));
        Assert.That(first.IsActive(), Is.False);
        Assert.That(unrelated.IsActive(), Is.True);
        Assert.That(component.Target.value, Is.EqualTo(4).Within(0.001f));
        replacement.Goto(0.5f);
        Assert.That(component.Target.value, Is.EqualTo(6).Within(0.001f));
        Assert.That(preset.Duration, Is.EqualTo(1));
        Assert.That(preset.Ease, Is.EqualTo(Ease.Linear));
    }

    [Test]
    public void SliderClampsAndOnlyNotifiesWhenRequested()
    {
        var component = SliderComponent();
        component.Target.minValue = -2;
        component.Target.maxValue = 4;
        var notifications = 0;
        component.Target.onValueChanged.AddListener(_ => notifications++);
        component.SetImmediate(100);
        Assert.That(component.Target.value, Is.EqualTo(4));
        Assert.That(notifications, Is.Zero);
        component.NotifyValueChanged = true;
        component.SetImmediate(-100);
        Assert.That(component.Target.value, Is.EqualTo(-2));
        Assert.That(notifications, Is.EqualTo(1));
        var tween = Track(component.SetValue(100));
        tween.Goto(1);
        Assert.That(component.Target.value, Is.EqualTo(4));
        Assert.That(notifications, Is.GreaterThan(1));
    }

    [Test]
    public void DisableCancelsSliderAtDisplayedValueAndReuseStartsThere()
    {
        var component = SliderComponent();
        component.Target.maxValue = 10;
        component.SetImmediate(0);
        var tween = Track(component.SetValue(10));
        tween.Goto(0.3f);
        component.gameObject.SetActive(false);
        Assert.That(tween.IsActive(), Is.False);
        Assert.That(component.Target.value, Is.EqualTo(3).Within(0.001f));
        component.gameObject.SetActive(true);
        var next = Track(component.SetValue(7));
        next.Goto(0.5f);
        Assert.That(component.Target.value, Is.EqualTo(5).Within(0.001f));
    }

    [Test]
    public void FillDamageWaitsBeforeSecondaryAndRetargetsBothDisplays()
    {
        var component = FillComponent();
        component.SecondaryDelay = 0.5f;
        component.SetImmediate(1);
        var first = Track(component.SetValue(0.5f));
        first.Goto(0.25f);
        Assert.That(component.Target.fillAmount, Is.EqualTo(0.875f).Within(0.001f));
        Assert.That(component.SecondaryTarget.fillAmount, Is.EqualTo(1));
        var second = Track(component.SetValue(0.25f));
        Assert.That(first.IsActive(), Is.False);
        Assert.That(component.Target.fillAmount, Is.EqualTo(0.875f).Within(0.001f));
        Assert.That(component.SecondaryTarget.fillAmount, Is.EqualTo(1));
        second.Goto(0.75f);
        Assert.That(component.Target.fillAmount, Is.EqualTo(0.40625f).Within(0.001f));
        Assert.That(component.SecondaryTarget.fillAmount, Is.EqualTo(0.8125f).Within(0.001f));
        second.Goto(1.5f);
        Assert.That(component.Target.fillAmount, Is.EqualTo(0.25f).Within(0.001f));
        Assert.That(component.SecondaryTarget.fillAmount, Is.EqualTo(0.25f).Within(0.001f));
    }

    [Test]
    public void FillHealingAndReducedMotionKeepSecondaryConsistent()
    {
        var component = FillComponent();
        component.SetImmediate(0.2f);
        var tween = Track(component.SetValue(0.8f));
        Assert.That(component.Target.fillAmount, Is.EqualTo(0.2f).Within(0.001f));
        Assert.That(component.SecondaryTarget.fillAmount, Is.EqualTo(0.8f).Within(0.001f));
        tween.Goto(0.5f);
        Assert.That(component.Target.fillAmount, Is.EqualTo(0.5f).Within(0.001f));
        component.ReducedMotion = true;
        Assert.That(component.SetValue(10), Is.Null);
        Assert.That(tween.IsActive(), Is.False);
        Assert.That(component.Target.fillAmount, Is.EqualTo(1));
        Assert.That(component.SecondaryTarget.fillAmount, Is.EqualTo(1));
        component.SetImmediate(-10);
        Assert.That(component.Target.fillAmount, Is.Zero);
        Assert.That(component.SecondaryTarget.fillAmount, Is.Zero);
    }

    [Test]
    public void FillDisableCancelsDelayedSecondaryWithoutStaleCompletion()
    {
        var component = FillComponent();
        component.SetImmediate(1);
        var first = Track(component.SetValue(0.1f));
        first.Goto(0.1f);
        var displayed = component.Target.fillAmount;
        component.enabled = false;
        Assert.That(first.IsActive(), Is.False);
        component.enabled = true;
        component.SetImmediate(0.6f);
        Assert.That(component.Target.fillAmount, Is.EqualTo(0.6f).Within(0.001f));
        Assert.That(component.SecondaryTarget.fillAmount, Is.EqualTo(0.6f).Within(0.001f));
        Assert.That(displayed, Is.GreaterThan(0.6f));
    }

    [Test]
    public void CounterRetargetsAndFormatsWithInvariantCulture()
    {
        var component = CounterComponent();
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            component.NumberFormat = "N0";
            component.SetImmediate(1000);
            Assert.That(component.Target.text, Is.EqualTo("1,000"));
            var first = Track(component.SetValue(2000));
            first.Goto(0.5f);
            Assert.That(component.Value, Is.EqualTo(1500).Within(0.001f));
            Assert.That(component.Target.text, Is.EqualTo("1,500"));
            var second = Track(component.SetValue(2500));
            Assert.That(first.IsActive(), Is.False);
            Assert.That(component.Value, Is.EqualTo(1500).Within(0.001f));
            second.Goto(0.5f);
            Assert.That(component.Value, Is.EqualTo(2000).Within(0.001f));
            Assert.That(component.Target.text, Is.EqualTo("2,000"));
        }
        finally {CultureInfo.CurrentCulture = culture;}
    }

    [Test]
    public void InvalidCounterFormatIsRejectedBeforeChangingRunningValue()
    {
        var component = CounterComponent();
        component.SetImmediate(10);
        var first = Track(component.SetValue(20));
        first.Goto(0.5f);
        LogAssert.Expect(LogType.Error, new Regex("^TweenCounter .*numberFormat=Q$"));
        component.NumberFormat = "Q";
        Assert.That(component.NumberFormat, Is.EqualTo("0"));
        Assert.That(component.Value, Is.EqualTo(15).Within(0.001f));
        Assert.That(first.IsActive(), Is.True);
        first.Goto(0.75f);
        Assert.That(component.Target.text, Is.EqualTo("18"));
    }

    [Test]
    public void CounterDisableStopsCallbacksAndReducedMotionCompletesImmediately()
    {
        var component = CounterComponent();
        component.SetImmediate(0);
        float observed = -1;
        var callbacks = 0;
        component.OnValueChanged.AddListener(value => {observed = value; callbacks++;});
        var first = Track(component.SetValue(10));
        first.Goto(0.4f);
        var beforeDisable = callbacks;
        component.enabled = false;
        Assert.That(first.IsActive(), Is.False);
        Assert.That(callbacks, Is.EqualTo(beforeDisable));
        Assert.That(observed, Is.EqualTo(4).Within(0.001f));
        component.enabled = true;
        component.ReducedMotion = true;
        Assert.That(component.SetValue(9), Is.Null);
        Assert.That(observed, Is.EqualTo(9));
        Assert.That(component.Value, Is.EqualTo(9));
        Assert.That(component.Target.text, Is.EqualTo("9"));
    }

    [Test]
    public void InvalidPresetDoesNotCancelExistingSliderTween()
    {
        var component = SliderComponent();
        var first = Track(component.SetValue(1));
        first.Goto(0.25f);
        preset.Duration = -1;
        var displayed = component.Target.value;
        LogAssert.Expect(LogType.Error, new Regex("^TweenSlider .*preset "));
        Assert.That(component.SetValue(0), Is.Null);
        Assert.That(first.IsActive(), Is.True);
        Assert.That(component.Target.value, Is.EqualTo(displayed));
    }

    [Test]
    public void ImmediateInitializationWorksWithoutPresetAndStopCompletesOwnedTween()
    {
        var slider = SliderComponent();
        slider.Preset = null;
        slider.SetImmediate(0.25f);
        Assert.That(slider.Target.value, Is.EqualTo(0.25f));
        slider.Preset = preset;
        var tween = Track(slider.SetValue(0.8f));
        slider.Stop(true);
        Assert.That(tween.IsActive(), Is.False);
        Assert.That(slider.Target.value, Is.EqualTo(0.8f).Within(0.001f));
        var fill = FillComponent();
        fill.Preset = null;
        fill.SetImmediate(0.3f);
        Assert.That(fill.Target.fillAmount, Is.EqualTo(0.3f).Within(0.001f));
        var counter = CounterComponent();
        counter.Preset = null;
        counter.SetImmediate(123);
        Assert.That(counter.Value, Is.EqualTo(123));
        Assert.That(counter.Target.text, Is.EqualTo("123"));
    }

    [Test]
    public void MissingPresetsReportTheComponentWithoutChangingDisplay()
    {
        var slider = SliderComponent();
        slider.SetImmediate(0.3f);
        slider.Preset = null;
        LogAssert.Expect(LogType.Error, new Regex("^TweenSlider .*preset "));
        Assert.That(slider.SetValue(1), Is.Null);
        Assert.That(slider.Target.value, Is.EqualTo(0.3f).Within(0.001f));
        var fill = FillComponent();
        fill.SetImmediate(0.4f);
        fill.Preset = null;
        LogAssert.Expect(LogType.Error, new Regex("^TweenFill .*preset "));
        Assert.That(fill.SetValue(1), Is.Null);
        Assert.That(fill.Target.fillAmount, Is.EqualTo(0.4f).Within(0.001f));
        var counter = CounterComponent();
        counter.SetImmediate(7);
        counter.Preset = null;
        LogAssert.Expect(LogType.Error, new Regex("^TweenCounter .*preset "));
        Assert.That(counter.SetValue(10), Is.Null);
        Assert.That(counter.Value, Is.EqualTo(7));
    }

    [Test]
    public void NonFiniteValueDoesNotCancelCounterTween()
    {
        var component = CounterComponent();
        component.SetImmediate(4);
        var tween = Track(component.SetValue(10));
        tween.Goto(0.5f);
        LogAssert.Expect(LogType.Error, new Regex("^TweenCounter .*value=NaN$"));
        Assert.That(component.SetValue(float.NaN), Is.Null);
        Assert.That(tween.IsActive(), Is.True);
        Assert.That(component.Value, Is.EqualTo(7).Within(0.001f));
    }
    [TestCase("slider")]
    [TestCase("fill")]
    [TestCase("counter")]
    public void ToggleReducedMotionFinishesPendingValues(string kind)
    {
        if (kind == "slider")
        {
            var slider = SliderComponent();
            slider.SetImmediate(0);
            var sliderTween = Track(slider.SetValue(0.8f));
            sliderTween.Goto(0.5f);
            slider.ReducedMotion = true;
            Assert.That(slider.Target.value, Is.EqualTo(0.8f).Within(0.001f), "Enabling reduced motion must finish the running slider");
            Assert.That(sliderTween.IsActive(), Is.False);
        }
        else if (kind == "fill")
        {
            var fill = FillComponent();
            fill.SetImmediate(1);
            var fillTween = Track(fill.SetValue(0.2f));
            fillTween.Goto(0.5f);
            fill.ReducedMotion = true;
            Assert.That(fill.Target.fillAmount, Is.EqualTo(0.2f).Within(0.001f), "Enabling reduced motion must finish the primary fill");
            Assert.That(fill.SecondaryTarget.fillAmount, Is.EqualTo(0.2f).Within(0.001f), "Enabling reduced motion must finish the delayed secondary fill");
            Assert.That(fillTween.IsActive(), Is.False);
        }
        else
        {
            var counter = CounterComponent();
            counter.SetImmediate(10);
            var counterTween = Track(counter.SetValue(20));
            counterTween.Goto(0.5f);
            counter.ReducedMotion = true;
            Assert.That(counter.Value, Is.EqualTo(20).Within(0.001f), "Enabling reduced motion must finish the running counter");
            Assert.That(counter.Target.text, Is.EqualTo("20"));
            Assert.That(counterTween.IsActive(), Is.False);
        }
    }

    [Test]
    public void FillImageSimpleIsRejectedWithoutCancellingExistingTween()
    {
        var component = FillComponent();
        component.SetImmediate(1);
        var tween = Track(component.SetValue(0.2f));
        tween.Goto(0.25f);
        var displayed = component.Target.fillAmount;
        component.Target.type = Image.Type.Simple;
        LogAssert.Expect(LogType.Error, new Regex("^TweenFill .*target.type=Simple"));
        Assert.That(component.SetValue(0.5f), Is.Null);
        Assert.That(tween.IsActive(), Is.True);
        Assert.That(component.Target.fillAmount, Is.EqualTo(displayed));
    }
    private TweenSlider SliderComponent()
    {
        var gameObject = Create("slider", typeof(RectTransform), typeof(Slider), typeof(TweenSlider));
        var component = gameObject.GetComponent<TweenSlider>();
        component.Target = gameObject.GetComponent<Slider>();
        component.Preset = preset;

        return component;
    }

    private TweenFill FillComponent()
    {
        var gameObject = Create("fill", typeof(RectTransform), typeof(Image), typeof(TweenFill));
        var component = gameObject.GetComponent<TweenFill>();
        component.Target = gameObject.GetComponent<Image>();
        component.Target.type = Image.Type.Filled;
        component.SecondaryTarget = Create("secondary", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        component.SecondaryTarget.type = Image.Type.Filled;
        component.Preset = preset;

        return component;
    }

    private TweenCounter CounterComponent()
    {
        var gameObject = Create("counter", typeof(RectTransform), typeof(Text), typeof(TweenCounter));
        var component = gameObject.GetComponent<TweenCounter>();
        component.Target = gameObject.GetComponent<Text>();
        component.Preset = preset;

        return component;
    }

    private GameObject Create(string name, params System.Type[] components)
    {
        var gameObject = new GameObject(name, components);
        objects.Add(gameObject);

        return gameObject;
    }

    private Tween Track(Tween tween)
    {
        Assert.That(tween, Is.Not.Null);
        tween.Pause();
        tweens.Add(tween);

        return tween;
    }
}
