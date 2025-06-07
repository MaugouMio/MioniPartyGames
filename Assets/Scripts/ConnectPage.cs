using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ConnectPage : MonoBehaviour
{
	public static ConnectPage Instance { get; private set; }

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

	void Awake()
	{
		Instance = this;
	}

	// Start is called once before the first execution of Update after the MonoBehaviour is created
	void Start()
    {
		IP_Input.text = PlayerPrefs.GetString("ServerIP", "");
		NameInput.text = PlayerPrefs.GetString("PlayerName", "");

		//NetManager.Instance.OnConnected = OpenNameWindow;
		NetManager.Instance.OnDisconnected = OnDisconnected;
    }

	void OnDestroy()
	{
		Instance = null;

		//NetManager.Instance.OnConnected = null;
		NetManager.Instance.OnDisconnected = null;
	}

	private void OnDisconnected()
	{
		ConnectingMask.SetActive(false);
		NameWindow.SetActive(false);
		TopMask.SetActive(false);
		SetConnectMessage("連線中斷");
	}

	public void SetConnectMessage(string message)
	{
		ConnectHintText.text = message;
	}

	public void OpenNameWindow()
	{
		//ConnectingMask.SetActive(false);
		NameWindow.SetActive(true);
	}

	public void OnVersionCheckFailed()
	{
		ConnectingMask.SetActive(false);
		SetConnectMessage("遊戲版本不符，請更新遊戲");
	}

	public void ClickConnect()
	{
		if (ConnectingMask.activeSelf)
			return;

		string[] param = IP_Input.text.Split(':');
		if (param.Length != 2)
		{
			SetConnectMessage("請輸入正確的 IP:PORT 格式");
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
			SetConnectMessage("請輸入正確的 IP:PORT 格式");
			return;
		}

		PlayerPrefs.SetString("ServerIP", IP_Input.text);
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

		PlayerPrefs.SetString("PlayerName", NameInput.text);
		NetManager.Instance.SendName(encodedName);
		SceneManager.LoadScene("GameScene");
	}
}
