using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

public partial class NetManager : MonoBehaviour
{
	public static NetManager Instance { get; private set; }

	public Action OnConnected;
	public Action OnDisconnected;

	private TcpClient m_Client;
	private Thread m_ReadThread;
	private Queue<NetPacket> m_PacketQueue;

	private bool isDisconnected = false;

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
			ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
		}
	}

	void Update()
	{
		while (true)
		{
			NetPacket packet = GetPacket();
			if (packet == null)
				break;

			ProcessReceive(packet);
		}

		Thread.MemoryBarrier();
		if (isDisconnected)
		{
			Disconnect();
			isDisconnected = false;
		}
		Thread.MemoryBarrier();
	}

	void OnDestroy()
	{
		Disconnect();
	}

	public async void Connect(string host, int port)
	{
		if (m_Client != null)
			return;

		try
		{
			m_Client = new TcpClient();
			try
			{
				var task = m_Client.ConnectAsync(host, port);
				await Task.WhenAny(task, Task.Delay(5000));
				if (task.IsFaulted || !task.IsCompleted)
				{
					Disconnect();
					return;
				}
			}
			catch
			{
				Debug.Log("Connect failed");
			}

			m_ReadThread = new Thread(ReadProcess);
			m_ReadThread.IsBackground = true;
			m_ReadThread.Start();

			m_PacketQueue = new Queue<NetPacket>();
			OnConnected?.Invoke();
			SendVersionCheck();
		}
		catch
		{
			Disconnect();
		}
	}

	public void Disconnect()
	{
		if (m_Client != null)
		{
			m_Client.Close();
			m_Client = null;
		}
		if (m_ReadThread != null)
		{
			m_ReadThread.Abort();
			m_ReadThread = null;
		}
		OnDisconnected?.Invoke();

		if (SceneManager.GetActiveScene().name != "LoginScene")
			SceneManager.LoadScene("LoginScene");
	}

	private NetPacket GetPacket()
	{
		NetPacket packet = null;

		Thread.MemoryBarrier();
		if (m_PacketQueue != null && m_PacketQueue.Count > 0)
			packet = m_PacketQueue.Dequeue();
		Thread.MemoryBarrier();

		return packet;
	}

	public void SendPacket(NetPacket packet)
	{
		if (m_Client == null)
			return;
		if (!m_Client.Connected)
			return;

		m_Client.GetStream().Write(packet.ToBytes());
	}

	private void GeneratePacket(byte[] buffer, ref int size)
	{
		int offset = 0;
		while (size - offset >= NetPacket.HEADER_SIZE)  // header size
		{
			int len = BitConverter.ToInt32(buffer, offset + 1);
			if (size - offset < len + NetPacket.HEADER_SIZE)
				break;

			NetPacket packet = new NetPacket(buffer[offset], len, new byte[len]);
			Array.Copy(buffer, offset + NetPacket.HEADER_SIZE, packet.data, 0, len);

			offset += len + NetPacket.HEADER_SIZE;

			Thread.MemoryBarrier();
			m_PacketQueue?.Enqueue(packet);
			Thread.MemoryBarrier();
		}

		if (offset > 0)
		{
			// move elements
			Array.Copy(buffer, offset, buffer, 0, size - offset);
			size -= offset;
		}
	}

	private void ReadProcess()
	{
		byte[] buffer = new byte[m_Client.ReceiveBufferSize];
		int offset = 0;
		while (true)
		{
			if (!m_Client.Connected)
			{
				Thread.Sleep(500);
				continue;
			}

			try
			{
				int len = m_Client.GetStream().Read(buffer, offset, m_Client.ReceiveBufferSize - offset);
				if (len > 0)
				{
					offset += len;
					GeneratePacket(buffer, ref offset);
				}
			}
			catch (Exception e)
			{
				Debug.Log(e.ToString());

				Thread.MemoryBarrier();
				isDisconnected = true;
				Thread.MemoryBarrier();
			}
		}
	}
}
