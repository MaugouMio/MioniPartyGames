using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TextList : MonoBehaviour
{
	[SerializeField]
	private ScrollRect scrollRect;
	[SerializeField]
	private List<Text> textList;

	void Start()
	{
		if (textList.Count == 0)
			Debug.LogError($"TextList component is empty for object {gameObject.name}");
	}

	public void UpdateData(IEnumerable<string> stringList)
	{
		int idx = 0;
		if (stringList != null)
		{
			foreach (string str in stringList)
			{
				Text obj;
				if (idx >= textList.Count)
				{
					obj = Instantiate(textList[0], transform);
					textList.Add(obj);
				}
				else
				{
					obj = textList[idx];
				}

				obj.gameObject.SetActive(true);
				obj.text = str;
				idx++;
			}
		}

		// 關閉多餘的 Text 元件
		while (idx < textList.Count)
			textList[idx++].gameObject.SetActive(false);
	}

	public void MoveToLast()
	{
		scrollRect.verticalNormalizedPosition = 0f;
	}
}
