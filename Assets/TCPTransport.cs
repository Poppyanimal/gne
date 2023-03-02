using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using System;
//using Unity.Collections;
using UnityEngine;
//using Unity.Networking.Transport;
//using Unity.Networking.Transport.Relay;
//using Unity.Networking.Transport.Utilities;

public class TCPTransport
{
    //assumption:
    //using "using" for sockets means I dont have to do
    //socket.Shutdown(SocketShutdown.Both); or
    //socket.Close();
    //because the using will handle it itself
    Socket externalListener, internalClient, externalClient, internalListener;

    int maxHashMemory = 20;
    int packetsToSpam = 5;
    List<ushort> hashMemory = new List<ushort>();

    System.Random rand = new();

    public async void runServerForwarder(int hostingPort = 25565, int gofPort = 35751)
    {
        //external hosting via tcp
        IPAddress extIP = IPAddress.Any;
        int extPort = hostingPort; //what the other player is looking for w/ their launcher
        IPEndPoint extEndPoint = new IPEndPoint(extIP, extPort);

        externalListener
            = new(extIP.AddressFamily, SocketType.Stream, ProtocolType.Unspecified);

        externalListener.Bind(extEndPoint);
        externalListener.Listen(100);

        Debug.Log("waiting for external connection");
        var externalHandler = await externalListener.AcceptAsync();
        Debug.Log("external connection found");

        //internal client spoof via udp
        IPAddress internIP = IPAddress.Loopback;
        int internPort = gofPort; //where gleam of force is hosting from
        IPEndPoint internEndPoint = new IPEndPoint(internIP, internPort);

        internalClient
            = new(internIP.AddressFamily, SocketType.Stream, ProtocolType.Unspecified);

        Debug.Log("waiting to connect to internal client");
        await internalClient.ConnectAsync(internEndPoint);
        Debug.Log("internal client found");

        //packet stuff
        //asumption: an async method will not be waited for completion before the next set of code runs
        forwardExternalForServerAsync(externalHandler, internalClient);
        forwardInternalForServerAsync(externalHandler, internalClient);
    }

    public async void runClientForwarder(string ipToConnectTo, int hostPort = 25565, int gofPort = 35751)
    {
        //internal hosting spoof via udp
        IPAddress internIP = IPAddress.Loopback;
        int internPort = gofPort; //where gleam of force is trying to connect to
        IPEndPoint internEndPoint = new IPEndPoint(internIP, internPort);

        internalListener
            = new(internEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Unspecified);

        internalListener.Bind(internEndPoint);
        internalListener.Listen(100);

        Debug.Log("waiting for internal client to be found");
        var internalHandler = await internalListener.AcceptAsync();
        Debug.Log("internal client connected");

        //external client via tcp
        IPAddress extIP = IPAddress.Parse(ipToConnectTo);
        int extPort = hostPort; //what the other player is looking for w/ their launcher
        IPEndPoint extEndPoint = new IPEndPoint(extIP, extPort);

        externalClient
            = new(extIP.AddressFamily, SocketType.Stream, ProtocolType.Unspecified);

        Debug.Log("waiting to connect to external server");
        await externalClient.ConnectAsync(extEndPoint);
        Debug.Log("externel server found");

        //packet stuff
        //asumption: an async method will not be waited for completion before the next set of code runs
        forwardExternalForClientAsync(externalClient, internalHandler);
        forwardInternalForClientAsync(externalClient, internalHandler);
    }

    //
    //
    //

    async void forwardExternalForServerAsync(Socket externalHandler, Socket internalClient)
    {
        Debug.Log("forwarding external for server started");
        while(true)
        {
            var bufferExt = new byte[1_024]; //byte buffer of 1,024 bytes
            var receivedExt = await externalHandler.ReceiveAsync(bufferExt, SocketFlags.None);

            if(receivedExt > 2)
            {
                var signatureBytes = new byte[2];
                Array.Copy(bufferExt, signatureBytes, 2);

                ushort signature = BitConverter.ToUInt16(signatureBytes);

                if(!hashMemory.Contains(signature))
                {
                    hashMemory.Add(signature);
                    if(hashMemory.Count > maxHashMemory)
                        hashMemory.RemoveAt(0);

                    var dataBytes = new byte[receivedExt - 2];
                    Array.Copy(bufferExt, 2, dataBytes, 0, receivedExt);

                    await internalClient.SendAsync(dataBytes, SocketFlags.None);
                }

            }
            else
            {
                Debug.LogError("Server External -> Internal packet size less than 3 at "+receivedExt+" ; discarding packet");
            }
        }

    }

    async void forwardInternalForServerAsync(Socket externalHandler, Socket internalClient)
    {
        Debug.Log("forwarding internal for server started");
        while(true)
        {
            var bufferExt = new byte[1_024]; //byte buffer of 1,024 bytes
            var receivedExt = await internalClient.ReceiveAsync(bufferExt, SocketFlags.None);

            var dataBytesWSignature = new byte[receivedExt+2];
            Array.Copy(bufferExt, 0, dataBytesWSignature, 2, receivedExt);

            var signatureBytes = new byte[2];
            rand.NextBytes(signatureBytes);
            Array.Copy(signatureBytes, 0, dataBytesWSignature, 0, 2);

            for(int i = 0; i < packetsToSpam; i++)
                await externalHandler.SendAsync(dataBytesWSignature, SocketFlags.None);
        }
    }

    async void forwardExternalForClientAsync(Socket externalClient, Socket internalServer)
    {
        Debug.Log("forwarding external for client started");
        while(true)
        {
            var bufferExt = new byte[1_024]; //byte buffer of 1,024 bytes
            var receivedExt = await externalClient.ReceiveAsync(bufferExt, SocketFlags.None);

            if(receivedExt > 2)
            {
                var signatureBytes = new byte[2];
                Array.Copy(bufferExt, signatureBytes, 2);

                ushort signature = BitConverter.ToUInt16(signatureBytes);

                if(!hashMemory.Contains(signature))
                {
                    hashMemory.Add(signature);
                    if(hashMemory.Count > maxHashMemory)
                        hashMemory.RemoveAt(0);

                    var dataBytes = new byte[receivedExt - 2];
                    Array.Copy(bufferExt, 2, dataBytes, 0, receivedExt);

                    await internalServer.SendAsync(dataBytes, SocketFlags.None);
                }

            }
            else
            {
                Debug.LogError("Server External -> Internal packet size less than 3 at "+receivedExt+" ; discarding packet");
            }
        }
    }

    async void forwardInternalForClientAsync(Socket externalClient, Socket internalServer)
    {
        Debug.Log("forwarding internal for client started");
        while(true)
        {
            var bufferExt = new byte[1_024]; //byte buffer of 1,024 bytes
            var receivedExt = await internalServer.ReceiveAsync(bufferExt, SocketFlags.None);

            var dataBytesWSignature = new byte[receivedExt+2];
            Array.Copy(bufferExt, 0, dataBytesWSignature, 2, receivedExt);
            
            var signatureBytes = new byte[2];
            rand.NextBytes(signatureBytes);
            Array.Copy(signatureBytes, 0, dataBytesWSignature, 0, 2);

            for(int i = 0; i < packetsToSpam; i++)
                await externalClient.SendAsync(dataBytesWSignature, SocketFlags.None);
        }
    }

    /*public void dumpSockets()
    {
        List<Socket> sockets = new List<Socket>() { externalClient, externalListener, internalClient, internalListener };

        foreach(Socket socket in sockets)
        {
            if(socket != null)
            {
                try { socket.Shutdown(SocketShutdown.Both); } catch(System.Exception e){ Debug.Log(e); }
            }
        }
    }*/

    //old runServer() snippets

        /*while(true)
        {
            //always assume that the first two bytes of the received packet specify the length of data part of the packet as a ushort

            //receive from external
            var bufferExt = new byte[1_024]; //byte buffer of 1,024 bytes
            var receivedExt = await externalHandler.ReceiveAsync(bufferExt, SocketFlags.None);

            //assumption: receivedExt updates even when reading isnt done
            //maybe a bad assumption?

            //send from external to internal
            if(receivedExt >= 2) //aka, all the length data has now been received
            {
                byte[] lengthInfo = new byte[2];
                Array.Copy(bufferExt, lengthInfo, 2);
                ushort dataLength = BitConverter.ToUInt16(lengthInfo, 0);

                if(receivedExt == dataLength + 2) //all data is present, forward the new data to internal
                {
                    var bufferToSend = new byte[dataLength];
                    Array.Copy(bufferExt, 2, bufferToSend, 0, dataLength);


                }
                else
                    Debug.Log("length info is different than currently availabled buffer data!!!   the int does update w/o being finished!");
            }

            
            //receive from internal
            var bufferIntern = new byte[1_024];
            var receivedIntern = await internalClient.ReceiveAsync(bufferIntern, SocketFlags.None);
            Debug.Log("current received internal packet byte length:   "+receivedIntern);

            //assumption: lets assume it always finishes reading all data before continuing and conditnue as if it is


        }*/


    ///
    ///
    ///
    ///
    ///


    /*
    public NetworkDriver driver;
    public NativeList<NetworkConnection> connections;

    public void bindToPort(ushort port)
    {
        NetworkSettings set = new NetworkSettings();
        driver = NetworkDriver.Create();
        var endpoint = NetworkEndPoint.AnyIpv4;
        endpoint.Port = port;
        if(driver.Bind(endpoint) != 0)
            Debug.LogError("Failed to bind to port "+port);
        else
            driver.Listen();

        connections = new NativeList<NetworkConnection>(4, Allocator.Persistent);
    }

    public void OnDestroy()
    {
        if(driver.IsCreated)
        {
            driver.Dispose();
            connections.Dispose();
        }
    }

    void Update()
    {
        driver.ScheduleUpdate().Complete();

        for(int i = connections.Length - 1; i >= 0; i--)
            if(!connections[i].IsCreated)
                connections.RemoveAtSwapBack(i);

        NetworkConnection c;
        while ((c = driver.Accept()) != default(NetworkConnection))
        {
            connections.Add(c);
            Debug.Log("A Connection Was Accepted");
        }

        DataStreamReader stream;
        for(int i = 0; i < connections.Length; i++)
        {
            if(!connections[i].IsCreated)
                continue;

            NetworkEvent.Type cmd;
            while((cmd = driver.PopEventForConnection(connections[i], out stream)) != NetworkEvent.Type.Empty)
            {
                if(cmd == NetworkEvent.Type.Data)
                {
                    //assume is uint
                    uint number = stream.ReadUInt();
                    Debug.Log("Got " + number + " from the Client adding + 2 to it.");

                    number += 2;

                    driver.BeginSend(NetworkPipeline.Null, connections[i], out var writer);
                    writer.WriteUInt(number);
                    driver.EndSend(writer);
                }
                else if(cmd == NetworkEvent.Type.Disconnect)
                {
                    Debug.Log("Client disconnected from server");
                    connections[i] = default(NetworkConnection);
                }
            }
        }
    }*/
    
}
