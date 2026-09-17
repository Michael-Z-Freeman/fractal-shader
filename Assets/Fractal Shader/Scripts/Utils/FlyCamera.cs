using UnityEngine;
using UnityEngine.InputSystem;

public class FlyCamera : MonoBehaviour
{
	[Header("--- HAREKET AYARLARI ---")]
	public float moveSpeed = 10f;
	public float sprintMultiplier = 3f;

	[Header("--- FARE (MOUSE) AYARLARI ---")]
	public float mouseSensitivity = 2f;

	private float rotationX = 0f;
	private float rotationY = 0f;

	// Kameranın App tarafından ezilmesini engellemek için kendi mutlak pozisyonumuzu tutuyoruz
	private Vector3 absolutePosition;

	void Start()
	{
		Cursor.lockState = CursorLockMode.Locked;
		Cursor.visible = false;

		Vector3 angles = transform.eulerAngles;
		rotationX = angles.x;
		rotationY = angles.y;

		// Başlangıç pozisyonumuzu kaydediyoruz
		absolutePosition = transform.position;
	}

	// Update yerine LateUpdate kullanıyoruz! 
	// Bu sayede "App" sistemi kamerayı oynatsa bile, biz o kareden hemen sonra kendi kameramızı eziyoruz.
	void LateUpdate()
	{
		Keyboard keyboard = Keyboard.current;
		Mouse mouse = Mouse.current;

		// 1. FARE İLE ETRAFA BAKMA
		Vector2 mouseDelta = mouse != null ? mouse.delta.ReadValue() : Vector2.zero;
		rotationY += mouseDelta.x * mouseSensitivity;
		rotationX -= mouseDelta.y * mouseSensitivity;
		rotationX = Mathf.Clamp(rotationX, -90f, 90f);

		// Kendi mutlak dönüşümüzü (Rotasyon) uyguluyoruz
		Quaternion currentRotation = Quaternion.Euler(rotationX, rotationY, 0f);
		transform.rotation = currentRotation;

		// 2. KLAVYE İLE HAREKET (WASD)
		float currentSpeed = moveSpeed;

		if (keyboard != null && keyboard.leftShiftKey.isPressed)
		{
			currentSpeed *= sprintMultiplier;
		}

		float moveX = 0f;
		float moveZ = 0f;
		if (keyboard != null)
		{
			moveX = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
			moveZ = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);
		}
		float moveY = 0f;

		if (keyboard != null && (keyboard.eKey.isPressed || keyboard.spaceKey.isPressed)) moveY = 1f;
		if (keyboard != null && (keyboard.qKey.isPressed || keyboard.leftCtrlKey.isPressed)) moveY = -1f;

		// 3. MUTLAK POZİSYON HESAPLAMA (App'i Ezen Kısım)
		Vector3 move = new Vector3(moveX, moveY, moveZ);

		// Kameranın baktığı yöne göre mutlak pozisyonumuzu güncelliyoruz
		absolutePosition += currentRotation * move * currentSpeed * Time.deltaTime;

		// Gerçek kameranın pozisyonunu zorla kendi pozisyonumuza eşitliyoruz
		transform.position = absolutePosition;

		// 4. FARE KİLİDİNİ AÇMA/KAPAMA
		if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
		{
			Cursor.lockState = CursorLockMode.None;
			Cursor.visible = true;
		}

		if (mouse != null && mouse.leftButton.wasPressedThisFrame)
		{
			Cursor.lockState = CursorLockMode.Locked;
			Cursor.visible = false;
		}
	}
}
