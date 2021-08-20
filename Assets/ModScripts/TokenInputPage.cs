using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class TokenInputPage : MonoBehaviour
{
    public KMSelectable ConnectButton;
    public KMSelectable RefreshButton;
    public InputField TokenInput;
    public TextMesh ButtonText;
    public TextMesh StatusText;
    public GameObject ErrorText;

    void Start()
    {
        DiscordPlaysService.ws._tokenInputPage = this;
        TokenInput.text = DiscordPlaysService.ws.Token;
        OnEnable();
        ConnectButton.OnInteract += () =>
        {
            if(DiscordPlaysService.ws.CurrentState == WSHandler.WSState.Connected)
                DiscordPlaysService.ws.OnDestroy();
            else
            {
                string token = TokenInput.text;
                if (token.Length == 30)
                {
                    DiscordPlaysService.ws.Token = token;
                    DiscordPlaysService.Connect();
                }
            }
            return false;
        };
        RefreshButton.OnInteract += () =>
        {
            DiscordPlaysService.RefreshSettings();
            return false;
        };
    }

    void OnEnable()
    {
        DiscordPlaysService.ws.PageActive = true;
    }

    void OnDisable()
    {
        DiscordPlaysService.ws.PageActive = false;
    }
    
    void OnDestroy()
    {
        OnDisable();
    }
}
