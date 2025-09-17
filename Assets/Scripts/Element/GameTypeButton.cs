using UnityEngine;
using UnityEngine.UI;

public class GameTypeButton : MonoBehaviour
{
	private GameType gameType;

	[SerializeField]
	private SelectGameWindow parentSelectGameWindow;

	private Toggle toggle;
	private Text gameName;

	void Awake()
    {
		toggle = GetComponent<Toggle>();
		gameName = GetComponentInChildren<Text>();
	}

	public void SetGameType(GameType gameType)
	{
		this.gameType = gameType;
		switch (gameType)
		{
			case GameType.GUESS_WORD:
				gameName.text = "�q�W��";
				break;
			case GameType.ARRANGE_NUMBER:
				gameName.text = "�ƼƦr";
				break;
			default:
				Debug.LogError("GameTypeButton::SetGameType Unknown game type");
				break;
		}
	}

	public void Select()
	{
		toggle.isOn = true;
	}

	public void OnToggle(bool isOn)
	{
		if (isOn)
			parentSelectGameWindow.SelectGameType(gameType);
	}
}
