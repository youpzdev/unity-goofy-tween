using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using DG.Tweening;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class InternalEaseUITests
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

    [TestCase(Ease.INTERNAL_Custom)]
    [TestCase(Ease.INTERNAL_Zero)]
    public void InternalButtonEaseIsRejectedBeforeReplacingValidHover(Ease internalEase)
    {
        var obj = new GameObject("Internal ease button", typeof(RectTransform), typeof(Button));
        objects.Add(obj);
        obj.SetActive(false);
        obj.transform.localScale = new Vector3(2, 3, 1);
        var button = obj.AddComponent<TweenButton>();
        obj.SetActive(true);
        button.HoverPreset = Preset(TweenEffect.Scale, Ease.Linear);
        button.OnPointerEnter(new PointerEventData(null));
        var running = button.CurrentTween;
        running.Goto(0.1f, true);
        Vector3 scale = button.transform.localScale;
        button.HoverPreset = Preset(TweenEffect.Scale, internalEase);
        LogAssert.Expect(LogType.Error, new Regex("Ease"));

        button.OnSelect(new BaseEventData(null));

        Assert.That(running.IsActive(), Is.True, "Internal ease cancelled valid hover: " + internalEase);
        Assert.That(button.CurrentTween, Is.SameAs(running));
        Assert.That(button.transform.localScale, Is.EqualTo(scale));
    }

    [TestCase(Ease.INTERNAL_Custom)]
    [TestCase(Ease.INTERNAL_Zero)]
    public void InternalPanelEaseIsRejectedBeforeReplacingValidTransition(Ease internalEase)
    {
        var obj = new GameObject("Internal ease panel", typeof(RectTransform), typeof(CanvasGroup));
        objects.Add(obj);
        var panel = obj.AddComponent<TweenPanel>();
        panel.HidePreset = Preset(TweenEffect.Fade, Ease.Linear);
        panel.Hide();
        var running = panel.CurrentTween;
        running.Goto(0.1f, true);
        float alpha = panel.Group.alpha;
        Vector3 scale = panel.Content.localScale;
        panel.ShowPreset = Preset(TweenEffect.Fade, internalEase);
        LogAssert.Expect(LogType.Error, new Regex("Ease"));

        panel.Show();

        Assert.That(running.IsActive(), Is.True, "Internal ease cancelled valid transition: " + internalEase);
        Assert.That(panel.CurrentTween, Is.SameAs(running));
        Assert.That(panel.Group.alpha, Is.EqualTo(alpha));
        Assert.That(panel.Content.localScale, Is.EqualTo(scale));
        Assert.That(panel.IsShown, Is.False);
    }

    private TweenPreset Preset(TweenEffect effect, Ease ease)
    {
        var preset = ScriptableObject.CreateInstance<TweenPreset>();
        objects.Add(preset);
        preset.Duration = 0.2f;
        preset.Effect = effect;
        preset.Vector = Vector3.one * 1.1f;
        preset.Ease = ease;

        return preset;
    }
}
