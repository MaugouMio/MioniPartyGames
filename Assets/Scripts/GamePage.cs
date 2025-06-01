using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GamePage : MonoBehaviour
{
	public static GamePage Instance { get; private set; }

	[SerializeField]
	private List<PlayerInfo> PlayerList;

	private bool needUpdate = false;

	void Awake()
	{
		Instance = this;
	}

	// Start is called once before the first execution of Update after the MonoBehaviour is created
	void Start()
    {
		NetManager.Instance.OnDisconnected = OnDisconnected;
    }

    // Update is called once per frame
    void Update()
    {
		if (needUpdate)
			_UpdateData();

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
		NetManager.Instance.OnDisconnected = null;
	}

	private void OnDisconnected()
	{
		// ConnectingMask.SetActive(false);
		// NameWindow.SetActive(false);
		// TopMask.SetActive(false);
		// ConnectHintText.text = "連線中斷";
	}

	private void _UpdateData()
	{
		UpdatePlayerInfo();

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
			}

			// 關閉未使用的物件
			while (idx < PlayerList.Count)
				PlayerList[idx++].gameObject.SetActive(false);
		}
	}
}
