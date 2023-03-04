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

public class TCPTransport //TODO: better name
{
    //assumption:
    //using "using" for sockets means I dont have to do
    //socket.Shutdown(SocketShutdown.Both); or
    //socket.Close();
    //because the using will handle it itself
    Socket externalListener, internalClient, externalClient, internalListener,
        udp_externalListener, /*udp_internalClient,*/ udp_externalClient/*, udp_internalListener*/;

    public async void runServerForwarder(int hostingPort = 25565, int gofPort = 35751) //assume that the hosting port will also be accompanied by that port + 1 being open
    {
        //TODO: have both the TCP over TCP and the UDP over TCP on the same port instead of seperate ones

        IPAddress extIP = IPAddress.Any;
        int extPort = hostingPort; //what the other player is looking for w/ their launcher
        IPEndPoint extEndPoint = new IPEndPoint(extIP, extPort);

        externalListener
            = new(extIP.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

        externalListener.Bind(extEndPoint);
        externalListener.Listen(100);

        Debug.Log("waiting for external TCP connection");
        var externalHandler = await externalListener.AcceptAsync();
        Debug.Log("external connection TCP found");

        IPAddress internIP = IPAddress.Loopback;
        int internPort = gofPort; //where gleam of force is hosting from
        IPEndPoint internEndPoint = new IPEndPoint(internIP, internPort);

        internalClient
            = new(SocketType.Stream, ProtocolType.Tcp);

        Debug.Log("Cconnecting to internal client over TCP");
        await internalClient.ConnectAsync(internEndPoint);
        Debug.Log("internal client connected over TCP");

        forwardDataInAsync(externalHandler, internalClient);
        forwardDataOutAsync(externalHandler, internalClient);

        //
        //start of udp forwarded to tcp stuff instead of just wrapping tcp in tcp
        //

        //the port that the udp data will be forwarded on instead of the tcp above
        IPEndPoint udp_ExtEndPoint = new IPEndPoint(extIP, extPort + 1);

        udp_externalListener
            = new(extIP.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

        udp_externalListener.Bind(udp_ExtEndPoint);
        udp_externalListener.Listen(100);

        Debug.Log("waiting for external UDP over TCP connection");
        var udp_ExternalHandler = await udp_externalListener.AcceptAsync();
        Debug.Log("external UDP over TCP connection found");

        UdpClient localUDPCon = new UdpClient(internEndPoint);

        Debug.Log("passing local UDP connection's old implementation. UDP will be handled in forwarding");
        /*udp_internalClient
            = new(SocketType.Dgram, ProtocolType.Udp);

        Debug.Log("Connect to internal client over UDP");
        await udp_internalClient.ConnectAsync(internEndPoint);
        Debug.Log("internal client connected over UDP");*/

        forwardDataInUDPAsync(udp_ExternalHandler, localUDPCon);
        forwardDataOutUDPAsync(udp_ExternalHandler, localUDPCon);
    }

    public async void runClientForwarder(string ipToConnectTo, int hostPort = 25565, int gofPort = 35751)
    {
        IPAddress internIP = IPAddress.Loopback;
        int internPort = gofPort; //where gleam of force is trying to connect to
        IPEndPoint internEndPoint = new IPEndPoint(internIP, internPort);

        internalListener
            = new(SocketType.Stream, ProtocolType.Tcp);

        internalListener.Bind(internEndPoint);
        internalListener.Listen(100);

        Debug.Log("waiting for internal client to be found over TCP");
        var internalHandler = await internalListener.AcceptAsync();
        Debug.Log("internal client connected over TCP");

        IPAddress extIP = IPAddress.Parse(ipToConnectTo);
        int extPort = hostPort; //what the other player is looking for w/ their launcher
        IPEndPoint extEndPoint = new IPEndPoint(extIP, extPort);

        externalClient
            = new(extIP.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

        Debug.Log("Connecting to external server for TCP over TCP");
        await externalClient.ConnectAsync(extEndPoint);
        Debug.Log("Externel server found and connected for TCP over TCP");

        forwardDataInAsync(externalClient, internalHandler);
        forwardDataOutAsync(externalClient, internalHandler);
        
        //
        //start of udp forwarded to tcp stuff instead of just wrapping tcp in tcp
        //
        IPEndPoint udp_ExtEndPoint = new IPEndPoint(extIP, extPort + 1);

        UdpClient localUDPCon = new UdpClient(internEndPoint);
            
        /*udp_internalListener
            = new(SocketType.Dgram, ProtocolType.Udp);

        udp_internalListener.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.ReuseAddress, true);
            
        udp_internalListener.Bind(internEndPoint);*/

        Debug.Log("passing local UDP connection's old implementation. UDP will be handled in forwarding");
        //Debug.Log("waiting for internal client to connect over udp");
        //var udp_InternalHandler = await udp_internalListener.ReceiveAsync();
        //Debug.Log("internal client connection over udp established");
        
        udp_externalClient
            = new(extIP.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

        Debug.Log("Connecting to external server on UDP over TCP");
        await udp_externalClient.ConnectAsync(udp_ExtEndPoint);
        Debug.Log("Connected to external server on UDP over TCP");

        forwardDataInUDPAsync(udp_externalClient, localUDPCon);
        forwardDataOutUDPAsync(udp_externalClient, localUDPCon);
    }

    //
    //
    //

    async void forwardDataInAsync(Socket externSocket, Socket internSocket)
    {
        Debug.Log("forwarding data in started");
        while(true)
        {
            var bufferExt = new byte[1_024]; //byte buffer of 1,024 bytes
            var receivedExt = await externSocket.ReceiveAsync(bufferExt, SocketFlags.None);

            var dataBytes = new byte[receivedExt];
            Array.Copy(bufferExt, dataBytes, receivedExt);

            //Debug.Log("inbound network packet: "+BitConverter.ToString(dataBytes));

            await internSocket.SendAsync(dataBytes, SocketFlags.None);
        }
    }

    async void forwardDataOutAsync(Socket externSocket, Socket internSocket)
    {
        Debug.Log("forwarding data out start");
        while(true)
        {
            var bufferExt = new byte[1_024]; //byte buffer of 1,024 bytes
            var receivedExt = await internSocket.ReceiveAsync(bufferExt, SocketFlags.None);

            var dataBytes = new byte[receivedExt];
            Array.Copy(bufferExt, dataBytes, receivedExt);
            
            //Debug.Log("outbound network packet: "+BitConverter.ToString(dataBytes));

            await externSocket.SendAsync(dataBytes, SocketFlags.None);
        }
    }

    async void forwardDataInUDPAsync(Socket externSocket, UdpClient internClient)
    {
        Debug.Log("forwarding data in (UDP) started");
        while(true)
        {
            var bufferExt = new byte[1_024]; //byte buffer of 1,024 bytes
            var receivedExt = await externSocket.ReceiveAsync(bufferExt, SocketFlags.None);

            var dataBytes = new byte[receivedExt];
            Array.Copy(bufferExt, dataBytes, receivedExt);

            await internalClient.SendAsync(dataBytes, SocketFlags.None);
        }

    }

    async void forwardDataOutUDPAsync(Socket externSocket, UdpClient internClient)
    {
        IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
        Debug.Log("forwarding data out (UDP) start");
        while(true)
        {
            var dataBytes = internClient.Receive(ref sender);
            if(sender.Address != IPAddress.Loopback && sender.Port != 35751)
                continue;
            
            await externSocket.SendAsync(dataBytes, SocketFlags.None);
        }
        
    }

    /*async void forwardExternalForServerAsync(Socket externalHandler, Socket internalClient)
    {
        Debug.Log("forwarding external for server started");
        while(true)
        {
            var bufferExt = new byte[1_024]; //byte buffer of 1,024 bytes
            var receivedExt = await externalHandler.ReceiveAsync(bufferExt, SocketFlags.None);

            var dataBytes = new byte[receivedExt];
            Array.Copy(bufferExt, dataBytes, receivedExt);

            //Debug.Log("inbound network packet: "+BitConverter.ToString(dataBytes));

            await internalClient.SendAsync(dataBytes, SocketFlags.None);
        }

    }

    async void forwardInternalForServerAsync(Socket externalHandler, Socket internalClient)
    {
        Debug.Log("forwarding internal for server started");
        while(true)
        {
            var bufferExt = new byte[1_024]; //byte buffer of 1,024 bytes
            var receivedExt = await internalClient.ReceiveAsync(bufferExt, SocketFlags.None);

            var dataBytes = new byte[receivedExt];
            Array.Copy(bufferExt, dataBytes, receivedExt);
            
            //Debug.Log("outbound network packet: "+BitConverter.ToString(dataBytes));

            await externalHandler.SendAsync(dataBytes, SocketFlags.None);
        }
    }

    async void forwardExternalForClientAsync(Socket externalClient, Socket internalServer)
    {
        Debug.Log("forwarding external for client started");
        while(true)
        {
            var bufferExt = new byte[1_024]; //byte buffer of 1,024 bytes
            var receivedExt = await externalClient.ReceiveAsync(bufferExt, SocketFlags.None);

            var dataBytes = new byte[receivedExt];
            Array.Copy(bufferExt, dataBytes, receivedExt);
            
            //Debug.Log("inbound network packet: "+BitConverter.ToString(dataBytes));

            await internalServer.SendAsync(dataBytes, SocketFlags.None);
        }
    }

    async void forwardInternalForClientAsync(Socket externalClient, Socket internalServer)
    {
        Debug.Log("forwarding internal for client started");
        while(true)
        {
            var bufferExt = new byte[1_024]; //byte buffer of 1,024 bytes
            var receivedExt = await internalServer.ReceiveAsync(bufferExt, SocketFlags.None);

            var dataBytes = new byte[receivedExt];
            Array.Copy(bufferExt, dataBytes, receivedExt);
            
            //Debug.Log("outbound network packet: "+BitConverter.ToString(dataBytes));

            await externalClient.SendAsync(dataBytes, SocketFlags.None);
        }
    }*/

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
