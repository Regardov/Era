using System.Collections.Generic;
using UnityEngine;

namespace Era
{
    // Presentation state is advanced exactly once per Update, never by an IMGUI repaint.
    // The bounded cache also retires hidden catalogue cards, old bosses and lobby controls.
    public sealed class HudMotion
    {
        sealed class Channel
        {
            public float value, target, speed, touched;
        }
        readonly Dictionary<string, Channel> channels = new Dictionary<string, Channel>();
        readonly List<string> expired = new List<string>();
        float clock;
        public int Count => channels.Count;
        public const int Capacity = 384;

        public void Clear() { channels.Clear(); expired.Clear(); clock = 0; }
        Channel Get(string key, float initial)
        {
            if (channels.TryGetValue(key, out var channel)) return channel;
            if (channels.Count >= Capacity)
            {
                string oldest = null; float age = float.MaxValue;
                foreach (var pair in channels) if (pair.Value.touched < age) { oldest = pair.Key; age = pair.Value.touched; }
                if (oldest != null) channels.Remove(oldest);
            }
            channel = new Channel { value = initial, target = initial, speed = 14, touched = clock };
            channels.Add(key, channel); return channel;
        }
        public float Value(string key, float target, float speed = 14, float? initial = null)
        {
            var c = Get(key, initial ?? target); c.target = target; c.speed = speed; c.touched = clock; return c.value;
        }
        public float Read(string key, float fallback = 0)
        {
            if (!channels.TryGetValue(key, out var c)) return fallback;
            c.touched = clock; return c.value;
        }
        public void Reset(string key, float value)
        {
            var c = Get(key, value); c.value = c.target = value; c.touched = clock;
        }
        public void Pulse(string key, float strength = 1)
        {
            var c = Get(key, 0); c.value = Mathf.Max(c.value, strength); c.target = 0; c.speed = 7; c.touched = clock;
        }
        public void Tick(float delta)
        {
            delta = Mathf.Max(0, delta); clock += delta; expired.Clear();
            foreach (var pair in channels)
            {
                var c = pair.Value;
                c.value = Mathf.Lerp(c.value, c.target, 1 - Mathf.Exp(-Mathf.Max(0, c.speed) * delta));
                if (Mathf.Abs(c.value - c.target) < .0001f) c.value = c.target;
                if (clock - c.touched > 3) expired.Add(pair.Key);
            }
            foreach (var key in expired) channels.Remove(key);
        }
        public static float EaseOut(float value) { value = Mathf.Clamp01(value); return 1 - Mathf.Pow(1 - value, 3); }
        public static float Envelope(float age, float duration, float enter = .18f, float exit = .24f)
        {
            if (duration <= 0 || age < 0 || age >= duration) return 0;
            return EaseOut(age / Mathf.Max(.001f, enter)) * EaseOut((duration - age) / Mathf.Max(.001f, exit));
        }
    }

    // Values always show the authoritative state; only the bar and delayed damage trail interpolate.
    public sealed class HudHealthMeter
    {
        public float Value { get; private set; }
        public float Trail { get; private set; }
        public float Damage { get; private set; }
        float target, hold;
        bool initialized;
        public void Set(float ratio)
        {
            ratio = Mathf.Clamp01(ratio);
            if (!initialized) { initialized = true; Value = Trail = target = ratio; return; }
            if (ratio < target - .0001f) { hold = .18f; Damage = Mathf.Clamp01((target - ratio) * 3 + .18f); }
            target = ratio;
        }
        public void Tick(float delta)
        {
            delta = Mathf.Max(0, delta);
            Value = Mathf.Lerp(Value, target, 1 - Mathf.Exp(-23 * delta));
            float trailDelta = Mathf.Max(0, delta - hold); hold = Mathf.Max(0, hold - delta);
            if (target > Trail) Trail = Value;
            else Trail = Mathf.Max(Value, Mathf.Lerp(Trail, target, 1 - Mathf.Exp(-5 * trailDelta)));
            Damage = Mathf.Max(0, Damage - delta * 2.5f);
        }
    }
}
