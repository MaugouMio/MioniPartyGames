using System;
using UnityEngine;

public class GIFController : MonoBehaviour
{
	public Action onPlayEnd;

	public void OnPlayEnd()
	{
		onPlayEnd?.Invoke();
		gameObject.SetActive(false);
	}
}
