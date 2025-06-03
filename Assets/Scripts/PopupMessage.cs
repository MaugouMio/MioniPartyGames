using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.RuleTile.TilingRuleOutput;

public class PopupMessage : MonoBehaviour
{
	public int floatHeight = 100;
	public float enterDuration = 0.3f;
	public float stayDuration = 3f;
	public float exitDuration = 0.3f;

	private CanvasGroup canvasGroup;
	private Text messageText;
	private IEnumerator animCoroutine;
	private Vector3 initPosition;

	void Awake()
	{
		canvasGroup = GetComponent<CanvasGroup>();
		if (canvasGroup == null)
			Debug.LogError($"PopupMessage requires a CanvasGroup component. ({gameObject.name})");
		else
			canvasGroup.alpha = 0f; // 初始透明度設為0

		messageText = GetComponentInChildren<Text>();
		if (messageText == null)
			Debug.LogError($"PopupMessage requires a Text component as a child. ({gameObject.name})");
	}

	void Start()
	{
		initPosition = transform.localPosition;
	}

	private IEnumerator Display()
	{
		canvasGroup.alpha = 0f;

		// 淡入動畫
		float currentTime = 0f;
		while (currentTime < enterDuration)
		{
			currentTime += Time.deltaTime;
			float rate = Mathf.Clamp01(currentTime / enterDuration);
			canvasGroup.alpha = rate;
			transform.localPosition = initPosition + Vector3.down * floatHeight * (1f - rate);
			yield return null;
		}
		transform.localPosition = initPosition;
		canvasGroup.alpha = 1f;

		// 停留時間
		yield return new WaitForSeconds(stayDuration);

		// 淡出動畫
		currentTime = 0f;
		while (currentTime < exitDuration)
		{
			currentTime += Time.deltaTime;
			canvasGroup.alpha = 1f - Mathf.Clamp01(currentTime / exitDuration);
			yield return null;
		}

		canvasGroup.alpha = 0f;
		animCoroutine = null;
	}

	public void ShowMessage(string message)
	{
		if (canvasGroup == null || messageText == null)
		{
			Debug.LogError("PopupMessage is not properly initialized.");
			return;
		}

		if (animCoroutine != null)
			StopCoroutine(animCoroutine);

		messageText.text = message;
		animCoroutine = Display();
		StartCoroutine(animCoroutine);
	}
}
