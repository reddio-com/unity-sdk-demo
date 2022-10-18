using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Reddio.Api.V1;
using Reddio.Crypto;
using TMPro;
using UnityEngine;
using WalletConnectSharp.Core.Models;
using WalletConnectSharp.Core.Models.Ethereum;
using WalletConnectSharp.Unity;

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
            await this.wc.Connect();
        }
        else if (state == 0 && WalletConnect.ActiveSession.Accounts != null &&
                 WalletConnect.ActiveSession.Accounts.Length > 0)
        {
            state = -1;
            await this.Disconnect();
            state = 0;
        }
        else if (state == 0 && WalletConnect.ActiveSession.ReadyForUserPrompt)
        {
            await this.Connect();
            state = 1;
        }
        else if (WalletConnect.ActiveSession.Accounts != null && state == 1)
        {
            state = 2;
            this.logText.text += "Connceted\n";
            this.GetStarkKey();
        }
    }


    public async Task Connect()
    {
        if (this.wc == null)
        {
            this.wcGameObject = Instantiate(this.walletConnectPrefab, Vector3.zero, Quaternion.identity);
            this.wc = this.wcGameObject.GetComponent<WalletConnect>();
            await this.wc.Connect();
        }

        this.wc.OpenDeepLink();
    }

    public void ReConnect()
    {
        this.Disconnect();
        this.wc.OpenDeepLink();
    }

    public async Task Disconnect()
    {
        if (this.wcGameObject != null)
        {
            await WalletConnect.ActiveSession.Disconnect();
            Destroy(this.wcGameObject);
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

    public void GetRandomStarkKey()
    {
        var randomPrivateKey = CryptoService.GetRandomPrivateKey();
        var publicKey = CryptoService.GetPublicKey(randomPrivateKey);

        logText.text +=
            $"Private Key:\n{randomPrivateKey.ToString("x")}\nPublic Key/Stark Key:\n{publicKey.ToString("x")}\n";

        Debug.Log("Private Key");
        Debug.Log(randomPrivateKey.ToString("x"));
        Debug.Log("Public Key");
        Debug.Log(publicKey.ToString("x"));
    }

    public async void Trasnfer()
    {
        var client = ReddioClient.Testnet();
        var starkKey = "0x6736f7449da3bf44bf0f7bdd6463818e1ef272641d43021e8bca17b32ec2df0";
        var tokenId = "500";
        var receiver = "0x7865bc66b610d6196a7cbeb9bf066c64984f6f06b5ed3b6f5788bd9a6cb099c";
        var amount = "1";

        logText.text += String.Format("Would transfer ERC721 from {0} to {1}, amount {2}, tokenId {3}\n", starkKey,
            receiver, amount, tokenId);
        var result = await client.Transfer(
            starkKey,
            "0xa7b68cf2ee72b2a0789914daa8ae928aec21b6b0bf020e394833f4c732d99d",
            amount,
            "0x941661bd1134dc7cc3d107bf006b8631f6e65ad5",
            tokenId,
            "ERC721",
            receiver
        );
        logText.text += "Transfer Requested, sequence:" + result.Data.SequenceId + "\n";
        logText.text += JsonConvert.SerializeObject(result) + "\n";
        logText.text += "Waiting Transfer get Accepted, sequence:" + result.Data.SequenceId + "\n";
        var waitingTransfer =
            await client.WaitingTransferGetApproved(starkKey,
                300523);
        logText.text += "Transfer Accepted, sequence:" + waitingTransfer.Data[0].SequenceId + "\n";
        logText.text += JsonConvert.SerializeObject(waitingTransfer) + "\n";
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