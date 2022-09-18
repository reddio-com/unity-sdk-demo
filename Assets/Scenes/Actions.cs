using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Reddio.Crypto;
using TMPro;
using UnityEngine;
using WalletConnectSharp.Core.Models;
using WalletConnectSharp.Core.Models.Ethereum;
using WalletConnectSharp.Core.Models.Ethereum.Types;
using WalletConnectSharp.Unity;
using WalletConnectUnity.Demo.Scripts;

public class Actions : MonoBehaviour
{
    const int STATE_IDLE = 0;
    private const int STATE_WALLET_CONNECT_LOADED = 1;
    private const int STATE_WALLET_CONNECT_INITIALIZED = 2;
    private const int STATE_WALLET_CONNECT_CONNECTED = 3;
    public GameObject walletConnectPrefab;
    public TextMeshProUGUI accountText;
    public TextMeshProUGUI logText;

    private GameObject wcGameObject;
    private WalletConnect wc = null;
    private int state = 0;

    async void Update()
    {
        if (this.wcGameObject == null || WalletConnect.ActiveSession == null ||
            WalletConnect.ActiveSession.Accounts == null)
        {
            accountText.text = "Waiting for Connection";
        }
        else
        {
            accountText.text = "\nConnected to Chain " + WalletConnect.ActiveSession.ChainId + ":\n" +
                               WalletConnect.ActiveSession.Accounts[0];
        }

        if (state == 0 && this.wcGameObject == null)
        {
            this.wcGameObject = Instantiate(this.walletConnectPrefab, Vector3.zero, Quaternion.identity);
            this.wc = this.wcGameObject.GetComponent<WalletConnect>();
        }
        else if (state == 0 && WalletConnect.ActiveSession.Accounts != null &&
                 WalletConnect.ActiveSession.Accounts.Length > 0)
        {
            state = -1;
            this.Disconnect();
            state = 0;
        }
        else if (state == 0 && WalletConnect.ActiveSession.ReadyForUserPrompt)
        {
            this.Connect();
            state = 1;
        }
        else if (WalletConnect.ActiveSession.Accounts != null && state == 1)
        {
            state = 2;
            this.logText.text += "Connceted\n";
            this.GetStarkKey();
        }
    }


    public void Connect()
    {
        if (this.wc == null)
        {
            this.wcGameObject = Instantiate(this.walletConnectPrefab, Vector3.zero, Quaternion.identity);
            this.wc = this.wcGameObject.GetComponent<WalletConnect>();
        }

        this.wc.OpenDeepLink();
    }

    public void ReConnect()
    {
        this.Disconnect();
        this.wc.OpenDeepLink();
    }

    public async void Disconnect()
    {
        if (this.wcGameObject != null)
        {
            Destroy(this.wcGameObject);
            await WalletConnect.ActiveSession.Disconnect();
            this.wcGameObject = null;
            this.wc = null;
        }
    }

    public async Task<string> SignTypedData<T>(T data, EIP712Domain eip712Domain, int addressIndex = 0)
    {
        var address = WalletConnect.ActiveSession.Accounts[addressIndex];

        var results = await WalletConnect.ActiveSession.EthSignTypedData(address, data, eip712Domain);

        return results;
    }

    public async void GetStarkKey()
    {
        var address = WalletConnect.ActiveSession.Accounts[0];
        var payload = new ReddioSign(address, "Generate layer 2 key", 5);
        var response = await WalletConnect.ActiveSession.Send<ReddioSign, EthResponse>(payload);
        Debug.Log("Reddio Sign Completed");
        Debug.Log(response.Result);

        var privateKey = CryptoService.GetPrivateKeyFromEthSignature(response.Result);
        var publicKey = CryptoService.GetPublicKey(privateKey);

        logText.text += $"Private Key:\n{privateKey.ToString("x")}\nPublic Key/Stark Key:\n{publicKey.ToString("x")}\n";

        Debug.Log("Private Key");
        Debug.Log(privateKey.ToString("x"));
        Debug.Log("Public Key");
        Debug.Log(publicKey.ToString("x"));
    }

    public class ReddioSign : JsonRpcRequest
    {
        [JsonProperty("params")] private string[] _parameters;

        public ReddioSign(string address, string message, int chainId)
        {
            this.Method = "eth_signTypedData_v4";

            var typeData = ReddioSignPayload.Create(message, chainId);
            var encodedTypeData = JsonConvert.SerializeObject(typeData);
            Debug.Log("encodedTypeData");
            Debug.Log(encodedTypeData);

            this._parameters = new string[] { address, encodedTypeData };
        }
    }

    public class ReddioSignPayload
    {
        [JsonProperty("domain")] public ReddioSignPayloadDomain Domain;
        [JsonProperty("message")] public ReddioSignPayloadMessage Message;
        [JsonProperty("primaryType")] public string PrimaryType;
        [JsonProperty("types")] public Dictionary<string, List<ReddioSignPayloadTypesEntry>> Types;

        private ReddioSignPayload(ReddioSignPayloadDomain domain, ReddioSignPayloadMessage message, string primaryType,
            Dictionary<string, List<ReddioSignPayloadTypesEntry>> types)
        {
            Domain = domain;
            Message = message;
            PrimaryType = primaryType;
            Types = types;
        }

        public static ReddioSignPayload Create(string message, int chainId)
        {
            return new ReddioSignPayload(
                new ReddioSignPayloadDomain(chainId),
                new ReddioSignPayloadMessage(message),
                "reddio",
                new Dictionary<string, List<ReddioSignPayloadTypesEntry>>()
                {
                    {
                        "EIP712Domain", new List<ReddioSignPayloadTypesEntry>()
                        {
                            new("chainId", "uint256")
                        }
                    },
                    {
                        "reddio", new List<ReddioSignPayloadTypesEntry>()
                        {
                            new("contents", "string")
                        }
                    }
                }
            );
        }
    }

    public class ReddioSignPayloadMessage
    {
        public ReddioSignPayloadMessage(string contents)
        {
            Contents = contents;
        }

        [JsonProperty("contents")] public string Contents;
    }
}

public class ReddioSignPayloadTypesEntry
{
    [JsonProperty("name")] public string Name;
    [JsonProperty("type")] public string Type;

    public ReddioSignPayloadTypesEntry(string name, string type)
    {
        Name = name;
        Type = type;
    }
}

public class ReddioSignPayloadDomain
{
    [JsonProperty("chainId")] public int ChainId;

    public ReddioSignPayloadDomain(int chainId)
    {
        ChainId = chainId;
    }
}