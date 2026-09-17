using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FractalShader
{
	/// <summary>
	/// Provides utility methods to generate perceptually uniform color gradients using the LCH color space.
	/// This prevents the "muddy" look typical of linear RGB interpolation.
	/// </summary>
	public static class Gradients
	{
		/// <summary>
		/// Creates a high-quality palette from a set of key colors.
		/// </summary>
		/// <param name="colorKeys">Vector4 array where (x,y,z) is RGB 0-255 and (w) is the position percentage 0-1.</param>
		/// <param name="resolution">The total number of colors (steps) in the resulting gradient.</param>
		/// <returns>An array of RGB Vector3s (0.0 to 1.0 range).</returns>
		public static Vector3[] GeneratePalette(Vector4[] colorKeys, int resolution)
		{
			List<ColorConverter.LCHColor> lchPalette = new List<ColorConverter.LCHColor>();

			if (resolution < colorKeys.Length)
			{
				Debug.LogError("[Gradients] Resolution is too low to accommodate all color keys.");
				throw new ArgumentException("Resolution must be greater than or equal to colorKeys length.");
			}

			for (int i = 0; i < colorKeys.Length; i++)
			{
				// Convert current key to LCH
				ColorConverter.LCHColor startLCH = RGBToLCH(Vector4ToRGB(colorKeys[i]));
				lchPalette.Add(startLCH);

				// If there is a next color, interpolate between them
				if (i < colorKeys.Length - 1)
				{
					ColorConverter.LCHColor endLCH = RGBToLCH(Vector4ToRGB(colorKeys[i + 1]));

					// Calculate how many steps should fit between these two specific keys based on their percentages
					int startIdx = Mathf.FloorToInt(colorKeys[i].w * resolution);
					int endIdx = Mathf.CeilToInt(colorKeys[i + 1].w * resolution);
					int stepsBetween = endIdx - startIdx - 2;

					if (stepsBetween > 0)
					{
						var intermediateColors = InterpolateLCH(startLCH, endLCH, stepsBetween);
						lchPalette.AddRange(intermediateColors);
					}
				}
			}

			// Safety check: Fill remaining slots if precision errors caused a shortfall
			while (lchPalette.Count < resolution)
			{
				lchPalette.Add(RGBToLCH(Vector4ToRGB(colorKeys[colorKeys.Length - 1])));
			}

			// Convert the final LCH list back to RGB for GPU processing
			Vector3[] finalRgbPalette = new Vector3[resolution];
			for (int j = 0; j < resolution; j++)
			{
				finalRgbPalette[j] = LCHToRGB(lchPalette[j]).ToVector3();
			}

			return finalRgbPalette;
		}

		/// <summary>
		/// Interpolates between two LCH colors, handling the 360-degree Hue wrap-around correctly.
		/// </summary>
		private static ColorConverter.LCHColor[] InterpolateLCH(ColorConverter.LCHColor a, ColorConverter.LCHColor b, int steps)
		{
			ColorConverter.LCHColor[] results = new ColorConverter.LCHColor[steps];

			float deltaL = (b.L - a.L) / (steps + 1f);
			float deltaC = (b.C - a.C) / (steps + 1f);
			float deltaH;

			// Correct for shortest path on the hue wheel (prevents going "the long way" around the color circle)
			float diffH = b.H - a.H;
			if (Mathf.Abs(diffH) <= 180f)
			{
				deltaH = diffH / (steps + 1f);
			}
			else
			{
				deltaH = (360f - Mathf.Abs(diffH)) / (steps + 1f);
				deltaH *= -Mathf.Sign(diffH); // Flip direction to take the shortest path
			}

			for (int i = 0; i < steps; i++)
			{
				float newH = a.H + deltaH * (i + 1f);

				// Wrap hue back to [0-360]
				if (newH >= 360f) newH -= 360f;
				if (newH < 0) newH += 360f;

				results[i] = new ColorConverter.LCHColor(
					a.L + deltaL * (i + 1f),
					a.C + deltaC * (i + 1f),
					newH
				);
			}

			return results;
		}

		// --- INTERNAL CONVERSIONS ---

		private static ColorConverter.RGBColor Vector4ToRGB(Vector4 v)
		{
			// Input: 0-255, Output: 0-1
			return new ColorConverter.RGBColor(v.x / 255f, v.y / 255f, v.z / 255f);
		}

		private static ColorConverter.LCHColor RGBToLCH(ColorConverter.RGBColor col)
		{
			return col.ToXYZ().ToLAB().ToLCH();
		}

		private static ColorConverter.RGBColor LCHToRGB(ColorConverter.LCHColor col)
		{
			return col.ToLAB().ToXYZ().ToRGB();
		}
	}
}