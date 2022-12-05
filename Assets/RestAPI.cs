using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Reddio.Api.V1;
using reddio.api.V2.Rest;
using reddio.unity.V2;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RestAPI : MonoBehaviour
{
    public TextMeshProUGUI m_text;

    // Start is called before the first frame update
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
    }

    public void OnClick()
    {
        StartCoroutine(UpdateStarkexContract());
    }

    public IEnumerator UpdateStarkexContract()
    {
        var reddioRestClient = ReddioRestClient.Testnet();
        var unityClient = new ReddioUnityRestClient(reddioRestClient);
        Debug.Log("1 - current thread: " + System.Threading.Thread.CurrentThread.ManagedThreadId);

        yield return unityClient.GetBalance(
            new GetBalanceMessage("0x1c2847406b96310a32c379536374ec034b732633e8675860f20f4141e701ff4", ""),
            (balance) =>
            {
                Debug.Log("2 - current thread: " + System.Threading.Thread.CurrentThread.ManagedThreadId);
                Debug.Log("balance: " + balance);
            },
            (error) => { });
    }
}