using UnityEngine;
using System;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Generic;
using System.Threading.Tasks;

public enum PROTOCOL_CLIENT
{
	NAME,
	JOIN,
	LEAVE,
	START,
	CANCEL_START,
	QUESTION,
	GUESS,
	VOTE,
}

public enum PROTOCOL_SERVER
{
	INIT,
	CONNECT,
	DISCONNECT,
	NAME,
	JOIN,
	LEAVE,
	START_COUNTDOWN,
	START,
	GAMESTATE,
	PLAYER_ORDER,
	QUESTION,
	SUCCESS,
	GUESS,
	VOTE,
	GUESS_AGAIN,
	GUESS_RECORD,
	END,
}

public class NetPacket
{
	public const int HEADER_SIZE = 5;

	public byte protocol;
	public int size;

	public byte[] data;

	public byte[] ToBytes()
	{
		byte[] bytes = new byte[HEADER_SIZE + size];
		bytes[0] = protocol;
		Array.Copy(BitConverter.GetBytes(size), 0, bytes, 1, sizeof(int));
		return bytes;
	}
}

public class NetManager : MonoBehaviour
{
	public static NetManager Instance { get; private set; }

	public Action OnConnected;
	public Action OnDisconnected;

	private TcpClient m_Client;
	private Thread m_ReadThread;
	private Queue<NetPacket> m_PacketQueue;

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
	}

	public NetPacket GetPacket()
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
			Debug.Log($"MIO {size} {buffer[0]} {len}");
			if (size - offset < len + NetPacket.HEADER_SIZE)
				break;

			NetPacket packet = new NetPacket();
			packet.protocol = buffer[offset];
			packet.size = len;
			packet.data = new byte[len];
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
				Debug.Log($"MIO {m_Client.GetStream().CanRead} {len}");
				if (len > 0)
				{
					offset += len;
					GeneratePacket(buffer, ref offset);
				}
			}
			catch (Exception e)
			{
				Debug.Log(e.ToString());
			}
		}
	}
}
