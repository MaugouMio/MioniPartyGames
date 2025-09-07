using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;

public class SelectGameWindow : MonoBehaviour
{
	private GameType currentSelectGameType;

	[SerializeField]
	private List<GameTypeButton> gameTypeButtons;
	[SerializeField]
	private Text description;

    void Awake()
    {
		if (gameTypeButtons.Count == 0)
		{
			Debug.LogError("SelectGameWindow gameTypeButtons is empty");
			return;
		}

		int index = 0;
		foreach (var type in Enum.GetValues(typeof(GameType)))
		{
			if (gameTypeButtons.Count < index)
				gameTypeButtons.Add(Instantiate(gameTypeButtons[0], gameTypeButtons[0].transform.parent));

			gameTypeButtons[index++].SetGameType((GameType)type);
		}
	}

	void Start()
	{
		// 預設選猜名詞遊戲
		SelectGameType(GameType.GUESS_WORD);
	}

	public void OnConfirm()
	{
		NetManager.Instance.SendCreateRoom(currentSelectGameType);
	}

	public void SetShow(bool show)
	{
		gameObject.SetActive(show);
	}


	public void SelectGameType(GameType gameType)
	{
		currentSelectGameType = gameType;
		switch (gameType)
		{
			case GameType.GUESS_WORD:
				description.text = "你將會替隨機一名其他玩家出題，也會有另外一名隨機的玩家替你出題。\n想辦法透過是非問句一步步猜出自己的題目是什麼吧！";
				break;
			case GameType.ARRANGE_NUMBER:
				description.text = "你和你的朋友們會被分配隨機的數字。\n在不要說出自己數字的前提下，想辦法讓所有人由小到大出完所有數字吧！";
				break;
			default:
				Debug.LogError("SelectGameWindow::SelectGameType Unknown game type");
				break;
		}
	}
}
