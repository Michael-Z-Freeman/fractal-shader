using UnityEngine;

namespace FractalShader
{
	/// <summary>
	/// Utility class for high-accuracy color space conversions.
	/// Formulas sourced from the Bruce Lindbloom color math database.
	/// </summary>
	public static class ColorConverter
	{
		// --- RGB COLOR SPACE ---
		/// <summary>
		/// Standard RGB color representation with components in the 0.0 to 1.0 range.
		/// </summary>
		public struct RGBColor
		{
			public float R, G, B;

			public RGBColor(float r, float g, float b)
			{
				R = r;
				G = g;
				B = b;
			}

			/// <summary>
			/// Converts RGB to the CIE XYZ color space.
			/// Includes inverse gamma companding.
			/// </summary>
			public XYZColor ToXYZ()
			{
				float r = InverseCompand(R);
				float g = InverseCompand(G);
				float b = InverseCompand(B);

				// Transformation matrix for sRGB to XYZ (D65)
				float x = 0.4124564f * r + 0.3575761f * g + 0.1804375f * b;
				float y = 0.2126729f * r + 0.7151522f * g + 0.0721750f * b;
				float z = 0.0193339f * r + 0.1191920f * g + 0.9503041f * b;

				return new XYZColor(x, y, z);

				float InverseCompand(float v)
				{
					return (v <= 0.04045f) ? (v / 12.92f) : Mathf.Pow((v + 0.055f) / 1.055f, 2.4f);
				}
			}

			public Vector3 ToVector3() => new Vector3(R, G, B);

			public static RGBColor Random() =>
				new RGBColor(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value);
		}

		// --- XYZ COLOR SPACE ---
		/// <summary>
		/// CIE 1931 XYZ color space, the foundation for all other device-independent color spaces.
		/// </summary>
		public struct XYZColor
		{
			public float X, Y, Z;

			public XYZColor(float x, float y, float z)
			{
				X = x;
				Y = y;
				Z = z;
			}

			/// <summary>
			/// Converts XYZ to RGB with sRGB gamma companding.
			/// </summary>
			public RGBColor ToRGB()
			{
				// Transformation matrix for XYZ to sRGB (D65)
				float r = 3.2404542f * X - 1.5371385f * Y - 0.4985314f * Z;
				float g = -0.9692660f * X + 1.8760108f * Y + 0.0415560f * Z;
				float b = 0.0556434f * X - 0.2040259f * Y + 1.0572252f * Z;

				return new RGBColor(Compand(r), Compand(g), Compand(b));

				float Compand(float v)
				{
					return (v <= 0.0031308f) ? (v * 12.92f) : (Mathf.Pow(v, 1f / 2.4f) * 1.055f - 0.055f);
				}
			}

			/// <summary>
			/// Converts XYZ to CIELAB (L*a*b*) space.
			/// Uses D65 Reference White.
			/// </summary>
			public LABColor ToLAB()
			{
				// Reference white D65
				float x = X / 0.95047f;
				float y = Y / 1.00000f;
				float z = Z / 1.08883f;

				float l = 116f * Pivot(y) - 16f;
				float a = 500f * (Pivot(x) - Pivot(y));
				float b = 200f * (Pivot(y) - Pivot(z));

				return new LABColor(l, a, b);

				float Pivot(float v)
				{
					return (v > 0.008856f) ? Mathf.Pow(v, 1f / 3f) : ((v * 903.3f + 16f) / 116f);
				}
			}

			public Vector3 ToVector3() => new Vector3(X, Y, Z);
		}

		// --- LAB COLOR SPACE ---
		/// <summary>
		/// CIELAB color space (L* = Lightness, a* = Green-Red, b* = Blue-Yellow).
		/// Perceptually uniform space ideal for color blending.
		/// </summary>
		public struct LABColor
		{
			public float L, A, B;

			public LABColor(float l, float a, float b)
			{
				L = l;
				A = a;
				B = b;
			}

			/// <summary>
			/// Converts CIELAB back to XYZ.
			/// </summary>
			public XYZColor ToXYZ()
			{
				float t = (L + 16f) / 116f;
				float x = InversePivot(A / 500f + t);
				float z = InversePivot(t - B / 200f);
				float y = (L > (903.3f * 0.008856f)) ? Mathf.Pow(t, 3f) : (L / 903.3f);

				return new XYZColor(x * 0.95047f, y, z * 1.08883f);

				float InversePivot(float v)
				{
					float v3 = Mathf.Pow(v, 3f);
					return (v3 > 0.008856f) ? v3 : ((116f * v - 16f) / 903.3f);
				}
			}

			/// <summary>
			/// Converts CIELAB to CIELCH (Lightness, Chroma, Hue).
			/// </summary>
			public LCHColor ToLCH()
			{
				float c = Mathf.Sqrt(A * A + B * B);
				float h = Mathf.Atan2(B, A) * Mathf.Rad2Deg;
				if (h < 0) h += 360f;

				return new LCHColor(L, c, h);
			}

			public Vector3 ToVector3() => new Vector3(L, A, B);
		}

		// --- LCH COLOR SPACE ---
		/// <summary>
		/// CIELCH color space (L* = Lightness, C* = Chroma, H = Hue).
		/// Excellent for user-friendly color pickers.
		/// </summary>
		public struct LCHColor
		{
			public float L, C, H;

			public LCHColor(float l, float c, float h)
			{
				L = l;
				C = c;
				H = h;
			}

			/// <summary>
			/// Converts CIELCH back to CIELAB.
			/// </summary>
			public LABColor ToLAB()
			{
				float rad = H * Mathf.Deg2Rad;
				float a = C * Mathf.Cos(rad);
				float b = C * Mathf.Sin(rad);
				return new LABColor(L, a, b);
			}

			public Vector3 ToVector3() => new Vector3(L, C, H);
		}
	}
}