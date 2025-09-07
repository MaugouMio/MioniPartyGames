using UnityEngine;
using UnityEngine.UI;

public class GameTypeButton : MonoBehaviour
{
	private GameType gameType;

	[SerializeField]
	private SelectGameWindow parentSelectGameWindow;

	private Text gameName;

	void Awake()
    {
		gameName = GetComponentInChildren<Text>();
	}

	public void SetGameType(GameType gameType)
	{
		switch (gameType)
		{
			case GameType.GUESS_WORD:
				gameName.text = "²q¦Wµü";
				break;
			case GameType.ARRANGE_NUMBER:
				gameName.text = "±Æ¼Æ¦r";
				break;
			default:
				Debug.LogError("GameTypeButton::SetGameType Unknown game type");
				break;
		}
	}

	public void OnToggle(bool isOn)
	{
		if (isOn)
			parentSelectGameWindow.SelectGameType(gameType);
	}
}
