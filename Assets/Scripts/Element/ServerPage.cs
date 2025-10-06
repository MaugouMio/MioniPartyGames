using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ServerPage : MonoBehaviour
{
	[SerializeField]
	private InputField IP_Input;
	[SerializeField]
	private ServerListItem customServerItem;
	[SerializeField]
	private ServerListItem templateServerItem;

	private ServerList serverList;
	private int selectedServerIndex = -1;

	void Start()
	{
		IP_Input.text = PlayerPrefs.GetString("ServerIP", "");
		selectedServerIndex = PlayerPrefs.GetInt("ServerIndex", -2);  // -1 是自訂伺服器，用 -2 表示沒選過

		serverList = Resources.Load<ServerList>("ScriptableObjects/ServerList");
		if (serverList != null)
		{
			for (int i = 0; i < serverList.servers.Count; i++)
			{
				var item = i > 0 ? Instantiate(templateServerItem, templateServerItem.transform.parent) : templateServerItem;
				item.SetServerIndex(i);
				item.SetServerName(serverList.servers[i].name);
				if (i == selectedServerIndex)
					item.Select();
			}

			// 沒選過的話預設選到第一個伺服器
			if (selectedServerIndex == -2 && serverList.servers.Count > 0)
			{
				selectedServerIndex = 0;
				templateServerItem.Select();
			}
		}
		else
		{
			Debug.LogWarning("ServerList not found!");
		}

		// 如果沒有選擇伺服器，自動跳到自訂伺服器
		if (selectedServerIndex < 0)
		{
			selectedServerIndex = -1;
			customServerItem.Select();
		}
	}

	void Update()
	{
		// 沒有 OnFocus event 只好自己偵測
		if (selectedServerIndex >= 0 && IP_Input.isFocused)
		{
			selectedServerIndex = -1;
			customServerItem.Select();
		}
	}

	public void Show(bool isShow)
	{
		if (gameObject.activeSelf != isShow)
			gameObject.SetActive(isShow);
	}

	public void SelectServer(int index)
	{
		selectedServerIndex = index;
	}

	public void ClickConnect()
	{
		// 選擇列表中的伺服器
		if (selectedServerIndex >= 0)
		{
			var server = serverList.servers[selectedServerIndex];
			ConnectPage.Instance.ConnectToServer(server.ip, server.port, server.isWss);
			GameData.Instance.ServerName = server.name;
			PlayerPrefs.SetInt("ServerIndex", selectedServerIndex);
			return;
		}

		string[] param = IP_Input.text.Split(':');
		if (param.Length == 2)
		{
			try
			{
				string ip = param[0];
				int port = Int32.Parse(param[1]);
				ConnectPage.Instance.ConnectToServer(ip, port, false);
			}
			catch
			{
				ConnectPage.Instance.SetConnectMessage("請輸入正確的 IP:PORT 格式");
				return;
			}
		}
		else if (param.Length == 3 && param[0] == "wss")
		{
			try
			{
				string ip = param[1];
				int port = Int32.Parse(param[2]);
				ConnectPage.Instance.ConnectToServer(ip, port, true);
			}
			catch
			{
				ConnectPage.Instance.SetConnectMessage("請輸入正確的 wss:IP:PORT 格式");
				return;
			}
		}
		else
		{
			ConnectPage.Instance.SetConnectMessage("請輸入正確的 [wss:]IP:PORT 格式");
			return;
		}

		GameData.Instance.ServerName = IP_Input.text;
		PlayerPrefs.SetString("ServerIP", IP_Input.text);
		PlayerPrefs.SetInt("ServerIndex", selectedServerIndex);
	}
}
