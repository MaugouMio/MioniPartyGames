using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ConnectPage : MonoBehaviour
{
	public static ConnectPage Instance { get; private set; }

	[SerializeField]
	private Text VersionText;
	[SerializeField]
	private Text ConnectHintText;
	[SerializeField]
	private GameObject LicensePage;
	[SerializeField]
	private Text LicenseText;
	[SerializeField]
	private GameObject ConnectingMask;
	[SerializeField]
	private InputField NameInput;
	[SerializeField]
	private ServerPage ServerSubPage;

	void Awake()
	{
		Instance = this;
	}

	// Start is called once before the first execution of Update after the MonoBehaviour is created
	void Start()
    {
		VersionText.text = $"v{GameData.GAME_VERSION}.{GameData.CLIENT_GAME_SUB_VERSION}";
		NameInput.text = PlayerPrefs.GetString("PlayerName", "");
		LicenseText.text = Resources.Load<TextAsset>("LICENSE").text;

		NetManager.Instance.OnDisconnected = OnDisconnected;
    }

	void OnDestroy()
	{
		Instance = null;

		NetManager.Instance.OnDisconnected = null;
	}

	private void OnDisconnected()
	{
		ConnectingMask.SetActive(false);
		SetConnectMessage("連線中斷");
	}

	public void SetConnectMessage(string message)
	{
		ConnectHintText.text = message;
		ServerSubPage.Show(false);
	}

	public void OnVersionCheckResult(uint serverVersion)
	{
		if (serverVersion == GameData.GAME_VERSION)
		{
			NetManager.Instance.SendName(System.Text.Encoding.UTF8.GetBytes(NameInput.text));
			SceneManager.LoadScene("RoomScene");
		}
		else
		{
			ConnectingMask.SetActive(false);
			if (serverVersion < GameData.GAME_VERSION)
				SetConnectMessage("目標伺服器為較古老的版本，無法進行連線");
			else
				SetConnectMessage("遊戲版本不符，請更新遊戲");

			Debug.LogError($"遊戲版本不符，伺服器版本：{serverVersion}, 客戶端版本：{GameData.GAME_VERSION}");
		}
	}

	public void ShowLicense(bool show)
	{
		LicensePage.SetActive(show);
	}

	private bool CheckAndSetName()
	{
		byte[] encodedName = System.Text.Encoding.UTF8.GetBytes(NameInput.text);
		if (encodedName.Length == 0)
		{
			SetConnectMessage("請輸入要使用的名稱");
			return false;
		}
		if (encodedName.Length > 255)
		{
			SetConnectMessage("請縮短名稱再試");
			return false;
		}
		if (NameInput.text.Contains('(') || NameInput.text.Contains(')'))
		{
			SetConnectMessage("名稱包含不允許的字元");
			return false;
		}

		PlayerPrefs.SetString("PlayerName", NameInput.text);
		return true;
	}

	public void ClickLogin()
	{
		if (!CheckAndSetName())
			return;

		ServerSubPage.Show(true);
	}

	public void SelectServer(int index)
	{
		ServerSubPage.SelectServer(index);
	}

	public void ConnectToServer(string ip, int port, bool isWss)
	{
		ConnectingMask.SetActive(true);
		ServerSubPage.Show(false);

		SetConnectMessage("連線中...");
		NetManager.Instance.Connect(ip, port, isWss);
	}
}
