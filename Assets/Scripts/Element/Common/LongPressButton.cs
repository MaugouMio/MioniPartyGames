using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class LongPressButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
	[SerializeField]
	private float longPressDuration = 1.0f; // 長按開始時間(秒)
    [SerializeField]
	private float longPressTriggerInterval = 0.05f; // 長按連續觸發間隔(秒)
	[SerializeField]
	private UnityEvent onLongPressTrigger;

	private Button button;

	private bool isPressing = false;
	private float pointDownTime;
	private float lastTriggerTime;

	public void OnPointerDown(PointerEventData eventData)
	{
		pointDownTime = Time.time;
		lastTriggerTime = pointDownTime + longPressDuration - longPressTriggerInterval;
		isPressing = true;
	}

	public void OnPointerUp(PointerEventData eventData)
	{
		isPressing = false;
	}

	void Awake()
	{
		button = GetComponent<Button>();
		if (button == null)
			button = gameObject.AddComponent<Button>();
	}

	void Update()
	{
		if (!isPressing)
			return;

		if (Time.time - pointDownTime < longPressDuration)
			return;

		while (Time.time - lastTriggerTime >= longPressTriggerInterval)
		{
			onLongPressTrigger?.Invoke();
			lastTriggerTime += longPressTriggerInterval;
		}
	}
}
