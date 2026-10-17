using DG.Tweening;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TweenDemoGeometryTests
{
    private const string DemoPath = "Assets/youpzdev/Goofy-Tween/Demo/Scenes/GoofyTweenDemo.unity";
    private const float Tolerance = 0.05f;

    [TestCase(344)]
    [TestCase(684)]
    public void CompletedDemoSequenceStaysInsideStageAndClearOfItsCaption(float width)
    {
        var scene = SceneManager.GetSceneByPath(DemoPath);
        var previousActive = SceneManager.GetActiveScene();
        Scene temporary = default;
        bool opened = !scene.IsValid() || !scene.isLoaded;
        GameObject clone = null;
        TweenSequence sequence = null;
        try
        {
            if (opened) scene = EditorSceneManager.OpenScene(DemoPath, OpenSceneMode.Additive);
            RectTransform source = null;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var rect in root.GetComponentsInChildren<RectTransform>(true))
                    if (rect.name == "SequenceStage") source = rect;
            Assert.That(source, Is.Not.Null, $"SequenceStage is missing in saved demo {DemoPath}");
            temporary = EditorSceneManager.NewPreviewScene();
            clone = (GameObject)Object.Instantiate(source.gameObject, temporary);
            clone.name = "TweenDemoGeometry_SequenceStage";
            var stage = clone.GetComponent<RectTransform>();
            stage.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            stage.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, source.rect.height);
            sequence = clone.GetComponentInChildren<TweenSequence>(true);
            Assert.That(sequence, Is.Not.Null, "Saved SequenceStage has no TweenSequence");
            foreach (var entry in sequence.Entries)
                Assert.That(entry.Target.IsChildOf(stage), Is.True, "Cloned sequence entry still targets the original scene");
            RectTransform caption = null;
            foreach (var rect in clone.GetComponentsInChildren<RectTransform>(true))
                if (rect.name == "Steps") caption = rect;
            Assert.That(caption, Is.Not.Null, "Saved SequenceStage has no Steps caption");
            var target = (RectTransform)sequence.Entries[0].Target;
            float initialX = target.localPosition.x;

            sequence.Play().Complete(true);

            Assert.That(target.localPosition.x, Is.EqualTo(initialX + 120).Within(Tolerance), "Completed sequence did not apply the saved MoveRight preset");
            Assert.That(Mathf.DeltaAngle(90, target.localEulerAngles.z), Is.EqualTo(0).Within(Tolerance), "Completed sequence did not apply the saved RotateQuarter preset");
            Assert.That(target.localScale.x, Is.EqualTo(1.18f).Within(Tolerance), "Completed sequence did not apply SequenceScale on X");
            Assert.That(target.localScale.y, Is.EqualTo(1.18f).Within(Tolerance), "Completed sequence did not apply SequenceScale on Y");
            Rect bounds = BoundsIn(stage, target);
            Rect captionBounds = BoundsIn(stage, caption);
            Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(stage.rect.xMin - Tolerance), $"Stage width={width}, target left={bounds.xMin}, stage left={stage.rect.xMin}");
            Assert.That(bounds.xMax, Is.LessThanOrEqualTo(stage.rect.xMax + Tolerance), $"Stage width={width}, target right={bounds.xMax}, stage right={stage.rect.xMax}");
            Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(stage.rect.yMin - Tolerance), $"Stage width={width}, target bottom={bounds.yMin}, stage bottom={stage.rect.yMin}");
            Assert.That(bounds.yMax, Is.LessThanOrEqualTo(stage.rect.yMax + Tolerance), $"Stage width={width}, target top={bounds.yMax}, stage top={stage.rect.yMax}");
            Assert.That(bounds.Overlaps(captionBounds), Is.False, $"Stage width={width}, animated bounds={bounds} overlap Steps bounds={captionBounds}");
        }
        finally
        {
            if (sequence != null) sequence.Stop();
            if (clone != null) Object.DestroyImmediate(clone);
            if (temporary.IsValid()) EditorSceneManager.ClosePreviewScene(temporary);
            if (opened && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            if (previousActive.IsValid() && previousActive.isLoaded) SceneManager.SetActiveScene(previousActive);
        }
    }

    private static Rect BoundsIn(RectTransform parent, RectTransform rect)
    {
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        Vector2 min = new(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new(float.NegativeInfinity, float.NegativeInfinity);
        foreach (var corner in corners)
        {
            Vector2 local = parent.InverseTransformPoint(corner);
            min = Vector2.Min(min, local);
            max = Vector2.Max(max, local);
        }

        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }
}
