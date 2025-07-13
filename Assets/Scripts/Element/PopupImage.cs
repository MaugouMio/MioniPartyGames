using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class PopupImage : MonoBehaviour
{
	public float duration = 0.3f;
	public float initAlpha = 0.8f;
	public float scaleFrom = 1f;
	public float scaleTo = 2f;

	private CanvasGroup canvasGroup;
	private Image image;
	private IEnumerator animCoroutine;

	void Awake()
	{
		canvasGroup = GetComponent<CanvasGroup>();
		if (canvasGroup == null)
			Debug.LogError($"PopupImage requires a CanvasGroup component. ({gameObject.name})");
		else
			canvasGroup.alpha = 0f; // 初始透明度設為0

		image = GetComponentInChildren<Image>();
		if (image == null)
			Debug.LogError($"PopupImage requires a Image component as a child. ({gameObject.name})");
	}

	private IEnumerator Display()
	{
		canvasGroup.alpha = initAlpha;
		image.transform.localScale = Vector3.one * scaleFrom;

		float currentTime = 0f;
		while (currentTime < duration)
		{
			currentTime += Time.deltaTime;
			float rate = Mathf.Clamp01(currentTime / duration);
			canvasGroup.alpha = (1f - rate) * initAlpha;
			image.transform.localScale = Vector3.one * Mathf.Lerp(scaleFrom, scaleTo, rate);
			yield return null;
		}

		canvasGroup.alpha = 0f;
		animCoroutine = null;
	}

	public void ShowImage(string filename)
	{
		if (canvasGroup == null || image == null)
		{
			Debug.LogError("PopupImage is not properly initialized.");
			return;
		}

		if (animCoroutine != null)
			StopCoroutine(animCoroutine);

		canvasGroup.alpha = 0f;
		image.sprite = Resources.Load<Sprite>($"Images/{filename}");
		if (image.sprite == null)
		{
			Debug.LogError($"Image '{filename}' not found in Resources/Images folder.");
			return;
		}
		image.rectTransform.sizeDelta = new Vector2(image.sprite.rect.width, image.sprite.rect.height);

		animCoroutine = Display();
		StartCoroutine(animCoroutine);
	}
}
