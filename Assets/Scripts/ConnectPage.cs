using System;
using UnityEngine;
using UnityEngine.UI;

public class ConnectPage : MonoBehaviour
{
	[SerializeField]
	private InputField InputIP;
	[SerializeField]
	private Text ConnectHintText;

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
			ClickConnect();
    }

	void OnDestroy()
	{
		NetManager.Instance.OnConnected = null;
		NetManager.Instance.OnDisconnected = null;
	}

	private void OnConnected()
	{
		ConnectHintText.text = "連線成功";
	}

	private void OnDisconnected()
	{
		ConnectHintText.text = "連線中斷";
	}

	public void ClickConnect()
	{
		string[] param = InputIP.text.Split(':');
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

		ConnectHintText.text = "連線中";
	}
}
