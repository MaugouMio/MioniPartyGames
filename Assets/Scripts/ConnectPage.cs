using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ConnectPage : MonoBehaviour
{
	[SerializeField]
	private InputField IP_Input;
	[SerializeField]
	private Text ConnectHintText;
	[SerializeField]
	private GameObject ConnectingMask;
	[SerializeField]
	private GameObject NameWindow;
	[SerializeField]
	private InputField NameInput;
	[SerializeField]
	private Text NameHintText;
	[SerializeField]
	private GameObject TopMask;

	// Start is called once before the first execution of Update after the MonoBehaviour is created
	void Start()
    {
		NetManager.Instance.OnConnected = OnConnected;
		NetManager.Instance.OnDisconnected = OnDisconnected;
    }

    // Update is called once per frame
    void Update()
    {
		if (Input.GetKeyDown(KeyCode.Return))
		{
			if (NameWindow.activeSelf)
				ClickSetName();
			else
				ClickConnect();
		}
    }

	void OnDestroy()
	{
		NetManager.Instance.OnConnected = null;
		NetManager.Instance.OnDisconnected = null;
	}

	private void OnConnected()
	{
		ConnectingMask.SetActive(false);
		NameWindow.SetActive(true);
	}

	private void OnDisconnected()
	{
		ConnectingMask.SetActive(false);
		NameWindow.SetActive(false);
		TopMask.SetActive(false);
		ConnectHintText.text = "連線中斷";
	}

	public void ClickConnect()
	{
		if (ConnectingMask.activeSelf)
			return;

		string[] param = IP_Input.text.Split(':');
		if (param.Length != 2)
		{
			ConnectHintText.text = "請輸入正確的 IP:PORT 格式";
			return;
		}

		try
		{
			string ip = param[0];
			int port = Int32.Parse(param[1]);
			NetManager.Instance.Connect(ip, port);
		}
		catch
		{
			ConnectHintText.text = "請輸入正確的 IP:PORT 格式";
			return;
		}

		ConnectingMask.SetActive(true);
	}

	public void ClickSetName()
	{
		if (TopMask.activeSelf)
			return;

		byte[] encodedName = System.Text.Encoding.UTF8.GetBytes(NameInput.text);
		if (encodedName.Length == 0)
		{
			NameHintText.text = "請輸入要使用的名稱";
			return;
		}
		if (encodedName.Length > 255)
		{
			NameHintText.text = "請縮短名稱再試";
			return;
		}

		NetManager.Instance.SendName(encodedName);
		SceneManager.LoadScene("GameScene");
	}
}
