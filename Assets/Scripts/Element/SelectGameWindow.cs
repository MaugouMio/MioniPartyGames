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
		// �w�]��q�W���C��
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
				description.text = "�A�N�|���H���@�W��L���a�X�D�A�]�|���t�~�@�W�H�������a���A�X�D�C\n�Q��k�z�L�O�D�ݥy�@�B�B�q�X�ۤv���D�جO����a�I";
				break;
			case GameType.ARRANGE_NUMBER:
				description.text = "�A�M�A���B�̷ͭ|�Q���t�H�����Ʀr�C\n�b���n���X�ۤv�Ʀr���e���U�A�Q��k���Ҧ��H�Ѥp��j�X���Ҧ��Ʀr�a�I";
				break;
			default:
				Debug.LogError("SelectGameWindow::SelectGameType Unknown game type");
				break;
		}
	}
}
