using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(ScrollRect))]
public class TextList : MonoBehaviour
{
	private ScrollRect scrollRect;
	private readonly List<Text> textList = new();

	[SerializeField]
	Text listItemTemplate;

	private int moveToLastTick = 0;

	void Awake()
	{
		scrollRect = GetComponent<ScrollRect>();

		if (listItemTemplate == null)
			Debug.LogError($"TextList item template is empty for object {gameObject.name}");
	}

	void Start()
	{
		listItemTemplate.gameObject.SetActive(false); // 初始時隱藏預設的 Text 元件
		textList.Add(listItemTemplate);
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
