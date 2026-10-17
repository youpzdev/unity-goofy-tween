using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using DG.Tweening;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.TestTools;

public class TweenUITests
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
    public void PanelHideShowInterruptionContinuesFromCurrentState()
    {
        var panel = CreatePanel();
        var preset = Preset(TweenEffect.Scale, new Vector3(0.8f, 0.8f, 1));
        panel.ShowPreset = preset;
        panel.HidePreset = preset;
        var rect = (RectTransform)panel.transform;
        var group = panel.GetComponent<CanvasGroup>();
        Vector3 baseline = rect.localScale;
        int hiddenCalls = 0;
        int shownCalls = 0;
        panel.OnHidden.AddListener(() => hiddenCalls++);
        panel.OnShown.AddListener(() => shownCalls++);

        panel.Hide();
        panel.CurrentTween.Goto(0.1f, true);
        Vector3 interruptedScale = rect.localScale;
        float interruptedAlpha = group.alpha;
        Assert.That(interruptedAlpha, Is.InRange(0.01f, 0.99f));
        panel.Show();
        Assert.That(rect.localScale, Is.EqualTo(interruptedScale));
        Assert.That(group.alpha, Is.EqualTo(interruptedAlpha));
        panel.CurrentTween.Goto(0.05f, true);
        panel.Hide();
        panel.CurrentTween.Goto(0.05f, true);
        panel.Show();
        panel.CurrentTween.Complete();

        Assert.That(rect.localScale, Is.EqualTo(baseline));
        Assert.That(group.alpha, Is.EqualTo(1));
        Assert.That(group.interactable && group.blocksRaycasts, Is.True);
        Assert.That(panel.IsShown, Is.True);
        Assert.That(panel.IsTransitioning, Is.False);
        Assert.That(hiddenCalls, Is.Zero);
        Assert.That(shownCalls, Is.EqualTo(1));
        Assert.That(preset.Vector, Is.EqualTo(new Vector3(0.8f, 0.8f, 1)));
        Assert.That(preset.Duration, Is.EqualTo(0.2f));
    }

    [Test]
    public void PanelHideBlocksInteractionAndDisableRestoresBaselineWithoutKillingForeignTween()
    {
        var panel = CreatePanel();
        panel.HidePreset = Preset(TweenEffect.Move, new Vector3(0, -90, 0));
        var rect = (RectTransform)panel.transform;
        var group = panel.GetComponent<CanvasGroup>();
        Vector2 position = rect.anchoredPosition;
        Vector3 scale = rect.localScale;
        var foreign = rect.DOMove(Vector3.one * 100, 10).Pause();
        try
        {
            panel.Hide();
            Assert.That(group.interactable || group.blocksRaycasts, Is.False);
            panel.CurrentTween.Complete();
            Assert.That(group.alpha, Is.Zero);
            Assert.That(rect.anchoredPosition, Is.EqualTo(position + new Vector2(0, -90)));
            panel.enabled = false;

            Assert.That(rect.anchoredPosition, Is.EqualTo(position));
            Assert.That(rect.localScale, Is.EqualTo(scale));
            Assert.That(group.alpha, Is.EqualTo(1));
            Assert.That(group.interactable && group.blocksRaycasts, Is.True);
            Assert.That(foreign.IsActive(), Is.True);
        }
        finally {foreign.Kill();}
    }

    [Test]
    public void PanelReducedMotionAppliesFinalAndCompletesCurrentTransition()
    {
        var panel = CreatePanel();
        panel.ShowPreset = Preset(TweenEffect.Fade, Vector3.one);
        panel.HidePreset = panel.ShowPreset;
        var group = panel.GetComponent<CanvasGroup>();
        panel.Hide();
        panel.ReducedMotion = true;
        Assert.That(group.alpha, Is.Zero);
        Assert.That(panel.IsTransitioning, Is.False);
        panel.Show();

        Assert.That(group.alpha, Is.EqualTo(1));
        Assert.That(panel.IsTransitioning, Is.False);
        Assert.That(group.interactable, Is.True);
    }

    [Test]
    public void ButtonPointerAndKeyboardUseOneScaleAnimationAndPreserveNonUnitBaseline()
    {
        var button = CreateButton();
        button.HoverPreset = Preset(TweenEffect.Scale, Vector3.one * 1.1f);
        button.PressPreset = Preset(TweenEffect.Scale, Vector3.one * 0.9f);
        button.ClickPreset = Preset(TweenEffect.Scale, Vector3.one * 1.2f);
        Vector3 baseline = button.transform.localScale;
        var pointer = new PointerEventData(null) {button = PointerEventData.InputButton.Left};
        var keyboard = new BaseEventData(null);

        ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
        button.CurrentTween.Complete();
        Assert.That(button.transform.localScale, Is.EqualTo(baseline * 1.1f));
        ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
        button.CurrentTween.Complete();
        Assert.That(button.transform.localScale, Is.EqualTo(baseline * 0.9f));
        ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        button.CurrentTween.Complete();
        Assert.That(button.transform.localScale, Is.EqualTo(baseline * 1.1f));
        ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerExitHandler);
        ExecuteEvents.Execute(button.gameObject, keyboard, ExecuteEvents.selectHandler);
        ExecuteEvents.Execute(button.gameObject, keyboard, ExecuteEvents.submitHandler);
        button.CurrentTween.Complete();
        Assert.That(button.transform.localScale, Is.EqualTo(baseline * 1.1f));
        ExecuteEvents.Execute(button.gameObject, keyboard, ExecuteEvents.deselectHandler);

        Assert.That(button.transform.localScale, Is.EqualTo(baseline));
        Assert.That(button.HoverPreset.Vector, Is.EqualTo(Vector3.one * 1.1f));
    }

    [Test]
    public void DisabledButtonIgnoresAnimationAndDisablePreservesForeignTween()
    {
        var button = CreateButton();
        button.HoverPreset = Preset(TweenEffect.Scale, Vector3.one * 1.1f);
        button.ClickPreset = Preset(TweenEffect.Scale, Vector3.one * 1.2f);
        Vector3 baseline = button.transform.localScale;
        var pointer = new PointerEventData(null) {button = PointerEventData.InputButton.Left};
        var selectable = button.GetComponent<Selectable>();
        var foreign = button.transform.DOMove(Vector3.one * 100, 10).Pause();
        try
        {
            button.OnPointerEnter(pointer);
            button.CurrentTween.Goto(0.1f, true);
            selectable.interactable = false;
            button.OnPointerClick(pointer);
            button.OnSubmit(new BaseEventData(null));
            Assert.That(button.CurrentTween, Is.Null);
            Assert.That(button.transform.localScale, Is.EqualTo(baseline));
            selectable.interactable = true;
            button.OnPointerEnter(pointer);
            button.CurrentTween.Goto(0.1f, true);
            button.enabled = false;

            Assert.That(button.transform.localScale, Is.EqualTo(baseline));
            Assert.That(button.CurrentTween, Is.Null);
            Assert.That(foreign.IsActive(), Is.True);
        }
        finally {foreign.Kill();}
    }

    [Test]
    public void ReducedMotionButtonChangesStateWithoutTween()
    {
        var button = CreateButton();
        button.HoverPreset = Preset(TweenEffect.Scale, Vector3.one * 1.1f);
        Vector3 baseline = button.transform.localScale;
        button.ReducedMotion = true;
        button.OnSelect(new BaseEventData(null));

        Assert.That(button.transform.localScale, Is.EqualTo(baseline * 1.1f));
        Assert.That(button.CurrentTween, Is.Null);
        button.OnDeselect(new BaseEventData(null));
        Assert.That(button.transform.localScale, Is.EqualTo(baseline));
    }

    [Test]
    public void ShowingInitiallyHiddenPanelUsesShowPresetAndStableBaseline()
    {
        var obj = new GameObject("Initially hidden tween panel", typeof(RectTransform), typeof(CanvasGroup));
        objects.Add(obj);
        obj.SetActive(false);
        obj.transform.localScale = new Vector3(2, 3, 1);
        obj.GetComponent<CanvasGroup>().alpha = 0;
        var panel = obj.AddComponent<TweenPanel>();
        panel.ShowPreset = Preset(TweenEffect.Scale, Vector3.one * 0.8f);
        obj.SetActive(true);

        panel.Show();
        Assert.That(obj.transform.localScale, Is.EqualTo(new Vector3(2, 3, 1) * 0.8f));
        panel.CurrentTween.Complete();

        Assert.That(obj.transform.localScale, Is.EqualTo(new Vector3(2, 3, 1)));
        Assert.That(obj.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1));
    }

    [Test]
    public void InvalidHoverPresetDoesNotCancelCurrentValidAnimation()
    {
        var button = CreateButton();
        button.HoverPreset = Preset(TweenEffect.Scale, Vector3.one * 1.1f);
        button.OnPointerEnter(new PointerEventData(null));
        var running = button.CurrentTween;
        running.Goto(0.1f, true);
        Vector3 currentScale = button.transform.localScale;
        var invalid = Preset(TweenEffect.Scale, Vector3.one * 1.2f);
        invalid.Duration = -1;
        button.HoverPreset = invalid;
        LogAssert.Expect(LogType.Error, new Regex("Duration=-1"));

        button.OnSelect(new BaseEventData(null));

        Assert.That(running.IsActive(), Is.True, "Invalid replacement killed the valid hover tween");
        Assert.That(button.CurrentTween, Is.SameAs(running));
        Assert.That(button.transform.localScale, Is.EqualTo(currentScale));
    }

    private TweenPanel CreatePanel()
    {
        var obj = new GameObject("Tween panel test", typeof(RectTransform), typeof(CanvasGroup));
        objects.Add(obj);
        obj.SetActive(false);
        var rect = (RectTransform)obj.transform;
        rect.localScale = new Vector3(2, 3, 1);
        rect.anchoredPosition = new Vector2(30, 40);
        var panel = obj.AddComponent<TweenPanel>();
        obj.SetActive(true);

        return panel;
    }

    private TweenButton CreateButton()
    {
        var obj = new GameObject("Tween button test", typeof(RectTransform), typeof(Button));
        objects.Add(obj);
        obj.SetActive(false);
        obj.transform.localScale = new Vector3(2, 3, 1);
        var button = obj.AddComponent<TweenButton>();
        obj.SetActive(true);

        return button;
    }

    private TweenPreset Preset(TweenEffect effect, Vector3 vector)
    {
        var preset = ScriptableObject.CreateInstance<TweenPreset>();
        objects.Add(preset);
        preset.Duration = 0.2f;
        preset.Ease = Ease.Linear;
        preset.Effect = effect;
        preset.Vector = vector;
        preset.Relative = true;

        return preset;
    }
}
