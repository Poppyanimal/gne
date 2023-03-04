using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Open.Nat;
using System.Threading;

public class netScript : MonoBehaviour
{
    string targetIP = "127.0.0.1";
    int targetPort = 25565;

    [SerializeField]
    TMPro.TMP_InputField ipInput, portInput;

    TCPTransport forwarder;

    public void updateIP()
    {
        if(isValidIP(ipInput.text))
            targetIP = ipInput.text;
        else
            ipInput.text = targetIP;
    }
    public void updatePort()
    {
        if(isValidPort(portInput.text))
            targetPort = int.Parse(portInput.text);
        else
            portInput.text = targetPort.ToString();
    }

    public void launchHost()
    {
        Debug.Log("launch host called");
        closeGOF();

        natPunchViaTCP((ushort)targetPort);
        natPunchViaTCP((ushort)(targetPort + 1));

        /*if(forwarder != null)
            forwarder.dumpSockets();*/

        forwarder = new TCPTransport();
        forwarder.runServerForwarder(hostingPort: targetPort);

        swapPIDToHost();
        launchGOF();
    }

    public void launchClient()
    {
        Debug.Log("Launch client called");
        closeGOF();

        /*if(forwarder != null)
            forwarder.dumpSockets();*/

        forwarder = new TCPTransport();
        forwarder.runClientForwarder(targetIP, targetPort);

        swapPIDToClient();
        launchGOF();
    }

    //

    void closeGOF()
    {
        //TODO
    }

    void launchGOF()
    {
        //TODO
    }

    void swapPIDToHost()
    {
        //TODO
    }

    void swapPIDToClient()
    {
        //TODO
    }

    bool isValidIP(string ip)
    {
        return true;
        //TODO
    }

    bool isValidPort(string port)
    {
        return true;
        //TODO
    }





    //
    //netcode
    //

    static async void natPunchViaTCP(ushort portToTry) //UPnP
    {
        Protocol protoToUse = Protocol.Tcp;

        var discoverer = new NatDiscoverer();
        var cts = new CancellationTokenSource(10000);
        var device = await discoverer.DiscoverDeviceAsync(PortMapper.Upnp, cts);

        await device.CreatePortMapAsync(new Mapping(protoToUse, portToTry, portToTry, "temporary mapping from gof launcher - UPnP"));
    }
    
    static async void natPunchViaUDP(ushort portToTry) //UPnP
    {
        Protocol protoToUse = Protocol.Udp;

        var discoverer = new NatDiscoverer();
        var cts = new CancellationTokenSource(10000);
        var device = await discoverer.DiscoverDeviceAsync(PortMapper.Upnp, cts);

        await device.CreatePortMapAsync(new Mapping(protoToUse, portToTry, portToTry, "temporary mapping from gof launcher - UPnP"));
    }
}
