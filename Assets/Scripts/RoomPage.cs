using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class RoomPage : MonoBehaviour
{
	public static RoomPage Instance { get; private set; }

	[SerializeField]
	private Text serverNameText;

	void Awake()
	{
		Instance = this;
	}

	// Start is called once before the first execution of Update after the MonoBehaviour is created
	void Start()
    {
		serverNameText.text = GameData.Instance.ServerName;
    }

	void OnDestroy()
	{
		Instance = null;
	}

	public void ClickCreateRoom()
	{
		NetManager.Instance.SendCreateRoom();
	}

	public void ClickJoinRoom()
	{
		// TODO: Open room id enter dialog
	}

	public void ClickBackToLogin()
	{
		NetManager.Instance.Disconnect();
	}
}
