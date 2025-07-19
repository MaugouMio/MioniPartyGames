using NativeWebSocket;
using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

public partial class NetManager : MonoBehaviour
{
	public static NetManager Instance { get; private set; }

	public Action OnConnected;
	public Action OnDisconnected;

	private WebSocket m_WebSocket;

	void Awake()
	{
		if (Resources.FindObjectsOfTypeAll<NetManager>().Length > 1)
		{
			gameObject.SetActive(false);
			Destroy(gameObject);
		}
		else
		{
			Instance = this;
			DontDestroyOnLoad(gameObject);
		}
	}

	void Update()
{
#if !UNITY_WEBGL || UNITY_EDITOR
		if (m_WebSocket != null)
			m_WebSocket.DispatchMessageQueue();
#endif
}

	async void OnDestroy()
	{
		await Disconnect();
	}

	public async void Connect(string host, int port)
	{
		if (m_WebSocket != null)
		{
			Debug.LogWarning("WebSocket is already connected.");
			return;
		}

		try
		{
			m_WebSocket = new WebSocket($"ws://{host}:{port}");

			m_WebSocket.OnOpen += () =>
			{
				OnConnected?.Invoke();
				SendVersionCheck();
			};

			m_WebSocket.OnError += (e) =>
			{
				Debug.LogError($"WebSocket error: {e}");
				OnDisconnect();
			};

			m_WebSocket.OnClose += (e) =>
			{
				Debug.Log($"WebSocket connection closed: {e}");
				OnDisconnect();
			};

			m_WebSocket.OnMessage += (bytes) =>
			{
				int dataLen = bytes.Length - 1;
				NetPacket packet = new NetPacket(bytes[0], new byte[dataLen]);
				Array.Copy(bytes, NetPacket.HEADER_SIZE, packet.data, 0, dataLen);
				ProcessReceive(packet);
			};

			await m_WebSocket.Connect();
		}
		catch (Exception e)
		{
			Debug.LogError($"WebSocket connection error: {e.Message}");
			await Disconnect();
		}
	}

	private void OnDisconnect()
	{
		OnDisconnected?.Invoke();
		if (SceneManager.GetActiveScene().name != "LoginScene")
			SceneManager.LoadScene("LoginScene");

		m_WebSocket = null;
	}

	public async Task Disconnect()
	{
		if (m_WebSocket == null)
			return;

		await m_WebSocket.Close();
		OnDisconnect();
	}

	public void SendPacket(NetPacket packet)
	{
		if (m_WebSocket == null)
			return;
		if (m_WebSocket.State != WebSocketState.Open)
			return;

		m_WebSocket.Send(packet.ToBytes());
	}
}
