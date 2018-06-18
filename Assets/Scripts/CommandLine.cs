using UnityEngine;
using UnityEngine.UI;

public class CommandLine : MonoBehaviour {

	#region Global Variables
	public Text TextPrefab;
	public InputField InputField;
	public GameObject Overlay;
	public GameObject Content;
	public ScrollRect ScrollRect;
	private bool _enabled = false;
	private bool _wasAtBottom = true;
	#endregion

	private void OnEnable()
	{
		_enabled = true;
		Overlay.SetActive(false);
		Debug.Log("enabled");
	}

	private void OnDisable()
	{
		_enabled = false;
		Debug.Log("disabled");
	}

	// Use this for initialization
	private void Start() {

	}

	// Update is called once per frame
	private void Update() {
		if (_enabled)
		{
			if (Input.GetKeyDown(KeyCode.Backslash))
			{
				Overlay.SetActive(!Overlay.activeSelf);
				InputField.text = string.Empty;
				Debug.Log("activate/deactivate");
				if (Overlay.activeSelf)
					InputField.ActivateInputField();
			}

			if (Input.GetKeyDown(KeyCode.Return) && Overlay.activeSelf && InputField.text != string.Empty)
			{
				Log(InputField.text);
				InputField.text = string.Empty;
				Debug.Log("clear");
				InputField.ActivateInputField();
			}
		}
		_wasAtBottom = ScrollRect.verticalNormalizedPosition <= 0.001f;
	}

	private void Log(string text)
	{
		Text newText = Instantiate(TextPrefab);
		newText.text = ">" + text;
		newText.transform.SetParent(Content.transform, false);

		if (_wasAtBottom)
		{
			Canvas.ForceUpdateCanvases();
			ScrollRect.verticalNormalizedPosition = 0.0f;
			Canvas.ForceUpdateCanvases();
		}
	}
}
