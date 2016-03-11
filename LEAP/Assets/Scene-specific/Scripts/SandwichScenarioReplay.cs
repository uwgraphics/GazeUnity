using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MathNet.Numerics;
using MathNet.Numerics.Distributions;
using MathNet.Numerics.RandomSources;
using System.Linq;

public class SandwichScenarioReplay : Scenario
{
	private string agentName = "Jason";

	//the relevant controllers
	private GazeController gazeCtrl;
	
	//Random distributions for smiling and nodding while listening
	private MersenneTwisterRandomSource randNumGen = null; //random number generator for seeding the distributions below
	private NormalDistribution nextSmileDistribution = null;
	private NormalDistribution smileLengthDistribution = null;

	//Random smiling
	private double nextSmileCounter = 0;
	private double nextSmileTime = 0;

	private bool startReplay = false;
	GameObject mutualGazeObject = null;

	public TextAsset logFile;
	public string timeStartingFrom = "";
	private double counter = 0;
	private GameObject[] gridTargets;

	class LogEvent
	{
		public double startTime;
		public string type;
		public string parameter;

		public LogEvent(double st, string t, string p)
		{
			startTime = st;
			type = t;
			parameter = p;
		}
	}

	private List<LogEvent> logEvents = null;
	private int currentLogIndex = 0;

	//public bool captureVideo = false;
	//VideoCapture vidcap;
	
	/// <see cref="Scenario._Init()"/>
	protected override void _Init()
	{
		//init the random number generators
		randNumGen = new MersenneTwisterRandomSource();
		nextSmileDistribution = new NormalDistribution(randNumGen);
		nextSmileDistribution.SetDistributionParameters(20.0, 5.0);
		smileLengthDistribution = new NormalDistribution(randNumGen);
		smileLengthDistribution.SetDistributionParameters(5.0, 1.0);
		
		nextSmileTime = nextSmileDistribution.NextDouble();
		//vidcap = GetComponent<VideoCapture>();
		//vidcap.enabled = false;
	}
	
	/// <see cref="Scenario._Run()"/>
	protected override IEnumerator _Run()
	{
		//speech and gaze behaviors
		//int curspeak = -1;
		int curgaze = -1;

		//Get and sort the grid gaze targets
		GameObject[] gridTargetsTemp = GameObject.FindGameObjectsWithTag("GazeTarget");
		Array.Sort(gridTargetsTemp, delegate(GameObject g1, GameObject g2) {
			return g1.name.CompareTo(g2.name);
		});
		//Make an adjustment for indexing (0 refers to no gaze)
		gridTargets = new GameObject[19];
		gridTargets[0] = null;
		for (int i = 0; i < 18; i++) {
			gridTargets[i+1] = gridTargetsTemp[i];
		}

		//Find the relevant controllers
		gazeCtrl = agents[agentName].GetComponent<GazeController>();

		//set the scene lights
		GameObject[] lights = GameObject.FindGameObjectsWithTag("Spotlight");
		float[] lightIntensities = new float[lights.Length];
		for (int i = 0; i < lights.Length; ++i) {
			lightIntensities[i] = lights[i].light.intensity;
			lights[i].light.intensity = 0f;	
		}

		mutualGazeObject = GameObject.Find("Main Camera");
		// Initialize gaze
		yield return new WaitForSeconds(0.6f);
		curgaze = GazeAt(agentName, mutualGazeObject, 0.8f, 0f);
		yield return StartCoroutine( WaitUntilFinished(curgaze) );
		curgaze = GazeAt(agentName, mutualGazeObject, 0.8f, 0f);
		yield return StartCoroutine( WaitUntilFinished(curgaze) );
		curgaze = GazeAt(agentName, mutualGazeObject, 1.0f, 0f);
		yield return StartCoroutine( WaitUntilFinished(curgaze) );

		for (int i = 0; i < lights.Length; ++i) {
			lights[i].light.intensity = lightIntensities[i];
		}

		logEvents = new List<LogEvent>();
		string[] logLines = logFile.text.Split("\n"[0]);
		DateTime startTime = Convert.ToDateTime(logLines[0].Split("\t"[0])[0]);
		if (timeStartingFrom != "" && timeStartingFrom != null)
		{
			startTime = Convert.ToDateTime(timeStartingFrom);
		}
		foreach (string line in logLines)
		{
			string[] lineElements = line.Split ("\t"[0]);
			if (lineElements.Length >= 2)
			{
				DateTime dt = Convert.ToDateTime(lineElements[0]);
				if (dt < startTime)
				{
					continue;
				}
				if (lineElements[1] == "Speech Start:")
				{
					//UnityEngine.Debug.Log(line);
					logEvents.Add (new LogEvent(dt.Subtract(startTime).TotalSeconds, lineElements[1], lineElements[2]));
				}
				else if (lineElements[1].Contains("Agent Gaze to User"))
				{
					//UnityEngine.Debug.Log(line);
					logEvents.Add (new LogEvent(dt.Subtract(startTime).TotalSeconds, lineElements[1], ""));
				}
				else if (lineElements[1].Contains("Agent Gaze to"))
				{
					//UnityEngine.Debug.Log(line);
					logEvents.Add (new LogEvent(dt.Subtract(startTime).TotalSeconds, lineElements[1], lineElements[2]));
				}
			}
		}

		startReplay = true;

		//SCREENSHOT CODE
		//if (captureVideo) {
		//	vidcap.enabled = true;
		//	vidcap.Start();
		//}
	}
	
	void Update() {
		//Periodic smiles and eyebrow raises to increase lifelikeness
		nextSmileCounter += Time.deltaTime;
		if (nextSmileCounter >= nextSmileTime) {
			nextSmileCounter = 0;
			nextSmileTime = nextSmileDistribution.NextDouble();
			StartCoroutine(agentSmileAndEyebrow(smileLengthDistribution.NextDouble()));
		}

		if (startReplay)
		{
			counter += Time.deltaTime;
			LogEvent le = logEvents[currentLogIndex];
			if (counter >= le.startTime)
			{
				if (le.type == "Speech Start:")
				{
					Speak (agentName, le.parameter.Trim());
				}
				else if (le.type.Contains("Agent Gaze to User"))
				{
					gazeCtrl.Head.align = 1f;
					if( gazeCtrl.Torso != null )
						gazeCtrl.Torso.align = 0f;
					gazeCtrl.GazeAt(mutualGazeObject);
				}
				else if (le.type == "Agent Gaze to Other:")
				{
					gazeCtrl.Head.align = 0.4f;
					if( gazeCtrl.Torso != null )
						gazeCtrl.Torso.align = 0f;
					gazeCtrl.GazeAt(gridTargets[Convert.ToInt32(le.parameter)]);
				}
				else if (le.type == "Agent Gaze to Reference:")
				{
					gazeCtrl.Head.align = 1f;
					if( gazeCtrl.Torso != null )
						gazeCtrl.Torso.align = 0f;
					gazeCtrl.GazeAt(gridTargets[Convert.ToInt32(le.parameter)]);
				}
				else if (le.type == "Agent Gaze to Ambiguous:")
				{
					gazeCtrl.Head.align = 0.4f;
					if( gazeCtrl.Torso != null )
						gazeCtrl.Torso.align = 0f;
					gazeCtrl.GazeAt(gridTargets[Convert.ToInt32(le.parameter)]);
				}
				else if (le.type == "Agent Gaze to Target:")
				{
					gazeCtrl.Head.align = 1f;
					if( gazeCtrl.Torso != null )
						gazeCtrl.Torso.align = 0f;
					gazeCtrl.GazeAt(gridTargets[Convert.ToInt32(le.parameter)]);
				}
				currentLogIndex++;
			}
		}
	}

	//for periodic smiling and eyebrow raising
	IEnumerator agentSmileAndEyebrow(double smileTime) {
		int curexpr = ChangeExpression(agentName,"ExpressionSmileOpen",0.3f,0.5f);
		yield return StartCoroutine( WaitUntilFinished(curexpr) );
		this[agentName].GetComponent<ExpressionController>().FixExpression();
		ChangeExpression(agentName,"ModifierBrowUp",0.6f,0.3f);
		yield return new WaitForSeconds((float)smileTime / 2.0f);
		curexpr = ChangeExpression(agentName,"ModifierBrowUp",0f,0.3f);
		yield return StartCoroutine( WaitUntilFinished(curexpr) );
		this[agentName].GetComponent<ExpressionController>().UnfixExpression("ExpressionSmileOpen");
		yield return new WaitForSeconds((float)smileTime / 2.0f);
		curexpr = ChangeExpression(agentName,"ExpressionSmileOpen",0f,0.5f);
		yield return StartCoroutine( WaitUntilFinished(curexpr) );
	}

	/// <see cref="Scenario._Finish()"/>
	protected override void _Finish() {}

	public void OnApplicationQuit()
	{
	}
};