using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GamePage : MonoBehaviour
{
	public static GamePage Instance { get; private set; }

	[SerializeField]
	private List<PlayerInfo> PlayerList;
	[SerializeField]
	private TextList EventList;
	[SerializeField]
	private TextList GuessRecord;
	[SerializeField]
	private TextList CurrentPlayerGuessRecord;

	private bool needUpdate = false;

	void Awake()
	{
		Instance = this;
	}

	// Start is called once before the first execution of Update after the MonoBehaviour is created
	void Start()
    {
		
    }

    // Update is called once per frame
    void Update()
    {
		if (needUpdate)
			UpdateDataReal();

		// if (Input.GetKeyDown(KeyCode.Return))
		// {
			// if (NameWindow.activeSelf)
				// ClickSetName();
			// else
				// ClickConnect();
		// }
    }

	void OnDestroy()
	{
		Instance = null;
	}

	private void UpdateDataReal()
	{
		UpdatePlayerInfo();
		UpdateMiddlePage();
		UpdateEventList();
		UpdateSelfGuessRecord();
		UpdateCurrentPlayerGuessRecord();

		needUpdate = false;
	}
	public void UpdateData()
	{
		needUpdate = true;
	}

	public void UpdatePlayerInfo()
	{
		if (GameData.Instance.CurrentState == GameState.WAITING)
		{
			int idx = 0;
			foreach (var player in GameData.Instance.PlayerDatas.Values)
			{
				PlayerInfo obj;
				if (idx >= PlayerList.Count)
				{
					obj = Instantiate(PlayerList[0]);
					obj.transform.SetParent(PlayerList[0].transform.parent, false);
					PlayerList.Add(obj);
				}
				else
				{
					obj = PlayerList[idx];
				}

				obj.gameObject.SetActive(true);
				obj.UpdateData(player);
				idx++;
			}

			// 關閉未使用的物件
			while (idx < PlayerList.Count)
				PlayerList[idx++].gameObject.SetActive(false);
		}
	}

	public void UpdateMiddlePage()
	{

	}

	public void UpdateEventList()
	{
		EventList.UpdateData(GameData.Instance.EventRecord);
	}

	public void UpdateSelfGuessRecord()
	{
		if (!GameData.Instance.PlayerDatas.TryGetValue(GameData.Instance.SelfUID, out PlayerData player))
		{
			GuessRecord.UpdateData(null);
			return;
		}

		GuessRecord.UpdateData(player.GuessHistory);
	}

	public void UpdateCurrentPlayerGuessRecord()
	{
		ushort uid = GameData.Instance.PlayerOrder[GameData.Instance.GuessingPlayerIndex];
		if (!GameData.Instance.PlayerDatas.TryGetValue(uid, out PlayerData player))
		{
			GuessRecord.UpdateData(null);
			return;
		}

		CurrentPlayerGuessRecord.UpdateData(player.GuessHistory);
	}
}
