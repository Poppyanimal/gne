using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using System;
using UnityEngine;

public class TCPTransport //TODO: better name
{
    //Socket externalListener, internalClient, externalClient, internalListener,
    //    udp_externalListener, /*udp_internalClient,*/ udp_externalClient/*, udp_internalListener*/;

    public async void runServerForwarder(int hostingPort = 25565, int gofPort = 35751) //assume that the hosting port will also be accompanied by that port + 1 being open
    {
        //TODO: have both the TCP over TCP and the UDP over TCP on the same port instead of seperate ones

        IPAddress extIP = IPAddress.Any;
        int extPort = hostingPort; //what the other player is looking for w/ their launcher
        IPEndPoint extEndPoint = new IPEndPoint(extIP, extPort);

        Socket externalListener
            = new(extIP.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

        externalListener.Bind(extEndPoint);
        externalListener.Listen(100);

        Debug.Log("waiting for external TCP connection");
        var externalHandler = await externalListener.AcceptAsync();
        Debug.Log("external connection TCP found");

        IPAddress internIP = IPAddress.Loopback;
        int internPort = gofPort; //where gleam of force is hosting from
        IPEndPoint internEndPoint = new IPEndPoint(internIP, internPort);

        Socket internalClient
            = new(SocketType.Stream, ProtocolType.Tcp);

        Debug.Log("Cconnecting to internal client over TCP");
        await internalClient.ConnectAsync(internEndPoint);
        Debug.Log("internal client connected over TCP");

        forwardDataInAsync(externalHandler, internalClient, true);
        forwardDataOutAsync(externalHandler, internalClient, true);

        //
        //start of udp forwarded to tcp stuff instead of just wrapping tcp in tcp
        //

        //the port that the udp data will be forwarded on instead of the tcp above
        IPEndPoint udp_ExtEndPoint = new IPEndPoint(extIP, extPort + 1);

        Socket udp_externalListener
            = new(SocketType.Stream, ProtocolType.Tcp);

        udp_externalListener.Bind(udp_ExtEndPoint);
        udp_externalListener.Listen(100);

        Debug.Log("waiting for external UDP over TCP connection");
        var udp_ExternalHandler = await udp_externalListener.AcceptAsync();
        Debug.Log("external UDP over TCP connection found");


        Debug.Log("Initializing local UDP client");
        UdpClient localUDPCon = new UdpClient(internEndPoint);
        Debug.Log("Initialized local UDP client");

        forwardDataInUDPAsync(udp_ExternalHandler, localUDPCon, true);
        forwardDataOutUDPAsync(udp_ExternalHandler, localUDPCon, true);
    }

    public async void runClientForwarder(string ipToConnectTo, int hostPort = 25565, int gofPort = 35751)
    {
        IPAddress internIP = IPAddress.Loopback;
        int internPort = gofPort; //where gleam of force is trying to connect to
        IPEndPoint internEndPoint = new IPEndPoint(internIP, internPort);

        Socket internalListener
            = new(SocketType.Stream, ProtocolType.Tcp);

        internalListener.Bind(internEndPoint);
        internalListener.Listen(100);

        Debug.Log("waiting for internal client to be found over TCP");
        var internalHandler = await internalListener.AcceptAsync();
        Debug.Log("internal client connected over TCP");

        IPAddress extIP = IPAddress.Parse(ipToConnectTo);
        int extPort = hostPort; //what the other player is looking for w/ their launcher
        IPEndPoint extEndPoint = new IPEndPoint(extIP, extPort);

        Socket externalClient
            = new(extIP.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

        Debug.Log("Connecting to external server for TCP over TCP");
        await externalClient.ConnectAsync(extEndPoint);
        Debug.Log("Externel server found and connected for TCP over TCP");

        forwardDataInAsync(externalClient, internalHandler, false);
        forwardDataOutAsync(externalClient, internalHandler, false);
        
        //
        //start of udp forwarded to tcp stuff instead of just wrapping tcp in tcp
        //
        IPEndPoint udp_ExtEndPoint = new IPEndPoint(extIP, extPort + 1);
        
        Socket udp_externalClient
            = new(SocketType.Stream, ProtocolType.Tcp);

        Debug.Log("Connecting to external server on UDP over TCP");
        await udp_externalClient.ConnectAsync(udp_ExtEndPoint);
        Debug.Log("Connected to external server on UDP over TCP");
        
        Debug.Log("Initializing local UDP client");
        UdpClient localUDPCon = new UdpClient(internEndPoint);
        Debug.Log("Initialized local UDP client");

        forwardDataInUDPAsync(udp_externalClient, localUDPCon, false);
        forwardDataOutUDPAsync(udp_externalClient, localUDPCon, false);
    }

    //
    //
    //
    bool socketIsDisconnected(Socket s)
    {
        return s.Poll(1000, SelectMode.SelectRead) && s.Available == 0; //if (connection not active or connection is active but there is data for reading) and number of bytes available for reading is 0
    }

    async void forwardDataInAsync(Socket externSocket, Socket internSocket, bool isTheServer)
    {
        Debug.Log("forwarding data in started");
        while(true)
        {
            //if Socket is pass by reference, this will break as two methods try reconnecting the socket; if it is not pass by reference, this should work
            if(socketIsDisconnected(externSocket))
            {
                Debug.Log("external socket (TCP over TCP) disconnected, attempting to reconnect");
                if(isTheServer)
                    externSocket = await externSocket.AcceptAsync();
                else
                    await externSocket.ConnectAsync(externSocket.LocalEndPoint);
                Debug.Log("external socket (TCP over TCP) reconnected");
            }
            if(socketIsDisconnected(internSocket))
            {
                Debug.Log("internal socket (TCP) disconnected, attempting to reconnect");
                if(isTheServer)
                    await internSocket.ConnectAsync(internSocket.LocalEndPoint);
                else
                    internSocket = await internSocket.AcceptAsync();
                Debug.Log("internal socket (TCP) reconnected");
            }
            
            //
            
            var bufferExt = new byte[1_024]; //byte buffer of 1,024 bytes
            var receivedExt = await externSocket.ReceiveAsync(bufferExt, SocketFlags.None);

            var dataBytes = new byte[receivedExt];
            Array.Copy(bufferExt, dataBytes, receivedExt);

            //Debug.Log("inbound network packet: "+BitConverter.ToString(dataBytes));

            await internSocket.SendAsync(dataBytes, SocketFlags.None);
        }
    }

    async void forwardDataOutAsync(Socket externSocket, Socket internSocket, bool isTheServer)
    {
        Debug.Log("forwarding data out start");
        while(true)
        {
            if(socketIsDisconnected(externSocket))
            {
                Debug.Log("external socket (TCP over TCP) disconnected, attempting to reconnect");
                if(isTheServer)
                    externSocket = await externSocket.AcceptAsync();
                else
                    await externSocket.ConnectAsync(externSocket.LocalEndPoint);
                Debug.Log("external socket (TCP over TCP) reconnected");
            }
            if(socketIsDisconnected(internSocket))
            {
                Debug.Log("internal socket (TCP) disconnected, attempting to reconnect");
                if(isTheServer)
                    await internSocket.ConnectAsync(internSocket.LocalEndPoint);
                else
                    internSocket = await internSocket.AcceptAsync();
                Debug.Log("internal socket (TCP) reconnected");
            }

            //

            var bufferExt = new byte[1_024]; //byte buffer of 1,024 bytes
            var receivedExt = await internSocket.ReceiveAsync(bufferExt, SocketFlags.None);

            var dataBytes = new byte[receivedExt];
            Array.Copy(bufferExt, dataBytes, receivedExt);
            
            //Debug.Log("outbound network packet: "+BitConverter.ToString(dataBytes));

            await externSocket.SendAsync(dataBytes, SocketFlags.None);
        }
    }

    async void forwardDataInUDPAsync(Socket externSocket, UdpClient internClient, bool isTheServer)
    {
        IPEndPoint localclient = new(IPAddress.Loopback, 35751);
        Debug.Log("forwarding data in (UDP) started");
        while(true)
        {
            if(socketIsDisconnected(externSocket))
            {
                Debug.Log("external socket (UDP over TCP) disconnected, attempting to reconnect");
                if(isTheServer)
                    externSocket = await externSocket.AcceptAsync();
                else
                    await externSocket.ConnectAsync(externSocket.LocalEndPoint);
                Debug.Log("external socket (UDP over TCP) reconnected");
            }
            //no internal reconnection because internal is udp
            
            //

            var bufferExt = new byte[1_024]; //byte buffer of 1,024 bytes
            var receivedExt = await externSocket.ReceiveAsync(bufferExt, SocketFlags.None);

            var dataBytes = new byte[receivedExt];
            Array.Copy(bufferExt, dataBytes, receivedExt);

            await internClient.SendAsync(dataBytes, dataBytes.Length, localclient);
        }

    }

    async void forwardDataOutUDPAsync(Socket externSocket, UdpClient internClient, bool isTheServer)
    {
        IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
        Debug.Log("forwarding data out (UDP) start");
        while(true)
        {
            if(socketIsDisconnected(externSocket))
            {
                Debug.Log("external socket (UDP over TCP) disconnected, attempting to reconnect");
                if(isTheServer)
                    externSocket = await externSocket.AcceptAsync();
                else
                    await externSocket.ConnectAsync(externSocket.LocalEndPoint);
                Debug.Log("external socket (UDP over TCP) reconnected");
            }
            //no internal reconnection because internal is udp
            
            //

            var dataBytes = internClient.Receive(ref sender);
            if(sender.Address != IPAddress.Loopback && sender.Port != 35751)
                continue;
            
            await externSocket.SendAsync(dataBytes, SocketFlags.None);
        }
        
    }

}
