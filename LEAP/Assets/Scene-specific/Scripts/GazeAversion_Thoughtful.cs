using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System;
using MathNet.Numerics;
using MathNet.Numerics.Distributions;
using MathNet.Numerics.RandomSources;

public class GazeAversion_Thoughtful : Scenario
{
	private string agentName = "Jasmin";
	private ListenController listenCtrl;
	
	//Random distributions for smiling and nodding while listening
	private MersenneTwisterRandomSource randNumGen = null; //random number generator for seeding the distributions below
	private NormalDistribution nextSmileDistribution = null;
	private NormalDistribution smileLengthDistribution = null;
	private NormalDistribution nextNodDistribution = null;
	private double nextSmileCounter = 0;
	private double nextNodCounter = 0;
	private double nextSmileTime = 0;
	private double nextNodTime = 0;
	public bool captureVideo = false;
	VideoCapture vidcap;
	

	/// <see cref="Scenario._Init()"/>
	protected override void _Init()
	{
		randNumGen = new MersenneTwisterRandomSource();
		nextSmileDistribution = new NormalDistribution(randNumGen);
		nextSmileDistribution.SetDistributionParameters(20.0, 5.0);
		smileLengthDistribution = new NormalDistribution(randNumGen);
		smileLengthDistribution.SetDistributionParameters(5.0, 1.0);
		nextNodDistribution = new NormalDistribution(randNumGen);
		nextNodDistribution.SetDistributionParameters(10.0, 3.0);
		
		nextSmileTime = nextSmileDistribution.NextDouble();
		nextNodTime = nextNodDistribution.NextDouble();
		
		vidcap = GetComponent<VideoCapture>();
		vidcap.enabled = false;
	}
	
	/// <see cref="Scenario._Run()"/>
	protected override IEnumerator _Run()
	{
		int curspeak = -1;
		int curgaze = -1;
		//int curlisten = -1;
		
		GazeAversionController gactrl = agents[agentName].GetComponent<GazeAversionController>();
		//GazeController gazectrl = agents[agentName].GetComponent<GazeController>();
		listenCtrl = agents[agentName].GetComponent<ListenController>();
		//SpeechController speechCtrl = agents[agentName].GetComponent<SpeechController>();
		
		//string timestamp = DateTime.Now.ToString("yyyy_MM_dd_HH-mm-ss_") + "Thoughtful_Jasmin_" + gactrl.condition.ToString();
		
		GameObject[] lights = GameObject.FindGameObjectsWithTag("Spotlight");
		float[] lightIntensities = new float[lights.Length];
		for (int i = 0; i < lights.Length; ++i) {
			lightIntensities[i] = lights[i].light.intensity;
			lights[i].light.intensity = 0f;	
		}
		
		// Initialize gaze
		yield return new WaitForSeconds(0.6f);
		if (gactrl.condition != GazeAversionCondition.BadModel) {
			curgaze = GazeAt(agentName, gactrl.mutualGazeObject, 0.8f, 0f);
			yield return StartCoroutine( WaitUntilFinished(curgaze) );
			curgaze = GazeAt(agentName, gactrl.mutualGazeObject, 0.8f, 0f);
			yield return StartCoroutine( WaitUntilFinished(curgaze) );
			curgaze = GazeAt(agentName, gactrl.mutualGazeObject, 1.0f, 0f);
			yield return StartCoroutine( WaitUntilFinished(curgaze) );
		}
		else {
			curgaze = GazeAt(agentName, GameObject.Find ("GazeLeft"), 0.8f, 0f);
			yield return StartCoroutine( WaitUntilFinished(curgaze) );
			curgaze = GazeAt(agentName, GameObject.Find ("GazeLeft"), 0.8f, 0f);
			yield return StartCoroutine( WaitUntilFinished(curgaze) );
			curgaze = GazeAt(agentName, GameObject.Find ("GazeLeft"), 1.0f, 0f);
			yield return StartCoroutine( WaitUntilFinished(curgaze) );
		}
		
		
		GameObject.Find ("Shield").SetActive(false);
		
		for (int i = 0; i < lights.Length; ++i) {
			lights[i].light.intensity = lightIntensities[i];	
		}
		
		//CODE FOR MAKING SCREENSHOT:
//		if (captureVideo) {
//			vidcap.enabled = true;
//			vidcap.Start();
//		}
			
		yield return new WaitForSeconds(6f);
		curspeak = Speak(agentName, "thoughtfulness_1", SpeechType.Answer, true);
		yield return StartCoroutine(WaitUntilFinished(curspeak));
		yield return new WaitForSeconds(1f);
		vidcap.enabled = false;
		
		//Mouse click starts the scenario
		/*while (!ListenController.mouseClicked) {
			yield return 0;	
		}
		listenCtrl.StartLogTiming();
		gactrl.StartLogTiming();
		speechCtrl.StartLogTiming();
		ListenController.mouseClicked = false;
		gactrl.resetTime();
		
		while (listenCtrl.utterancesLeft() > 0) {
			curlisten = Listen(agentName);
			yield return StartCoroutine( WaitUntilFinished(curlisten) );
			curspeak = Speak(agentName, listenCtrl.response, SpeechType.Answer, true);
			yield return StartCoroutine(WaitUntilFinished(curspeak));
			if (listenCtrl.utterancesLeft() > 0) {
				ListenController.mouseClicked = false;
				while (!ListenController.mouseClicked) {
					yield return 0;
				}
			}
			CancelAction(curlisten);
			//Get back into the "Not Listening" state
			while (listenCtrl.StateId != (int)ListenState.NotListening) {
				yield return 0;	
			}
		}
		
		using (StreamWriter outfile = new StreamWriter(Application.dataPath + @"\" + timestamp + ".txt")) {
			outfile.Write (sb.ToString());
		}
		
		foreach (GameObject g in lights) {
			g.light.intensity = 0f;	
		}
		GameObject.Find ("FinalInstructions").guiText.enabled = true;*/
	}
	
	void Update() {
		nextSmileCounter += Time.deltaTime;
		if (nextSmileCounter >= nextSmileTime) {
			nextSmileCounter = 0;
			nextSmileTime = nextSmileDistribution.NextDouble();
			StartCoroutine(agentSmileAndEyebrow(smileLengthDistribution.NextDouble()));
		}
		
		if (listenCtrl.StateId != (int)ListenState.NotListening) {
			nextNodCounter += Time.deltaTime;
			if (nextNodCounter >= nextNodTime) {
				nextNodCounter = 0f;
				nextNodTime = nextNodDistribution.NextDouble();
				HeadNod(agentName,1,FaceGestureSpeed.Slow,3f,0f);
			}
		}
	}
	
	IEnumerator agentSmileAndEyebrow(double smileTime) {
		int curexpr = ChangeExpression(agentName,"ExpressionSmileOpen",0.5f,0.1f);
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
	protected override void _Finish()
	{
	}
};