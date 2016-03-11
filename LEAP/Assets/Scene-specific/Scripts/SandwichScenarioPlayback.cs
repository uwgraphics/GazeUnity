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

//A struct for a gaze shift event, as read from the csv file
public struct GazeShift {
	public string target;
	public float startTime;
}

//A struct for task events like verbal references and action movements, as read in from the csv files
public struct TaskEvent {
	public string type;
	public string item;
	public float startTime;
}

public class SandwichScenarioPlayback : Scenario
{
	//agents
	private string instructorName = "Jason";
	private string workerName = "Jasmin";

	//CSV files
	public TextAsset workerData;
	public TextAsset instructorData;
	public bool PlaybackInstructorGaze = false;

	public GameObject instructorHead;
	private GameObject[] gridTargets;
	private GameObject[] sceneLights;

	//timing
	private float timeElapsed = 0f;
	private bool startTimer = false;

	//the relevant controllers
	private AsynchronousSocketListener gazeSocket;
	private InteractiveGazeController intGazeCtrl;
	private GazeController workerGazeCtrl;
	private GazeController instructorGazeCtrl;
	
	//Random distributions for smiling and nodding while listening
	private MersenneTwisterRandomSource randNumGen = null; //random number generator for seeding the distributions below
	private NormalDistribution nextSmileDistribution = null;
	private NormalDistribution smileLengthDistribution = null;

	//Random smiling
	private double nextSmileCounter = 0;
	private double nextSmileTime = 0;
	private double nextSmileCounter2 = 0;
	private double nextSmileTime2 = 0;
	
	VideoCapture vidcap;

	//Mapping object names to the grid
	private Dictionary<string, int> ingredientToGrid = new Dictionary<string, int>();
	private Dictionary<string,int[]> ambiguousItems = new Dictionary<string,int[]>();
	private Dictionary<string,int[]> otherItems = new Dictionary<string,int[]>();

	//Lists of the events
	private List<GazeShift> workerGazeShifts;
	private List<TaskEvent> taskEvents;
	private List<GazeShift> instructorGazeShifts;

	//Keep track of the current and next reference object
	private string currentReferenceObject = null;
	private string nextReferenceObject = null;

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
		
		vidcap = GetComponent<VideoCapture>();

		//Map ingredient names to the grid (some grid spaces have multiple items associated with them)
		ingredientToGrid.Add("WhiteCircle",1);
		ingredientToGrid.Add("Mustard",2);
		ingredientToGrid.Add("PinkCircle2",3);
		ingredientToGrid.Add("Cheese2",4);
		ingredientToGrid.Add("Pickles3",5);
		ingredientToGrid.Add("Lettuce2",5);
		ingredientToGrid.Add("Jelly",6);
		ingredientToGrid.Add("Tomato2",7);
		ingredientToGrid.Add("Pickles1",7); 
		ingredientToGrid.Add("Bacon2",8);
		ingredientToGrid.Add("Pickles2",9);
		ingredientToGrid.Add("Onions",9);
		ingredientToGrid.Add("Egg",10);
		ingredientToGrid.Add("Cookie",10);
		ingredientToGrid.Add("Bacon1",11);
		ingredientToGrid.Add("BrownBlob",11);
		ingredientToGrid.Add("Target",12);
		ingredientToGrid.Add("Lettuce1",13);
		ingredientToGrid.Add("BrownCircle",14);
		ingredientToGrid.Add("Cheese1",15);
		ingredientToGrid.Add("Mayo",16);
		ingredientToGrid.Add("PinkCircle1",17);
		ingredientToGrid.Add("Tomato1",18);
		ingredientToGrid.Add("Ketchup",18);

		//Specify the ambiguous items for each object
		ambiguousItems.Add("WhiteCircle", new int[0]);
		ambiguousItems.Add("Mustard", new int[0]);
		ambiguousItems.Add("PinkCircle2", new int[1]{ingredientToGrid["PinkCircle1"]});
		ambiguousItems.Add("Cheese2", new int[1]{ingredientToGrid["Cheese1"]});
		ambiguousItems.Add("Pickles3", new int[2]{ingredientToGrid["Pickles1"],ingredientToGrid["Pickles2"]});
		ambiguousItems.Add("Lettuce2", new int[1]{ingredientToGrid["Lettuce1"]});
		ambiguousItems.Add("Jelly", new int[0]);
		ambiguousItems.Add("Tomato2", new int[1]{ingredientToGrid["Tomato1"]});
		ambiguousItems.Add("Pickles1", new int[2]{ingredientToGrid["Pickles2"],ingredientToGrid["Pickles3"]});
		ambiguousItems.Add("Bacon2", new int[1]{ingredientToGrid["Bacon1"]});
		ambiguousItems.Add("Pickles2", new int[2]{ingredientToGrid["Pickles1"],ingredientToGrid["Pickles3"]});
		ambiguousItems.Add("Onions", new int[0]);
		ambiguousItems.Add("Egg", new int[0]);
		ambiguousItems.Add("Cookie", new int[0]);
		ambiguousItems.Add("Bacon1", new int[1]{ingredientToGrid["Bacon2"]});
		ambiguousItems.Add("BrownBlob", new int[1]{ingredientToGrid["BrownCircle"]});
		ambiguousItems.Add("Lettuce1", new int[1]{ingredientToGrid["Lettuce2"]});
		ambiguousItems.Add("BrownCircle", new int[1]{ingredientToGrid["BrownBlob"]});
		ambiguousItems.Add("Cheese1", new int[1]{ingredientToGrid["Cheese2"]});
		ambiguousItems.Add("Mayo", new int[0]);
		ambiguousItems.Add("PinkCircle1", new int[1]{ingredientToGrid["PinkCircle2"]});
		ambiguousItems.Add("Tomato1", new int[1]{ingredientToGrid["Tomato2"]});
		ambiguousItems.Add("Ketchup", new int[0]);

		//Create lists of all the "other" items for each object
		otherItems.Add("WhiteCircle", new int[16]{2,3,4,5,6,7,8,9,10,11,13,14,15,16,17,18});
		otherItems.Add("Mustard", new int[16]{1,3,4,5,6,7,8,9,10,11,13,14,15,16,17,18});
		otherItems.Add("PinkCircle2", new int[16]{1,2,4,5,6,7,8,9,10,11,13,14,15,16,17,18});
		otherItems.Add("Cheese2", new int[16]{1,2,3,5,6,7,8,9,10,11,13,14,15,16,17,18});
		otherItems.Add("Pickles3", new int[16]{1,2,3,4,6,7,8,9,10,11,13,14,15,16,17,18});
		otherItems.Add("Lettuce2", new int[16]{1,2,3,4,6,7,8,9,10,11,13,14,15,16,17,18});
		otherItems.Add("Jelly", new int[16]{1,2,3,4,5,7,8,9,10,11,13,14,15,16,17,18});
		otherItems.Add("Tomato2", new int[16]{1,2,3,4,5,6,8,9,10,11,13,14,15,16,17,18});
		otherItems.Add("Pickles1", new int[16]{1,2,3,4,5,6,8,9,10,11,13,14,15,16,17,18});
		otherItems.Add("Bacon2", new int[16]{1,2,3,4,5,6,7,9,10,11,13,14,15,16,17,18});
		otherItems.Add("Pickles2", new int[16]{1,2,3,4,5,6,7,8,10,11,13,14,15,16,17,18});
		otherItems.Add("Onions", new int[16]{1,2,3,4,5,6,7,8,10,11,13,14,15,16,17,18});
		otherItems.Add("Egg", new int[16]{1,2,3,4,5,6,7,8,9,11,13,14,15,16,17,18});
		otherItems.Add("Cookie", new int[16]{1,2,3,4,5,6,7,8,9,11,13,14,15,16,17,18});
		otherItems.Add("Bacon1", new int[16]{1,2,3,4,5,6,7,8,9,10,13,14,15,16,17,18});
		otherItems.Add("BrownBlob", new int[16]{1,2,3,4,5,6,7,8,9,10,13,14,15,16,17,18});
		otherItems.Add("Lettuce1", new int[16]{1,2,3,4,5,6,7,8,9,10,11,14,15,16,17,18});
		otherItems.Add("BrownCircle", new int[16]{1,2,3,4,5,6,7,8,9,10,11,13,15,16,17,18});
		otherItems.Add("Cheese1", new int[16]{1,2,3,4,5,6,7,8,9,10,11,13,14,16,17,18});
		otherItems.Add("Mayo", new int[16]{1,2,3,4,5,6,7,8,9,10,11,13,14,15,17,18});
		otherItems.Add("PinkCircle1", new int[16]{1,2,3,4,5,6,7,8,9,10,11,13,14,15,16,18});
		otherItems.Add("Tomato1", new int[16]{1,2,3,4,5,6,7,8,9,10,11,13,14,15,16,17});
		otherItems.Add("Ketchup", new int[16]{1,2,3,4,5,6,7,8,9,10,11,13,14,15,16,17});
		//subtract the ambiguous items
		otherItems["PinkCircle2"] = otherItems["PinkCircle2"].Except(ambiguousItems["PinkCircle2"]).ToArray();
		otherItems["Cheese2"] = otherItems["Cheese2"].Except(ambiguousItems["Cheese2"]).ToArray();
		otherItems["Pickles3"] = otherItems["Pickles3"].Except(ambiguousItems["Pickles3"]).ToArray();
		otherItems["Lettuce2"] = otherItems["Lettuce2"].Except(ambiguousItems["Lettuce2"]).ToArray();
		otherItems["Tomato2"] = otherItems["Tomato2"].Except(ambiguousItems["Tomato2"]).ToArray();
		otherItems["Pickles1"] = otherItems["Pickles1"].Except(ambiguousItems["Pickles1"]).ToArray();
		otherItems["Bacon2"] = otherItems["Bacon2"].Except(ambiguousItems["Bacon2"]).ToArray();
		otherItems["Pickles2"] = otherItems["Pickles2"].Except(ambiguousItems["Pickles2"]).ToArray();
		otherItems["Bacon1"] = otherItems["Bacon1"].Except(ambiguousItems["Bacon1"]).ToArray();
		otherItems["BrownBlob"] = otherItems["BrownBlob"].Except(ambiguousItems["BrownBlob"]).ToArray();
		otherItems["Lettuce1"] = otherItems["Lettuce1"].Except(ambiguousItems["Lettuce1"]).ToArray();
		otherItems["BrownCircle"] = otherItems["BrownCircle"].Except(ambiguousItems["BrownCircle"]).ToArray();
		otherItems["Cheese1"] = otherItems["Cheese1"].Except(ambiguousItems["Cheese1"]).ToArray();
		otherItems["PinkCircle1"] = otherItems["PinkCircle1"].Except(ambiguousItems["PinkCircle1"]).ToArray();
		otherItems["Tomato1"] = otherItems["Tomato1"].Except(ambiguousItems["Tomato1"]).ToArray();

		//Read in the worker gaze shift events and worker movement events
		workerGazeShifts = new List<GazeShift>();
		taskEvents = new List<TaskEvent>();
		instructorGazeShifts = new List<GazeShift>();
		string[] workerDataLines = workerData.text.Split("\n"[0]);
		foreach (string line in workerDataLines) {
			string[] lineElements = line.Split (","[0]);
			if (lineElements.Length == 4) {
				if (lineElements[0] == "Gaze_Objects") {
					int s = Int32.Parse (lineElements[2]);
					if (s >= 0) {
						workerGazeShifts.Add (new GazeShift(){target=lineElements[1],startTime=((float)s)/1000f});
					}
				}
				else if (lineElements[0] == "Gaze_Person") {
					int s = Int32.Parse (lineElements[2]);
					if (s >= 0) {
						workerGazeShifts.Add (new GazeShift(){target="Person",startTime=((float)s)/1000f});
					}
				}
				else if (lineElements[0] == "Gaze_Target") {
					int s = Int32.Parse (lineElements[2]);
					if (s >= 0) {
						workerGazeShifts.Add (new GazeShift(){target="Target",startTime=((float)s)/1000f});
					}
				}
				else if (lineElements[0] == "Movement") {
					taskEvents.Add (new TaskEvent(){type="Action",item=lineElements[1],startTime=((float)Int32.Parse (lineElements[2]))/1000f});
					taskEvents.Add (new TaskEvent(){type="ActionEnd",item=lineElements[1],startTime=((float)Int32.Parse (lineElements[3]))/1000f});
				}
			}
		}
		workerGazeShifts.Sort ((g1,g2) => g1.startTime.CompareTo(g2.startTime));

		//Read in the instructor reference and gaze events
		string[] instructorDataLines = instructorData.text.Split("\n"[0]);
		foreach (string line in instructorDataLines) {
			string[] lineElements = line.Split(","[0]);
			if (lineElements.Length == 4) {
				if (lineElements[0] == "object") {
					if (nextReferenceObject == null) {
						nextReferenceObject = lineElements[1];
					}
					taskEvents.Add (new TaskEvent(){type="Reference",item=lineElements[1],startTime=((float)Int32.Parse (lineElements[2]))/1000f});
					taskEvents.Add (new TaskEvent(){type="ReferenceEnd",item=lineElements[1],startTime=((float)Int32.Parse (lineElements[3]))/1000f});
				}
				else if (lineElements[0] == "Gaze_Objects") {
					int s = Int32.Parse (lineElements[2]);
					if (s >= 0) {
						instructorGazeShifts.Add (new GazeShift(){target=lineElements[1],startTime=((float)s)/1000f});
					}
				}
				else if (lineElements[0] == "Gaze_Person") {
					int s = Int32.Parse (lineElements[2]);
					if (s >= 0) {
						instructorGazeShifts.Add (new GazeShift(){target="Person",startTime=((float)s)/1000f});
					}
				}
				else if (lineElements[0] == "Gaze_Target") {
					int s = Int32.Parse (lineElements[2]);
					if (s >= 0) {
						instructorGazeShifts.Add (new GazeShift(){target="Target",startTime=((float)s)/1000f});
					}
				}
			}
		}
		taskEvents.Sort ((g1,g2) => g1.startTime.CompareTo(g2.startTime));
		instructorGazeShifts.Sort ((g1,g2) => g1.startTime.CompareTo(g2.startTime));
	}
	
	/// <see cref="Scenario._Run()"/>
	protected override IEnumerator _Run()
	{
		//speech and gaze behaviors
		int curspeak = -1;
		int curgaze = -1;

		//Find the relevant controllers
		intGazeCtrl = agents[instructorName].GetComponent<InteractiveGazeController>();
		workerGazeCtrl = agents[workerName].GetComponent<GazeController>();
		instructorGazeCtrl = agents[instructorName].GetComponent<GazeController>();
		AsynchronousSocketListener[] allSocketListeners = agents[instructorName].GetComponents<AsynchronousSocketListener>();
		foreach (AsynchronousSocketListener asl in allSocketListeners) {
			if (asl.ID == SocketSource.GazeTracker) {
				gazeSocket = asl;
			}
		}
		
		//string timestamp = DateTime.Now.ToString("yyyy_MM_dd_HH-mm-ss_") + intGazeCtrl.condition.ToString();

		//set the scene lights
		sceneLights = GameObject.FindGameObjectsWithTag("Spotlight");
		float[] lightIntensities = new float[sceneLights.Length];
		for (int i = 0; i < sceneLights.Length; ++i) {
			lightIntensities[i] = sceneLights[i].light.intensity;
			sceneLights[i].light.intensity = 0f;	
		}
		
		// Initialize instructor gaze
		yield return new WaitForSeconds(0.6f);
		curgaze = GazeAt(instructorName, intGazeCtrl.mutualGazeObject, 0.8f, 0f);
		yield return StartCoroutine( WaitUntilFinished(curgaze) );
		curgaze = GazeAt(instructorName, intGazeCtrl.mutualGazeObject, 0.8f, 0f);
		yield return StartCoroutine( WaitUntilFinished(curgaze) );
		curgaze = GazeAt(instructorName, intGazeCtrl.mutualGazeObject, 1.0f, 0f);
		yield return StartCoroutine( WaitUntilFinished(curgaze) );
		// Initialize worker gaze
		curgaze = GazeAt(workerName, instructorHead, 0.8f, 0f);
		yield return StartCoroutine( WaitUntilFinished(curgaze) );
		curgaze = GazeAt(workerName, instructorHead, 0.8f, 0f);
		yield return StartCoroutine( WaitUntilFinished(curgaze) );
		curgaze = GazeAt(workerName, instructorHead, 1.0f, 0f);
		yield return StartCoroutine( WaitUntilFinished(curgaze) );
	
		//SCREENSHOT CODE
		vidcap.Start_Capture("fullmodel-instructor");

		//bring the lights back up
		for (int i = 0; i < sceneLights.Length; ++i) {
			sceneLights[i].light.intensity = lightIntensities[i];
		}

		startTimer = true;
		intGazeCtrl.changePhase(ReferencePhase.PreReference);
		intGazeCtrl.setReferenceObject(ingredientToGrid[nextReferenceObject],ambiguousItems[nextReferenceObject],otherItems[nextReferenceObject]);


		yield return new WaitForSeconds(1f);
		//Start the audio track
		curspeak = Speak (instructorName, "4B-instructor-visible");
		yield return StartCoroutine(WaitUntilFinished(curspeak));

		//Dim the lights
		foreach (GameObject g in sceneLights) {
			g.light.intensity = 0f;	
		}
	}
	
	void Update() {
		if (startTimer) {
			timeElapsed += Time.deltaTime;
			//Execute the worker gazes
			if (workerGazeShifts.Count > 0 && timeElapsed >= workerGazeShifts[0].startTime) {
				if (workerGazeShifts[0].target == "Person") {
					gazeSocket.SocketContent = "Grid: 99";
					workerGazeCtrl.Head.align = 1f;
					workerGazeCtrl.GazeAt (instructorHead);
				}
				else {
					gazeSocket.SocketContent = "Grid: " + (ingredientToGrid[workerGazeShifts[0].target]-1);
					if (ingredientToGrid[workerGazeShifts[0].target] == intGazeCtrl.getReferenceIndex())
						workerGazeCtrl.Head.align = 1f;
					else
						workerGazeCtrl.Head.align = 0.1f;
					workerGazeCtrl.GazeAt (gridTargets[ingredientToGrid[workerGazeShifts[0].target]]);
				}
				workerGazeShifts.RemoveAt(0);
			}

			//Execute the instructor gazes if we are playing back existing data
			if (PlaybackInstructorGaze) {
				if (instructorGazeShifts.Count > 0 && timeElapsed >= instructorGazeShifts[0].startTime) {
					if (instructorGazeShifts[0].target == "Person") {
						instructorGazeCtrl.Head.align = 1f;
						instructorGazeCtrl.GazeAt (intGazeCtrl.mutualGazeObject);
					}
					else {
						if (ingredientToGrid[instructorGazeShifts[0].target] == intGazeCtrl.getReferenceIndex())
							instructorGazeCtrl.Head.align = 1f;
						else
							instructorGazeCtrl.Head.align = 0.1f;
						instructorGazeCtrl.GazeAt (gridTargets[ingredientToGrid[instructorGazeShifts[0].target]]);
					}
					instructorGazeShifts.RemoveAt(0);
				}
			}

			//Execute task events
			if (taskEvents.Count > 0 && timeElapsed >= taskEvents[0].startTime) {
				TaskEvent currentTaskEvent = taskEvents[0];
				taskEvents.RemoveAt(0);
				if (currentTaskEvent.type == "Reference") {
					if (currentTaskEvent.item == currentReferenceObject) { //This item was already referenced, so this is a refinement
						intGazeCtrl.changePhase (ReferencePhase.Refinement);
					}
					else {
						intGazeCtrl.changePhase (ReferencePhase.Reference);
						currentReferenceObject = currentTaskEvent.item;
						foreach (TaskEvent t in taskEvents) {
							if (t.type == "Reference") {
								nextReferenceObject = t.item;
								break;
							}
						}
					}
				}
				else if (currentTaskEvent.type == "ReferenceEnd") {
					intGazeCtrl.changePhase (ReferencePhase.Monitor);
				}
				else if (currentTaskEvent.type == "Action") {
					intGazeCtrl.changePhase (ReferencePhase.Action);
				}
				else if (currentTaskEvent.type == "ActionEnd") {
					intGazeCtrl.changePhase (ReferencePhase.PreReference);
					//Setup the next reference-action sequence
					intGazeCtrl.setReferenceObject(ingredientToGrid[nextReferenceObject],ambiguousItems[nextReferenceObject],otherItems[nextReferenceObject]);
				}
			}

			if (taskEvents.Count == 0) {
				foreach (GameObject g in sceneLights) {
					g.light.intensity = 0f;	
				}
			}
		}

		//Periodic smiles and eyebrow raises to increase lifelikeness
		nextSmileCounter += Time.deltaTime;
		if (nextSmileCounter >= nextSmileTime) {
			nextSmileCounter = 0;
			nextSmileTime = nextSmileDistribution.NextDouble();
			StartCoroutine(agentSmileAndEyebrow(smileLengthDistribution.NextDouble()));
		}

		nextSmileCounter2 += Time.deltaTime;
		if (nextSmileCounter2 >= nextSmileTime2) {
			nextSmileCounter2 = 0;
			nextSmileTime2 = nextSmileDistribution.NextDouble();
			StartCoroutine(agentSmileAndEyebrow2(smileLengthDistribution.NextDouble()));
		}
	}

	//for periodic smiling and eyebrow raising
	IEnumerator agentSmileAndEyebrow(double smileTime) {
		int curexpr = ChangeExpression(workerName,"ExpressionSmileOpen",0.5f,0.1f);
		yield return StartCoroutine( WaitUntilFinished(curexpr) );
		this[workerName].GetComponent<ExpressionController>().FixExpression();
		ChangeExpression(workerName,"ModifierBrowUp",0.6f,0.3f);
		yield return new WaitForSeconds((float)smileTime / 2.0f);
		curexpr = ChangeExpression(workerName,"ModifierBrowUp",0f,0.3f);
		yield return StartCoroutine( WaitUntilFinished(curexpr) );
		this[workerName].GetComponent<ExpressionController>().UnfixExpression("ExpressionSmileOpen");
		yield return new WaitForSeconds((float)smileTime / 2.0f);
		curexpr = ChangeExpression(workerName,"ExpressionSmileOpen",0f,0.5f);
		yield return StartCoroutine( WaitUntilFinished(curexpr) );
	}

	//for periodic smiling and eyebrow raising
	IEnumerator agentSmileAndEyebrow2(double smileTime) {
		int curexpr = ChangeExpression(instructorName,"ExpressionSmileOpen",0.5f,0.1f);
		yield return StartCoroutine( WaitUntilFinished(curexpr) );
		this[instructorName].GetComponent<ExpressionController>().FixExpression();
		ChangeExpression(instructorName,"ModifierBrowUp",0.6f,0.3f);
		yield return new WaitForSeconds((float)smileTime / 2.0f);
		curexpr = ChangeExpression(instructorName,"ModifierBrowUp",0f,0.3f);
		yield return StartCoroutine( WaitUntilFinished(curexpr) );
		this[instructorName].GetComponent<ExpressionController>().UnfixExpression("ExpressionSmileOpen");
		yield return new WaitForSeconds((float)smileTime / 2.0f);
		curexpr = ChangeExpression(instructorName,"ExpressionSmileOpen",0f,0.5f);
		yield return StartCoroutine( WaitUntilFinished(curexpr) );
	}
	
	/// <see cref="Scenario._Finish()"/>
	protected override void _Finish()
	{
	}
};