using System.Numerics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;

namespace Deltempo.App.WinUI;

/// <summary>
/// The shell's one motion system.
///
/// Every transition runs through here so the curve and the duration are a single
/// decision instead of a per-call guess. The rules:
///
///   - Entrances use one strong ease-out (the composition equivalent of
///     cubic-bezier(0.23, 1, 0.32, 1)) - it starts fast, which is what makes a
///     200ms transition feel like 120ms.
///   - Nothing in a utility UI runs past 250ms. The user is working, not watching.
///   - Only Opacity and Offset animate. Both are compositor properties, so they skip
///     layout and reflow entirely; animating a Width or a Margin would re-layout the
///     panel on every frame.
///   - Composition animations rather than XAML storyboards: they retarget instead of
///     restarting when a second navigation lands mid-flight, they run off the UI
///     thread's work, and they can target elements that were built in code (the rail's
///     selection bars) without a name table.
///   - Reduced motion ships with the animation, not after it: when Windows reports
///     animations off, the end state is applied and nothing moves.
/// </summary>
internal static class ShellMotion
{
    /// <summary>
    /// Per-element generation counters, so an Exit that is already fading out can be
    /// cancelled by an Enter instead of collapsing the surface the user just reopened.
    /// ConditionalWeakTable keeps the counter lifetime tied to the element.
    /// </summary>
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<UIElement, Generation> Exits = new();

    private sealed class Generation
    {
        public int Value;
    }

    /// <summary>
    /// False when the OS reports animations are off (Settings &gt; Accessibility &gt;
    /// Animation effects). Read once: this setting cannot change while the process is
    /// running, and a settings watcher for one boolean would cost more than it saves.
    /// </summary>
    public static bool MotionEnabled { get; } = ReadSystemMotionSetting();

    private static bool ReadSystemMotionSetting()
    {
        try
        {
            return new Windows.UI.ViewManagement.UISettings().AnimationsEnabled;
        }
        catch
        {
            // Unknown: play the animation rather than silently dull the whole UI.
            return true;
        }
    }

    private static Microsoft.UI.Composition.CompositionEasingFunction EaseOut(
        Microsoft.UI.Composition.Compositor compositor)
        => compositor.CreateCubicBezierEasingFunction(new Vector2(0.23f, 1f), new Vector2(0.32f, 1f));

    /// <summary>
    /// Entrance for content that replaced other content: fade from 0 and rise a few DIP
    /// into place. Plays relative to whatever opacity/offset layout set, so it is safe
    /// to call on any element in any state.
    /// </summary>
    public static void Enter(UIElement element, float riseDip = 10f, double milliseconds = 200)
    {
        if (!MotionEnabled || element is null)
        {
            return;
        }

        // A surface the user just reopened must not be collapsed by the Exit that is
        // still fading out behind it.
        if (Exits.TryGetValue(element, out Generation? generation))
        {
            generation.Value++;
        }

        var visual = ElementCompositionPreview.GetElementVisual(element);
        var compositor = visual.Compositor;
        var ease = EaseOut(compositor);
        var duration = TimeSpan.FromMilliseconds(milliseconds);

        float baseOpacity = visual.Opacity;
        Vector3 baseOffset = visual.Offset;

        var opacity = compositor.CreateScalarKeyFrameAnimation();
        opacity.Duration = duration;
        opacity.InsertKeyFrame(0f, 0f, ease);
        opacity.InsertKeyFrame(1f, baseOpacity, ease);
        visual.StartAnimation("Opacity", opacity);

        if (riseDip <= 0f)
        {
            return;
        }

        var offset = compositor.CreateVector3KeyFrameAnimation();
        offset.Duration = duration;
        offset.InsertKeyFrame(0f, baseOffset + new Vector3(0f, riseDip, 0f), ease);
        offset.InsertKeyFrame(1f, baseOffset, ease);
        visual.StartAnimation("Offset", offset);
    }

    /// <summary>
    /// Exit pair for <see cref="Enter"/>: fade out over a shorter beat, then hand back
    /// to the caller to collapse the element. The callback runs on the element's own
    /// dispatcher queue, so it is safe to touch UI from it.
    /// </summary>
    public static void Exit(UIElement element, Action? after, double milliseconds = 140)
    {
        if (!MotionEnabled || element is null)
        {
            after?.Invoke();
            return;
        }

        Generation counter = Exits.GetOrCreateValue(element);
        int token = ++counter.Value;

        var visual = ElementCompositionPreview.GetElementVisual(element);
        var compositor = visual.Compositor;
        var ease = EaseOut(compositor);

        float baseOpacity = visual.Opacity;

        var opacity = compositor.CreateScalarKeyFrameAnimation();
        opacity.Duration = TimeSpan.FromMilliseconds(milliseconds);
        opacity.InsertKeyFrame(0f, baseOpacity, ease);
        opacity.InsertKeyFrame(1f, 0f, ease);
        visual.StartAnimation("Opacity", opacity);

        if (after is null)
        {
            return;
        }

        var queue = element.DispatcherQueue;
        _ = Task.Delay(TimeSpan.FromMilliseconds(milliseconds)).ContinueWith(
            _ => queue.TryEnqueue(() =>
            {
                // Only collapse if no Enter has superseded this exit in the meantime.
                if (counter.Value == token)
                {
                    after?.Invoke();
                }
            }),
            TaskScheduler.Default);
    }
}
