using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using TMPro;

public class LobbyManager : NetworkBehaviour
{
    public GameObject playerScrollContent;
    public TMPro.TMP_Text txtPlayerNumber;
    public Button btnStart;
    public Button btnReady;
    public LobbyPlayerPanel playerPanelPrefab;

    public NetworkList<PlayerInfo> allPlayers = new NetworkList<PlayerInfo>();
    private List<LobbyPlayerPanel> playerPanels = new List<LobbyPlayerPanel>();



    private Color[] playerColors = new Color[] {
        Color.blue,
        Color.green,
        Color.yellow,
        Color.magenta,
        Color.cyan
    };
    private int colorIndex = 0;

    public void Start() {
        if (IsHost) {
            RefreshPlayerPanels();
            btnStart.onClick.AddListener(HostOnBtnStartClick);
        }

        if (IsClient) {
            btnReady.onClick.AddListener(ClientOnReadyClicked);
        }
    }

    public override void OnNetworkSpawn() {
        if (IsHost) {
            NetworkManager.Singleton.OnClientConnectedCallback += HostOnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += HostOnClientDisconnected;

            int myIndex = FindPlayerIndex(NetworkManager.LocalClientId);
            if (myIndex != -1)
            {
                PlayerInfo info = allPlayers[myIndex];
                info.isReady = false;
                allPlayers[myIndex] = info;
            }
            AddPlayerToList(NetworkManager.LocalClientId);
        }

        base.OnNetworkSpawn();

        if (IsClient && !IsHost) {
            btnStart.gameObject.SetActive(false);
        }

        txtPlayerNumber.text = $"Player #{NetworkManager.LocalClientId}";
        allPlayers.OnListChanged += ClientOnAllPlayersChanged;
        EnableStartIfAllReady();
    }

    // -----------------------
    // Private
    // -----------------------

    private void AddPlayerToList(ulong clientId)
    {
        allPlayers.Add(new PlayerInfo(clientId, NextColor(), true));
    }
    private void AddPlayerPanel(PlayerInfo info) {
        LobbyPlayerPanel newPanel = Instantiate(playerPanelPrefab);
        newPanel.transform.SetParent(playerScrollContent.transform, false);
        newPanel.SetName($"Player {info.clientId.ToString()}");
        newPanel.SetColor(info.color);
        newPanel.SetReady(info.isReady);
        playerPanels.Add(newPanel);
    }

    private void RefreshPlayerPanels() {
        foreach (LobbyPlayerPanel panel in playerPanels) {
            Destroy(panel.gameObject);
        }
        playerPanels.Clear();

        foreach (PlayerInfo pi in allPlayers) {
            AddPlayerPanel(pi);
        }
    }

    private Color NextColor()
    {
        Color newColor = playerColors[colorIndex];
        colorIndex += 1;
        if (colorIndex > playerColors.Length - 1)
        {
            colorIndex = 0;
        }
        return newColor;
    }

    public int FindPlayerIndex(ulong clientId)
    {
        var idx = 0;
        var found = false;

        while (idx < allPlayers.Count && !found)
        {
            if (allPlayers[idx].clientId == clientId)
            {
                found = true;
            }
            else
            {
                idx += 1;
            }
        }

        if (!found)
        {
            idx = -1;
        }

        return idx;
    }

    private void EnableStartIfAllReady() {
        int readyCount = 0;
        foreach (PlayerInfo readyInfo in allPlayers) {
            if (readyInfo.isReady) {
                readyCount += 1;
            }
        }

        btnStart.enabled = readyCount == allPlayers.Count;
        if (btnStart.enabled) {
            btnStart.GetComponentInChildren<TextMeshProUGUI>().text = "Start";
        } else {
            btnStart.GetComponentInChildren<TextMeshProUGUI>().text = "<Waiting for Ready>";
        }
    }

    // -----------------------
    // Events
    // -----------------------
    private void ClientOnAllPlayersChanged(NetworkListEvent<PlayerInfo> changeEvent) {
        RefreshPlayerPanels();
    }

    private void HostOnBtnStartClick() {
        StartGame();     
    }

    private void HostOnClientConnected(ulong clientId) {
        AddPlayerToList(clientId);
        EnableStartIfAllReady();
    }

    private void HostOnClientDisconnected(ulong clientId)
    {
        int index = FindPlayerIndex(clientId);
        if (index != -1)
        {
            allPlayers.RemoveAt(index);
            RefreshPlayerPanels();
        }
    }

    private void ClientOnReadyClicked() {
        ToggleReadyServerRpc();
    }


    // -----------------------
    // Public
    // -----------------------

   

    public override void OnDestroy()
    {
        allPlayers.Dispose();
    }
    public void StartGame(){
        var scene = NetworkManager.SceneManager.LoadScene("TheGameScene", UnityEngine.SceneManagement.LoadSceneMode.Single );
        btnStart.enabled = false;
    }

    [ServerRpc(RequireOwnership = false)]
    public void ToggleReadyServerRpc(ServerRpcParams serverRpcParams = default) {
        ulong clientId = serverRpcParams.Receive.SenderClientId;
        int playerIndex = FindPlayerIndex(clientId);
        PlayerInfo info = allPlayers[playerIndex];

        info.isReady = !info.isReady;
        allPlayers[playerIndex] = info;

        EnableStartIfAllReady();
    }

}
