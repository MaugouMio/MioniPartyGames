using UnityEngine;
using UnityEngine.UI;

public class ServerListItem : MonoBehaviour
{
	[SerializeField]
	private Text nameText;
	[SerializeField]
	private Toggle toggle;

	private int serverIndex = -1;

	public void SetServerIndex(int index)
	{
		serverIndex = index;
	}

	public void SetServerName(string name)
	{
		nameText.text = name;
	}

	public void Select()
	{
		toggle.isOn = true;
	}

	public void OnSelectChanged(bool isOn)
    {
		if (isOn)
			ConnectPage.Instance.SelectServer(serverIndex);
	}
}
