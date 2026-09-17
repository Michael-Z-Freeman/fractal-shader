using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace FractalShader
{
	/// <summary>
	/// Acts as a bridge between the UI, the Animation system, and the Fractal parameters.
	/// Uses Reflection to dynamically get and set properties on the active fractal.
	/// </summary>
	public class OptionsManager : MonoBehaviour
	{
		// The active app (fractal) instance this manager is controlling
		[HideInInspector] public App target;

		// Dictionaries to track active animation IDs for different types
		private Dictionary<string, int> animatedFloats = new Dictionary<string, int>();
		private Dictionary<string, AnimId2> animatedVector2s = new Dictionary<string, AnimId2>();
		private Dictionary<string, AnimId3> animatedVector3s = new Dictionary<string, AnimId3>();

		private void Update()
		{
			if (target == null) return;

			// Update all properties currently under animation
			foreach (var anim in animatedFloats)
			{
				SetOption(anim.Key, target.animationController.Get(anim.Value));
			}

			foreach (var anim in animatedVector2s)
			{
				SetOption(anim.Key, new Vector2(
					target.animationController.Get(anim.Value.x),
					target.animationController.Get(anim.Value.y)));
			}

			foreach (var anim in animatedVector3s)
			{
				SetOption(anim.Key, new Vector3(
					target.animationController.Get(anim.Value.x),
					target.animationController.Get(anim.Value.y),
					target.animationController.Get(anim.Value.z)));
			}
		}

		/// <summary>
		/// Dynamically sets a property value on the target instance using Reflection.
		/// </summary>
		public void SetOption(string propertyName, object value)
		{
			if (target == null) return;

			PropertyInfo prop = target.GetType().GetProperty(propertyName);
			if (prop != null && prop.CanWrite)
			{
				// Convert value to match the property's type if necessary
				object convertedValue = Convert.ChangeType(value, prop.PropertyType);
				prop.SetValue(target, convertedValue);

				target.ReRender(); // Trigger GPU update
			}
		}

		/// <summary>
		/// Retrieves a property value from the target instance using Reflection.
		/// </summary>
		public object GetOption(string propertyName)
		{
			if (target == null) return 0f;

			PropertyInfo prop = target.GetType().GetProperty(propertyName);
			if (prop == null)
			{
				Debug.LogWarning($"[OptionsManager] Property '{propertyName}' not found on {target.GetType().Name}");
				return 0f;
			}

			return prop.GetValue(target);
		}

		#region Animation Starters

		public void StartAnimation(string propertyName, float speed, float min, float max, bool isOscillating)
		{
			if (target == null) return;

			object currentVal = GetOption(propertyName);
			if (!(currentVal is int || currentVal is float)) return;

			float value = Convert.ToSingle(currentVal);
			int id = target.animationController.Register(value, speed, min, max, isOscillating);

			if (!animatedFloats.ContainsKey(propertyName))
				animatedFloats.Add(propertyName, id);
		}

		public void StartAnimation(string propertyName, Vector2 speed, Vector2 min, Vector2 max, bool isOscillating)
		{
			if (target == null) return;

			object currentVal = GetOption(propertyName);
			if (!(currentVal is Vector2)) return;

			Vector2 value = (Vector2)currentVal;
			AnimId2 ids = new AnimId2(
				target.animationController.Register(value.x, speed.x, min.x, max.x, isOscillating),
				target.animationController.Register(value.y, speed.y, min.y, max.y, isOscillating)
			);

			if (!animatedVector2s.ContainsKey(propertyName))
				animatedVector2s.Add(propertyName, ids);
		}

		public void StartAnimation(string propertyName, Vector3 speed, Vector3 min, Vector3 max, bool isOscillating)
		{
			if (target == null) return;

			object currentVal = GetOption(propertyName);
			if (!(currentVal is Vector3)) return;

			Vector3 value = (Vector3)currentVal;
			AnimId3 ids = new AnimId3(
				target.animationController.Register(value.x, speed.x, min.x, max.x, isOscillating),
				target.animationController.Register(value.y, speed.y, min.y, max.y, isOscillating),
				target.animationController.Register(value.z, speed.z, min.z, max.z, isOscillating)
			);

			if (!animatedVector3s.ContainsKey(propertyName))
				animatedVector3s.Add(propertyName, ids);
		}

		#endregion

		/// <summary>
		/// Stops animation for a specific property and cleans up IDs.
		/// </summary>
		public void StopAnimation(string propertyName)
		{
			if (target == null) return;

			if (animatedFloats.ContainsKey(propertyName))
			{
				target.animationController.Unregister(animatedFloats[propertyName]);
				animatedFloats.Remove(propertyName);
			}
			else if (animatedVector2s.ContainsKey(propertyName))
			{
				target.animationController.Unregister(animatedVector2s[propertyName].x);
				target.animationController.Unregister(animatedVector2s[propertyName].y);
				animatedVector2s.Remove(propertyName);
			}
			else if (animatedVector3s.ContainsKey(propertyName))
			{
				target.animationController.Unregister(animatedVector3s[propertyName].x);
				target.animationController.Unregister(animatedVector3s[propertyName].y);
				target.animationController.Unregister(animatedVector3s[propertyName].z);
				animatedVector3s.Remove(propertyName);
			}
		}

		// Internal structs for ID management
		private struct AnimId2 { public int x, y; public AnimId2(int x, int y) { this.x = x; this.y = y; } }
		private struct AnimId3 { public int x, y, z; public AnimId3(int x, int y, int z) { this.x = x; this.y = y; this.z = z; } }
	}
}