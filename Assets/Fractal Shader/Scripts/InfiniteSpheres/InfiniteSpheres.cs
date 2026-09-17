using UnityEngine;

namespace FractalShader
{
	public class InfiniteSpheres : App
	{
		public static InfiniteSpheres Instance;

		[Header("Sphere Configuration")]
		public float radius = 1.0f;
		public bool useRepetition = false;
		public bool invertGeometry = false;

		[Header("Visuals & Lighting")]
		public Color sphereColor = new Color(0.8f, 0.2f, 0.2f, 1f);
		public Color backgroundColor = Color.black;
		[Range(0.1f, 5f)] public float emissionStrength = 1f;

		[Header("Atmosphere")]
		[Range(10f, 200f)] public float fogDensity = 40f;
		[Range(10f, 500f)] public float maxDistance = 100f;

		protected override void Awake()
		{
			base.Awake();
			Instance = this;
			cameraType = CameraType.Free;
		}

		// URP Feature kullandığımız için Render metodu Feature içinden yönetilir
		protected override void Render(RenderTexture target) { }

		#region Options Manager Bridge (O_ Prefix)
		public float O_Radius { get => radius; set { radius = value; ReRender(); } }
		public bool O_Repeat { get => useRepetition; set { useRepetition = value; ReRender(); } }
		public bool O_Invert { get => invertGeometry; set { invertGeometry = value; ReRender(); } }
		public float O_Emission { get => emissionStrength; set { emissionStrength = value; ReRender(); } }
		#endregion
	}
}