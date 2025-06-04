using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TextList : MonoBehaviour
{
	[SerializeField]
	private ScrollRect scrollRect;
	[SerializeField]
	private List<Text> textList;

	private int moveToLastTick = 0;

	void Start()
	{
		if (textList.Count == 0)
			Debug.LogError($"TextList component is empty for object {gameObject.name}");
		else
			textList[0].gameObject.SetActive(false); // 初始時隱藏第一個 Text 元件
	}

	void Update()
	{
		if (moveToLastTick > 0)
		{
			if (--moveToLastTick == 0)
				scrollRect.verticalNormalizedPosition = 0f; // 滾動到最底部
		}
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
					obj.transform.SetParent(textList[0].transform.parent, false);
					textList.Add(obj);
				}
				else
				{
					obj = textList[idx];
				}

				obj.gameObject.SetActive(true);
				obj.text = str;
				LayoutRebuilder.ForceRebuildLayoutImmediate(obj.rectTransform);

				idx++;
			}
		}

		// 關閉多餘的 Text 元件
		while (idx < textList.Count)
			textList[idx++].gameObject.SetActive(false);
	}

	public void MoveToLast()
	{
		moveToLastTick = 2;
		//scrollRect.verticalNormalizedPosition = 0f;
	}
}
