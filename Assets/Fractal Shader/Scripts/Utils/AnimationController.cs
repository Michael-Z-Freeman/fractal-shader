using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FractalShader
{
	/// <summary>
	/// Core animation engine for the framework. 
	/// Manages linear and oscillating transitions for fractal parameters using a registration system.
	/// </summary>
	public class AnimationController : MonoBehaviour
	{
		private App app;
		private Dictionary<int, AnimParams> animations;

		private void OnEnable()
		{
			app = GetComponent<App>();
			animations = new Dictionary<int, AnimParams>();
		}

		/// <summary>
		/// Registers a variable to be animated.
		/// </summary>
		/// <param name="value">Current starting value.</param>
		/// <param name="speed">Animation speed.</param>
		/// <param name="min">Minimum range boundary.</param>
		/// <param name="max">Maximum range boundary.</param>
		/// <param name="isOscillating">If true, uses a Sine wave (Oscillation). If false, uses a Linear loop.</param>
		/// <returns>A unique ID used to retrieve the animated value.</returns>
		public int Register(float value, float speed, float min, float max, bool isOscillating)
		{
			value = Mathf.Clamp(value, min, max);

			// Generate a unique identifier for this animation track
			int id = Guid.NewGuid().GetHashCode();

			// Adjust speed for Pi-based oscillation if necessary
			if (isOscillating) speed *= Mathf.PI;

			AnimParams p = new AnimParams(value, speed, min, max, isOscillating, value);

			// Normalize value to the internal -1 to 1 space for calculation
			animations.Add(id, Normalize(p));
			return id;
		}

		/// <summary>
		/// Stops and removes an animation track.
		/// </summary>
		public void Unregister(int id)
		{
			if (animations.ContainsKey(id))
				animations.Remove(id);
		}

		private void Update()
		{
			// Use ToList to avoid "Collection Modified" exceptions during iteration
			foreach (var entry in animations.ToList())
			{
				AnimParams p = entry.Value;

				// Increment the internal value based on DeltaTime
				p.value += p.speed * app.RequestSmoothDeltaTime();

				// Update the dictionary with clamped/looped values
				animations[entry.Key] = WrapValue(p);
			}
		}

		/// <summary>
		/// Returns the current animated value for a specific ID.
		/// </summary>
		public float Get(int id)
		{
			return Denormalize(animations[id]).value;
		}

		/// <summary>
		/// Pass-by-reference retrieval for the current animated value.
		/// </summary>
		public void Get(int id, ref float value)
		{
			value = Denormalize(animations[id]).value;
		}

		// --- INTERNAL MATH TRANSFORMS ---

		/// <summary>
		/// Maps the internal raw value back to the user's defined Min;Max range.
		/// </summary>
		private AnimParams Denormalize(AnimParams p)
		{
			if (p.isOscillating)
			{
				// Convert linear progression to a smooth Cosine wave
				p.value = (Mathf.Cos(p.value) + 1f) / 2f;
				p.value *= p.max - p.min;
				p.value += p.min;
			}

			return p;
		}

		/// <summary>
		/// Maps the user's starting value into the internal -1 to 1 calculation space.
		/// </summary>
		private AnimParams Normalize(AnimParams p)
		{
			if (p.isOscillating)
			{
				p.value -= p.min;
				p.value /= p.max - p.min;
				// Convert range to ArcCos for phase-aligned oscillation
				p.value = Mathf.Acos(p.value * 2f - 1f);
			}

			return p;
		}

		/// <summary>
		/// Handles wrapping logic for linear loops and radian resets for oscillations.
		/// </summary>
		private AnimParams WrapValue(AnimParams p)
		{
			if (p.isOscillating)
			{
				// Keep values within a 0 to 2*PI range
				if (p.value > 2f * Mathf.PI) p.value -= 2f * Mathf.PI;
				if (p.value < 0f) p.value += 2f * Mathf.PI;
			}
			else
			{
				// Loop the linear value back to min/max boundaries
				float range = p.max - p.min;
				if (p.value > p.max) p.value -= range;
				if (p.value < p.min) p.value += range;
			}

			return p;
		}

		/// <summary>
		/// Container for animation state data.
		/// </summary>
		private struct AnimParams
		{
			public float value;
			public float speed;
			public float min;
			public float max;
			public bool isOscillating;
			public float lastValue;

			public AnimParams(float value, float speed, float min, float max, bool isOscillating, float lastValue)
			{
				this.value = value;
				this.speed = speed;
				this.min = min;
				this.max = max;
				this.isOscillating = isOscillating;
				this.lastValue = lastValue;
			}
		}
	}
}