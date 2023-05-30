using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using EventControllerAndLogger.Json;
using EventControllerAndLogger.Logger;
using Newtonsoft.Json;

namespace EventControllerAndLogger.Controller;

public class ECAL
{

    private  readonly IPAddress _ipAddress = IPAddress.Any;
    private Socket _clientSocket;
    private Thread _messageThread;

    private Socket unityClient;
    private InfluxDb _influxDb;

    private AppConfig _appConfig;


    public ECAL(AppConfig appConfig)
    {
        _appConfig = appConfig;
        if (appConfig.UseCrownet)
        {
            Console.WriteLine("[Notification] Logging to InfluxDB enabled.");

            _influxDb = new(appConfig.InfluxAddr, appConfig.InfluxPort);
        }
        else
        {
            Console.WriteLine("[Notification] Logging to InfluxDB disabled.");
        }

        if (appConfig.UseUnity)
        {
            Console.WriteLine("[Notification] Exporting to Unity enabled.");

            unityClient = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            
            
            Console.WriteLine($"UseUnity: {appConfig.UseUnity}, UseCrownet: {appConfig.UseCrownet}, UseInflux: {appConfig.UseInflux}, OmnetPort: {appConfig.OmnetPort}, UnityAddr: {appConfig.UnityAddr}, UnityPort: {appConfig.UnityPort}, InfluxAddr: {appConfig.InfluxAddr}, InfluxPort: {appConfig.InfluxPort}");

            var ep = new IPEndPoint(IPAddress.Parse(appConfig.UnityAddr), appConfig.UnityPort);

            try
            {
                unityClient.Connect(ep);
                Console.WriteLine("[Notification] Successfully connected to Unity");
                
                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
        }
        else
        {
            Console.WriteLine("[Notification] Exporting to Unity disabled.");

        }




        var serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        
        var endPoint = new IPEndPoint(_ipAddress, _appConfig.OmnetPort);
            serverSocket.Bind(endPoint);

        serverSocket.Listen(1);
        Console.WriteLine("Waiting for Omnet++ Simulation to connect on Port: {}");
        _clientSocket = serverSocket.Accept();
        Console.WriteLine("Simulation Connected!");


        _messageThread = new Thread(ReceiveData);
        _messageThread.Start();
        


    }


    private void ReceiveData()
    {



        while (true)
        {
            
            var lengthBuffer = new byte[4];
            _clientSocket.Receive(lengthBuffer, SocketFlags.None);
            
            if (unityClient.Connected) unityClient.Send(lengthBuffer);

            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(lengthBuffer);
            }
            
            int messageLength = BitConverter.ToInt32(lengthBuffer, 0);

            if (messageLength == 0)
            {
                Console.WriteLine("Received empty message. Client is disconnected!");
                break;
            }


            
            var messageBuffer = new byte[messageLength];
            var received = _clientSocket.Receive(messageBuffer, SocketFlags.None);

            if (unityClient.Connected) unityClient.Send(messageBuffer);

            var response = Encoding.UTF8.GetString(messageBuffer, 0, received);

            var message = JsonConvert.DeserializeObject<Message>(response);

            Debug.Assert(message != null, nameof(message) + " != null");
            Console.WriteLine("{0},{1},{2},{3},{4},{5}", message.Id, message.Path,message.Instruction, message.Coordinates.X, message.Coordinates.Y, message.Coordinates.Z);
            _influxDb.WriteToDatabase(message);
            
        }
        
        _clientSocket.Shutdown(SocketShutdown.Both);

        
    }


}