using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

//Where is the socket information coming from?
public enum SocketSource
{
	GazeTracker,
	SpeechRecognition,
	ActionTracker,
	HeadTracker
};

public enum StreamType
{
	live,
	debug,
	playback
}

/// <summary>
/// Script for receiving messages from clients (e.g., Gaze Tracker, Webcam, Google Speech Recognition)
/// </summary>
public class AsynchronousSocketListener : MonoBehaviour {
    // Thread signal.
    public static ManualResetEvent allDone = new ManualResetEvent(false);
	public SocketSource ID = SocketSource.GazeTracker;
	public int ipAddressIndex = 1;
	public int portNumber = 4242;
	public StreamType dataStream = StreamType.live;
	[HideInInspector]
	public string SocketContent = "nothing";
	[HideInInspector]
	public bool connectionMade = false;
	
	private Socket listener;
	private Thread acceptConnectionsThread;
	private bool isListening = true;

	//private List<int> hiddenGridCellsDEBUG = null;
	//private List<int> oldHiddenGridCellsDEBUG = null;
	private List<int> visibleGridCellsDEBUG = null;

    public void StartListening() {
        // Establish the local endpoint for the socket.
        IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
		IPAddress[] ipAddresses = ipHostInfo.AddressList;
		IPAddress ipAddress = null;
		if (ipAddressIndex == -1) {
			ipAddress = IPAddress.Parse("127.0.0.1");
		}
		else {
			ipAddress = ipAddresses[ipAddressIndex];
		}
        IPEndPoint localEndPoint = new IPEndPoint(ipAddress, portNumber);

        // Create a TCP/IP socket.
        listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp );

        // Bind the socket to the local endpoint and listen for incoming connections.
        try {
            listener.Bind(localEndPoint);
            listener.Listen(100);

	        // Set the event to nonsignaled state.
	        allDone.Reset();

	        // Start an asynchronous socket to listen for connections.
			string s = string.Format ("Waiting for a connection on IP {0}, port {1}...",ipAddress.ToString(), portNumber);
	        UnityEngine.Debug.Log(s);
	        listener.BeginAccept(new AsyncCallback(AcceptCallback), listener );

	        // Wait until a connection is made before continuing.
	        allDone.WaitOne();
        } catch (Exception e) {
            UnityEngine.Debug.Log(e.ToString());
        }
    }

    public void AcceptCallback(IAsyncResult ar) {
		if (!isListening)
			return;
        // Get the socket that handles the client request.
        Socket listener = (Socket) ar.AsyncState;
        Socket handler = listener.EndAccept(ar);

        // Create the state object.
        StateObject state = new StateObject();
        state.workSocket = handler;
        handler.BeginReceive( state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReadCallback), state);

		// Signal the main thread to continue.
		allDone.Set();
		connectionMade = true;
		UnityEngine.Debug.Log ("Connection made");
    }

    public void ReadCallback(IAsyncResult ar) {
		if (!isListening)
			return;
		
        String content = String.Empty;
        
        // Retrieve the state object and the handler socket
        // from the asynchronous state object.
        StateObject state = (StateObject) ar.AsyncState;
        Socket handler = state.workSocket;

        // Read data from the client socket. 
        int bytesRead = handler.EndReceive(ar);

        if (bytesRead > 0) {
            // There  might be more data, so store the data received so far.
            state.sb.Append(Encoding.ASCII.GetString(state.buffer,0,bytesRead));

            // Check for end-of-file tag. If it is not there, read 
            // more data.
            content = state.sb.ToString();
			//UnityEngine.Debug.Log (content);
			//Send (handler, content);
            if (content.IndexOf('\n') > -1) {
                // All the data has been read from the socket.
				string[] allLines = content.Split('\n');
				if (allLines.Length > 1)
				{
					SocketContent = allLines[allLines.Length-2];
				}
				else
				{
					SocketContent = content;
				}
				//string s = string.Format("Read {0} bytes from socket. \n Data : {1} \n Final socket content: {2}", content.Length, content, SocketContent);
                //UnityEngine.Debug.Log(s);
                // Echo the data back to the client.
                //Send(handler, content);
				state.sb = new StringBuilder();
            }
            handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReadCallback), state);
        }
    }
    
    private void Send(Socket handler, String data) {
        // Convert the string data to byte data using ASCII encoding.
        byte[] byteData = Encoding.ASCII.GetBytes(data);

        // Begin sending the data to the remote device.
        handler.BeginSend(byteData, 0, byteData.Length, 0, new AsyncCallback(SendCallback), handler);
    }

    private void SendCallback(IAsyncResult ar) {
        try {
            // Retrieve the socket from the state object.
            Socket handler = (Socket) ar.AsyncState;

            // Complete sending the data to the remote device.
            int bytesSent = handler.EndSend(ar);
            Console.WriteLine("Sent {0} bytes to client.", bytesSent);

            handler.Shutdown(SocketShutdown.Both);
            handler.Close();

        } catch (Exception e) {
            UnityEngine.Debug.Log(e.ToString());
        }
    }

    public void Start() {
		if (dataStream == StreamType.live) {
			acceptConnectionsThread = new Thread(new ThreadStart(StartListening));
			acceptConnectionsThread.Start();
		}
		else if (dataStream == StreamType.debug) {
			//hiddenGridCellsDEBUG = new List<int>(new int[18]{0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17});
			visibleGridCellsDEBUG = new List<int>();
		}
		else if (dataStream == StreamType.playback) {

		}
    }

	//Gui buttons for debugging without the real socket
	public void OnGUI() {
		if (dataStream == StreamType.debug) {
			if (ID == SocketSource.ActionTracker) {
				if (GUI.Button(new Rect(10, 130, 50, 50), "1")) {
					//Debug.Log("Moved 1");
					//hiddenGridCellsDEBUG.Remove(0);
					if (visibleGridCellsDEBUG.Contains (0))
					{
						visibleGridCellsDEBUG.Remove(0);
					}
					else
					{
						visibleGridCellsDEBUG.Add (0);
					}
				}
				if (GUI.Button(new Rect(70, 130, 50, 50), "2")) {
					//Debug.Log("Moved 2");
					//hiddenGridCellsDEBUG.Remove(1);
					if (visibleGridCellsDEBUG.Contains (1))
					{
						visibleGridCellsDEBUG.Remove(1);
					}
					else
					{
						visibleGridCellsDEBUG.Add (1);
					}
				}
				if (GUI.Button(new Rect(130, 130, 50, 50), "3")) {
					//Debug.Log("Moved 3");
					//hiddenGridCellsDEBUG.Remove(2);
					if (visibleGridCellsDEBUG.Contains (2))
					{
						visibleGridCellsDEBUG.Remove(2);
					}
					else
					{
						visibleGridCellsDEBUG.Add (2);
					}
				}
				if (GUI.Button(new Rect(190, 130, 50, 50), "4")) {
					//Debug.Log("Moved 4");
					//hiddenGridCellsDEBUG.Remove(3);
					if (visibleGridCellsDEBUG.Contains (3))
					{
						visibleGridCellsDEBUG.Remove(3);
					}
					else
					{
						visibleGridCellsDEBUG.Add (3);
					}
				}
				if (GUI.Button(new Rect(250, 130, 50, 50), "5")) {
					//Debug.Log("Moved 5");
					//hiddenGridCellsDEBUG.Remove(4);
					if (visibleGridCellsDEBUG.Contains (4))
					{
						visibleGridCellsDEBUG.Remove(4);
					}
					else
					{
						visibleGridCellsDEBUG.Add (4);
					}
				}
				if (GUI.Button(new Rect(310, 130, 50, 50), "6")) {
					//Debug.Log("Moved 6");
					//hiddenGridCellsDEBUG.Remove(5);
					if (visibleGridCellsDEBUG.Contains (5))
					{
						visibleGridCellsDEBUG.Remove(5);
					}
					else
					{
						visibleGridCellsDEBUG.Add (5);
					}
				}

				if (GUI.Button(new Rect(10, 70, 50, 50), "7")) {
					//Debug.Log("Moved 7");
					//hiddenGridCellsDEBUG.Remove(6);
					if (visibleGridCellsDEBUG.Contains (6))
					{
						visibleGridCellsDEBUG.Remove(6);
					}
					else
					{
						visibleGridCellsDEBUG.Add (6);
					}
				}
				if (GUI.Button(new Rect(70, 70, 50, 50), "8")) {
					//Debug.Log("Moved 8");
					//hiddenGridCellsDEBUG.Remove(7);
					if (visibleGridCellsDEBUG.Contains (7))
					{
						visibleGridCellsDEBUG.Remove(7);
					}
					else
					{
						visibleGridCellsDEBUG.Add (7);
					}
				}
				if (GUI.Button(new Rect(130, 70, 50, 50), "9")) {
					//Debug.Log("Moved 9");
					//hiddenGridCellsDEBUG.Remove(8);
					if (visibleGridCellsDEBUG.Contains (8))
					{
						visibleGridCellsDEBUG.Remove(8);
					}
					else
					{
						visibleGridCellsDEBUG.Add (8);
					}
				}
				if (GUI.Button(new Rect(190, 70, 50, 50), "10")) {
					//Debug.Log("Moved 10");
					//hiddenGridCellsDEBUG.Remove(9);
					if (visibleGridCellsDEBUG.Contains (9))
					{
						visibleGridCellsDEBUG.Remove(9);
					}
					else
					{
						visibleGridCellsDEBUG.Add (9);
					}
				}
				if (GUI.Button(new Rect(250, 70, 50, 50), "11")) {
					//Debug.Log("Moved 11");
					//hiddenGridCellsDEBUG.Remove(10);
					if (visibleGridCellsDEBUG.Contains (10))
					{
						visibleGridCellsDEBUG.Remove(10);
					}
					else
					{
						visibleGridCellsDEBUG.Add (10);
					}
				}
				if (GUI.Button(new Rect(310, 70, 50, 50), "12")) {
					//Debug.Log("Moved 12");
					//hiddenGridCellsDEBUG.Remove(11);
					if (visibleGridCellsDEBUG.Contains (11))
					{
						visibleGridCellsDEBUG.Remove(11);
					}
					else
					{
						visibleGridCellsDEBUG.Add (11);
					}
				}

				if (GUI.Button(new Rect(10, 10, 50, 50), "13")) {
					//Debug.Log("Moved 13");
					//hiddenGridCellsDEBUG.Remove(12);
					if (visibleGridCellsDEBUG.Contains (12))
					{
						visibleGridCellsDEBUG.Remove(12);
					}
					else
					{
						visibleGridCellsDEBUG.Add (12);
					}
				}
				if (GUI.Button(new Rect(70, 10, 50, 50), "14")) {
					//Debug.Log("Moved 14");
					//hiddenGridCellsDEBUG.Remove(13);
					if (visibleGridCellsDEBUG.Contains (13))
					{
						visibleGridCellsDEBUG.Remove(13);
					}
					else
					{
						visibleGridCellsDEBUG.Add (13);
					}
				}
				if (GUI.Button(new Rect(130, 10, 50, 50), "15")) {
					//Debug.Log("Moved 15");
					//hiddenGridCellsDEBUG.Remove(14);
					if (visibleGridCellsDEBUG.Contains (14))
					{
						visibleGridCellsDEBUG.Remove(14);
					}
					else
					{
						visibleGridCellsDEBUG.Add (14);
					}
				}
				if (GUI.Button(new Rect(190, 10, 50, 50), "16")) {
					//Debug.Log("Moved 16");
					//hiddenGridCellsDEBUG.Remove(15);
					if (visibleGridCellsDEBUG.Contains (15))
					{
						visibleGridCellsDEBUG.Remove(15);
					}
					else
					{
						visibleGridCellsDEBUG.Add (15);
					}
				}
				if (GUI.Button(new Rect(250, 10, 50, 50), "17")) {
					//Debug.Log("Moved 17");
					//hiddenGridCellsDEBUG.Remove(16);
					if (visibleGridCellsDEBUG.Contains (16))
					{
						visibleGridCellsDEBUG.Remove(16);
					}
					else
					{
						visibleGridCellsDEBUG.Add (16);
					}
				}
				if (GUI.Button(new Rect(310, 10, 50, 50), "18")) {
					//Debug.Log("Moved 18");
					//hiddenGridCellsDEBUG.Remove(17);
					if (visibleGridCellsDEBUG.Contains (17))
					{
						visibleGridCellsDEBUG.Remove(17);
					}
					else
					{
						visibleGridCellsDEBUG.Add (17);
					}
				}
				SocketContent = "visible:";
				for (int i = 0; i < visibleGridCellsDEBUG.Count; ++i) {
					//SocketContent += hiddenGridCellsDEBUG[i] + ":";
					SocketContent += visibleGridCellsDEBUG[i] + ":";
				}
			}
			else if (ID == SocketSource.SpeechRecognition) {
				if (GUI.Button(new Rect(10, 190, 150, 50), "Clarify")) {
					//Debug.Log("Asked for clarification");
					SocketContent = "clarify";
				}
				if (GUI.Button(new Rect(10, 250, 150, 50), "Confirm")) {
					//Debug.Log("Confirmed understanding");
					SocketContent = "confirm";
				}
			}
			else if (ID == SocketSource.GazeTracker) {
				if (GUI.Button(new Rect(10, 430, 50, 50), "1")) {
					SocketContent = "Grid: 0";
					//Debug.Log("Gazed at 1");
				}
				if (GUI.Button(new Rect(70, 430, 50, 50), "2")) {
					SocketContent = "Grid: 1";
					//Debug.Log("Gazed at 2");
				}
				if (GUI.Button(new Rect(130, 430, 50, 50), "3")) {
					SocketContent = "Grid: 2";
					//Debug.Log("Gazed at 3");
				}
				if (GUI.Button(new Rect(190, 430, 50, 50), "4")) {
					SocketContent = "Grid: 3";
					//Debug.Log("Gazed at 4");
				}
				if (GUI.Button(new Rect(250, 430, 50, 50), "5")) {
					SocketContent = "Grid: 4";
					//Debug.Log("Gazed at 5");
				}
				if (GUI.Button(new Rect(310, 430, 50, 50), "6")) {
					SocketContent = "Grid: 5";
					//Debug.Log("Gazed at 6");
				}

				if (GUI.Button(new Rect(370, 310, 100, 50), "Face")) {
					SocketContent = "Grid: 99";
					//Debug.Log("Gazed at Face");
				}
				
				if (GUI.Button(new Rect(10, 370, 50, 50), "7")) {
					SocketContent = "Grid: 6";
					//Debug.Log("Gazed at 7");
				}
				if (GUI.Button(new Rect(70, 370, 50, 50), "8")) {
					SocketContent = "Grid: 7";
					//Debug.Log("Gazed at 8");
				}
				if (GUI.Button(new Rect(130, 370, 50, 50), "9")) {
					SocketContent = "Grid: 8";
					//Debug.Log("Gazed at 9");
				}
				if (GUI.Button(new Rect(190, 370, 50, 50), "10")) {
					SocketContent = "Grid: 9";
					//Debug.Log("Gazed at 10");
				}
				if (GUI.Button(new Rect(250, 370, 50, 50), "11")) {
					SocketContent = "Grid: 10";
					//Debug.Log("Gazed at 11");
				}
				if (GUI.Button(new Rect(310, 370, 50, 50), "12")) {
					SocketContent = "Grid: 11";
					//Debug.Log("Gazed at 12");
				}
				
				if (GUI.Button(new Rect(10, 310, 50, 50), "13")) {
					SocketContent = "Grid: 12";
					//Debug.Log("Gazed at 13");
				}
				if (GUI.Button(new Rect(70, 310, 50, 50), "14")) {
					SocketContent = "Grid: 13";
					//Debug.Log("Gazed at 14");
				}
				if (GUI.Button(new Rect(130, 310, 50, 50), "15")) {
					SocketContent = "Grid: 14";
					//Debug.Log("Gazed at 15");
				}
				if (GUI.Button(new Rect(190, 310, 50, 50), "16")) {
					SocketContent = "Grid: 15";
					//Debug.Log("Gazed at 16");
				}
				if (GUI.Button(new Rect(250, 310, 50, 50), "17")) {
					SocketContent = "Grid: 16";
					//Debug.Log("Gazed at 17");
				}
				if (GUI.Button(new Rect(310, 310, 50, 50), "18")) {
					SocketContent = "Grid: 17";
					//Debug.Log("Gazed at 18");
				}
			}
		}
	}

	public void resetActionDEBUG()
	{
		if (dataStream == StreamType.debug)
		{
			visibleGridCellsDEBUG.Clear();
		}
	}
	
	public void OnApplicationQuit() {
		isListening = false;
		if (dataStream == StreamType.live) {
			listener.Close ();
			acceptConnectionsThread.Abort();
		}
	}
}

// State object for reading client data asynchronously
public class StateObject {
    // Client  socket.
    public Socket workSocket = null;
    // Size of receive buffer.
    public const int BufferSize = 1024;
    // Receive buffer.
    public byte[] buffer = new byte[BufferSize];
// Received data string.
    public StringBuilder sb = new StringBuilder();  
}