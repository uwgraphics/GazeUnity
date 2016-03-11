using UnityEngine;
using System;
using System.Collections;
using System.IO;

/// <summary>
/// Script for capturing video of scenario execution
/// at constant framerate.
/// </summary>
public class VideoCapture : MonoBehaviour
{
	public int captureFrameRate = 30;
	
	int frameIndex = 1;

	private bool startCapture = false;
	private string identifier = "";
	
	public void Start_Capture(string id)
	{
		Time.captureFramerate = captureFrameRate;
		identifier = id;

		if( !Directory.Exists("./VideoCapture-" + identifier) )
			Directory.CreateDirectory("./VideoCapture-" + identifier);
		
		frameIndex = 1;

		startCapture = true;
	}
	
	void Update()
	{
		if( !Directory.Exists("./VideoCapture-" + identifier) || !startCapture)
			return;
		
		string filename = string.Format( "./VideoCapture-"+identifier+"/frame{0:D5}.png", frameIndex++ );
		Application.CaptureScreenshot(filename);
	}
}
