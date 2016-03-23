using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using MathNet.Numerics;
using MathNet.Numerics.Distributions;
using MathNet.Numerics.RandomSources;
using System.Text;
using System.Linq;

//Captures the current focus of the agent's attention
public enum InteractiveGazeState
{
	PersonGaze,
	ReferenceGaze,
	AmbiguousGaze,
	OtherGaze,
	TargetGaze
};

//Different conditions for the evaluation
public enum InteractiveGazeCondition
{
	None,
	RandomGaze,
	NoGazeDetection,
	NoGazeProduction,
	FullModel
};

//Each reference-action sequence is divided into several phases
public enum ReferencePhase
{
	None,
	PreReference,
	Reference,
	Monitor,
	Refinement,
	Action
};

public class InteractiveGazeController : AnimController
{
	//references to other controllers
	protected GazeController gazeCtrl = null;
	protected AsynchronousSocketListener gazeListener = null;
	protected HeadGazeTracker headGazeCtrl = null;
    private OculusGazeTracker oculusGazeTracker = null;
    public bool useHeadTracker = false;
    public bool useOculus = false;

	//important grid indices
	protected GameObject[] gridTargets = null;
	private int referenceIndex = 0;
	private int targetIndex = 12;
	private int[] ambiguousIndices = null;
	private int[] otherIndices = null;
	
	//private int currentUserGaze = 0; //0 - "no" gaze, 1-18 - grid targets, 19 - agent
	private List<int> currentUserGaze = new List<int>();
	private int currentAgentGaze = 0;
	public InteractiveGazeCondition condition = InteractiveGazeCondition.None;
	protected InteractiveGazeState gazeState = InteractiveGazeState.PersonGaze;
	public ReferencePhase phase = ReferencePhase.None;

	private bool gazeShared = false;
	private bool gazeMutual = false;
	private double gazeSharedLength = 0;
	private double gazeMutualLength = 0;

	[HideInInspector]
	public StreamWriter LogStream = null;

	public GameObject mutualGazeObject = null;
	private double gazeLength;
	protected float timeElapsed = 0f;
	private bool offerRefinementFlag = false;
	private bool waitingForActionFlag = false;
	private double waitingForActionTime = 0.0;
	private double waitingForActionLength = 2.0;
	private bool waitingForReferenceGazeFlag = false;
	private double waitingForReferenceGazeTime = 0.0;
	private double waitingForReferenceGazeLength = 1.0;
	private bool nextGazeToTarget = false;
	
	//Distributions for drawing timing parameters from
	protected MersenneTwisterRandomSource randNumGen = null; //random number generator for seeding the distributions below
	protected NormalDistribution referenceGazeLength = null;
	protected NormalDistribution personGazeLength = null;
	protected NormalDistribution ambiguousGazeLength = null;
	protected NormalDistribution otherGazeLength = null;
	protected NormalDistribution targetGazeLength = null;

	//Transition probabilities
	private double referenceTransitionProbability;
	private double ambiguousTransitionProbability;
	private double otherTransitionProbability;
	private double personTransitionProbability;
	private double targetTransitionProbability;
	
	protected override void _Init()
	{
		//init the reference controllers
		gazeCtrl = Parent as GazeController;
		headGazeCtrl = gameObject.GetComponent<HeadGazeTracker>();
		AsynchronousSocketListener[] allSocketListeners = gameObject.GetComponents<AsynchronousSocketListener>();
		foreach (AsynchronousSocketListener asl in allSocketListeners) {
			if (asl.ID == SocketSource.GazeTracker) {
				gazeListener = asl;
			}
		}
        oculusGazeTracker = gameObject.GetComponent<OculusGazeTracker>();

		//Initialize distributions
		randNumGen = new MersenneTwisterRandomSource();
		referenceGazeLength = new NormalDistribution(randNumGen);
		personGazeLength = new NormalDistribution(randNumGen);
		ambiguousGazeLength = new NormalDistribution(randNumGen);
		otherGazeLength = new NormalDistribution(randNumGen);
		targetGazeLength = new NormalDistribution(randNumGen);
		 
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
		currentAgentGaze = 19;
		currentUserGaze.Add (0);
	}

	//A function for identifying the reference object, ambiguous objects, and other objects
	public void setReferenceObject(int rI, int[] aI, int[] oI) {
		referenceIndex = rI;
		ambiguousIndices = aI;
		otherIndices = oI;

		if (LogStream != null)
		{
			LogStream.WriteLine(String.Format ("{0:HH:mm:ss.ffff}\tReference Object:\t{1}", DateTime.Now, referenceIndex));
			LogStream.WriteLine(String.Format ("{0:HH:mm:ss.ffff}\tAmbiguous Objects:\t{1}", DateTime.Now, String.Join(",", Array.ConvertAll(ambiguousIndices, x => x.ToString()))));
		}
	}

    public void StartHeadTrackerCalibration()
    {
        if (headGazeCtrl != null)
        {
            headGazeCtrl.StartCalibration();
        }
    }

    private bool CurrentConditionHasGazeOutput()
    {
        if (condition == InteractiveGazeCondition.FullModel || condition == InteractiveGazeCondition.NoGazeDetection || condition == InteractiveGazeCondition.RandomGaze)
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    private bool CurrentConditionHasGazeInput()
    {
        if (condition == InteractiveGazeCondition.FullModel || condition == InteractiveGazeCondition.NoGazeProduction)
        {
            return true;
        }
        else
        {
            return false;
        }
    }

	public bool isOfferRefinementFlagSet() {
		return offerRefinementFlag;
	}

	public int getReferenceIndex() {
		return referenceIndex;
	}

	public virtual void resetTime() {
		timeElapsed = 0f;	
	}

	public void triggerReferenceGaze() {
		if (CurrentConditionHasGazeOutput())
        {
			//If the agent is already looking at the reference, just reset the timer so they don't immediately look away
			if (gazeState == InteractiveGazeState.ReferenceGaze)
            {
				resetTime ();
			}
			//Otherwise, look at the reference object
			else
            {
				GoToState((int)InteractiveGazeState.ReferenceGaze);
			}
		}
	}

	public void changePhase(ReferencePhase p) {
		phase = p;

		if ((LogStream != null) && (phase != ReferencePhase.None))
		{
			LogStream.WriteLine(String.Format ("{0:HH:mm:ss.ffff}\tPhase Start:\t{1}", DateTime.Now, phase.ToString()));
		}

		//Each phase has different parameters for the gaze lengths and probability of transitioning to gaze targets
		switch(phase) {
		case ReferencePhase.PreReference:
			referenceGazeLength.SetDistributionParameters(0.85, 0.75);
			personGazeLength.SetDistributionParameters(0.65, 0.6);
			ambiguousGazeLength.SetDistributionParameters(0.45, 0.3);
			otherGazeLength.SetDistributionParameters(0.35, 0.2);
			break;
		case ReferencePhase.Reference:
			referenceGazeLength.SetDistributionParameters(1.1, 0.8);
			personGazeLength.SetDistributionParameters(0.6, 0.45);
			ambiguousGazeLength.SetDistributionParameters(0.5, 0.2);
			otherGazeLength.SetDistributionParameters(0.45, 0.25);
			break;
		case ReferencePhase.Monitor:
			referenceGazeLength.SetDistributionParameters(1.2, 0.9);
			personGazeLength.SetDistributionParameters(1.7, 0.5);
			ambiguousGazeLength.SetDistributionParameters(0.6, 0.3);
			otherGazeLength.SetDistributionParameters(0.47, 0.3);
			break;
		case ReferencePhase.Refinement:
			referenceGazeLength.SetDistributionParameters(1.2, 1.1);
			personGazeLength.SetDistributionParameters(0.57, 0.3);
			ambiguousGazeLength.SetDistributionParameters(0.53, 0.4);
			otherGazeLength.SetDistributionParameters(0.4, 0.2);
			waitingForActionFlag = false;
			waitingForActionTime = 0.0;
			offerRefinementFlag = false;
			waitingForReferenceGazeFlag = false;
			waitingForReferenceGazeTime = 0.0;
			break;
		case ReferencePhase.Action:
			targetGazeLength.SetDistributionParameters(0.86, 0.5);
			personGazeLength.SetDistributionParameters(0.6, 0.25);
			otherGazeLength.SetDistributionParameters(0.66, 0.6);
			waitingForActionFlag = false;
			waitingForActionTime = 0.0;
			offerRefinementFlag = false;
			waitingForReferenceGazeFlag = false;
			waitingForReferenceGazeTime = 0.0;
			break;
		};

		if (condition == InteractiveGazeCondition.RandomGaze) {
			referenceGazeLength.SetDistributionParameters(0.75, 0.4);
			personGazeLength.SetDistributionParameters(0.75, 0.4);
			ambiguousGazeLength.SetDistributionParameters(0.75, 0.4);
			otherGazeLength.SetDistributionParameters(0.75, 0.4);
			targetGazeLength.SetDistributionParameters(0.75, 0.4);
		}
		setTransitionProbabilities();
	}

	//The probability of making specific gaze shifts varies across reference phases
	private void setTransitionProbabilities() {
		switch(phase) {
		case ReferencePhase.PreReference:
			otherTransitionProbability = 0.57;
			ambiguousTransitionProbability = 0;
			referenceTransitionProbability = 0.4;
			personTransitionProbability = 0.03;
			targetTransitionProbability = 0;
			break;
		case ReferencePhase.Reference:
			otherTransitionProbability = 0.41;
			ambiguousTransitionProbability = 0;
			referenceTransitionProbability = 0.48;
			personTransitionProbability = 0.11;
			targetTransitionProbability = 0;
			break;
		case ReferencePhase.Monitor:
			otherTransitionProbability = 0.34;
			ambiguousTransitionProbability = 0.02;
			referenceTransitionProbability = 0.49;
			personTransitionProbability = 0.14;
			targetTransitionProbability = 0;
			break;
		case ReferencePhase.Refinement:
			otherTransitionProbability = 0.35;
			ambiguousTransitionProbability = 0.03;
			referenceTransitionProbability = 0.47;
			personTransitionProbability = 0.15;
			targetTransitionProbability = 0;
			break;
		case ReferencePhase.Action:
			if (nextGazeToTarget) { //we need to ensure that the next gaze is to the target
				otherTransitionProbability = 0;
				ambiguousTransitionProbability = 0;
				referenceTransitionProbability = 0;
				personTransitionProbability = 0;
				targetTransitionProbability = 1.0;
				nextGazeToTarget = false;
			}
			otherTransitionProbability = 0.65;
			ambiguousTransitionProbability = 0;
			referenceTransitionProbability = 0;
			personTransitionProbability = 0.11;
			targetTransitionProbability = 0.24;
			break;
		};

		if (condition == InteractiveGazeCondition.RandomGaze) {
			otherTransitionProbability = 0.6;
			ambiguousTransitionProbability = 0.1;
			referenceTransitionProbability = 0.1;
			personTransitionProbability = 0.1;
			targetTransitionProbability = 0.1;
		}
	}

	//Gaze tracker messages are streaming in of the form 'Grid: XX', denoting the grid cell currently being gazed at.
	//The gaze tracker counts from 0, which must be adjusted for. 99 means gaze to the agent, and -1 means there is gaze to no grid locations.
	private int parseContent() {
		if (useOculus && oculusGazeTracker != null)
        {
            return oculusGazeTracker.getGridLocation();
        }
        else if (useHeadTracker && headGazeCtrl != null)
        {
            return headGazeCtrl.getGridLocation();
        }
        else if (gazeListener != null)
		{
			string currentContent = gazeListener.SocketContent;
			if (currentContent.Length < 7) {
				return 0;
			}
			if (currentContent[6] != '-') {
				switch(currentContent[6]) {
				case '0':
					return 1;
				case '2':
					return 3;
				case '3':
					return 4;
				case '4':
					return 5;
				case '5':
					return 6;
				case '6':
					return 7;
				case '7':
					return 8;
				case '8':
					return 9;
				case '9':
					if (currentContent.Length >= 8 && currentContent[7] == '9') {
						return 19;
					}
					else {
						return 10;
					}
				case '1':
					if (currentContent.Length >= 8) {
						switch(currentContent[7]) {
						case '0':
							return 11;
						case '1':
							return 12;
						case '2':
							return 13;
						case '3':
							return 14;
						case '4':
							return 15;
						case '5':
							return 16;
						case '6':
							return 17;
						case '7':
							return 18;
						default:
							return 2;
						}
					}
					else {
						return 2;
					}
				default:
					return 0;;
				}
			}
			else {
				return 0;
			}
		}
		else
		{
			return 0;
		}
	}

	//Given the specifically detected grid cell of gaze, add all grid cells in a square around it to 
	//make the set of possible current gaze targets. This relaxes our assumptions for using only the head vector 
	//as a proxy for gaze.
	private void setCurrentUserGaze(int gazePoint)
	{
		currentUserGaze.Clear();
		//no gaze or agent-directed gaze. just use that.
		if (gazePoint == 0 || gazePoint == 19 || (!useOculus && !useHeadTracker))
		{
			currentUserGaze.Add (gazePoint);
			return;
		}

		//the column including the gaze point
		currentUserGaze.Add (gazePoint);
		if ((gazePoint + 6) <= 18)
			currentUserGaze.Add (gazePoint + 6);
		if ((gazePoint - 6) > 0)
			currentUserGaze.Add (gazePoint - 6);

		//the column to the right of the gaze point
		if ((gazePoint % 6) != 0)
		{
			currentUserGaze.Add (gazePoint + 1);
			if ((gazePoint + 7) <= 18)
				currentUserGaze.Add (gazePoint + 7);
			if ((gazePoint - 5) > 0)
				currentUserGaze.Add (gazePoint - 5);
		}

		//the column to the left of the gaze point
		if ((gazePoint % 6) != 1)
		{
			currentUserGaze.Add (gazePoint - 1);
			if ((gazePoint + 5) <= 18)
				currentUserGaze.Add (gazePoint + 5);
			if ((gazePoint - 7) > 0)
				currentUserGaze.Add (gazePoint - 7);
		}
	}

    public List<int> GetCurrentUserGaze()
    {
        return currentUserGaze;
    }

	protected override void _Update()
	{
		if (phase == ReferencePhase.None) 
		{
			return;
		}

		//Get the current gaze target of the user
		int newGaze = parseContent();
		bool userGazeHasShifted = false;
		if (newGaze != currentUserGaze[0])
		{
			if (LogStream != null)
			{
				LogStream.WriteLine(String.Format ("{0:HH:mm:ss.ffff}\tUser Gaze Shift:\t{1}", DateTime.Now, newGaze));
			}
			userGazeHasShifted = true;
		}
		setCurrentUserGaze(newGaze);

		//Log if there is mutual gaze occurring
		if (!gazeMutual && (currentUserGaze[0] == 19) && (currentAgentGaze == 19))
		{
			if (LogStream != null)
			{
				LogStream.WriteLine(String.Format ("{0:HH:mm:ss.ffff}\tMutual Gaze Start", DateTime.Now));
			}
			gazeMutual = true;
		}

		//Log if there is otherwise shared gaze occurring (agent and user gazing to same object)
		if (!gazeShared && (currentUserGaze[0] == currentAgentGaze) && (currentUserGaze[0] != 0) && (currentUserGaze[0] != 19))
		{
			if (LogStream != null)
			{
				if (currentUserGaze[0] == referenceIndex)
				{
					LogStream.WriteLine(String.Format ("{0:HH:mm:ss.ffff}\tShared Reference Gaze Start:\t{1}", DateTime.Now, currentUserGaze[0]));
				}
				else if (currentUserGaze[0] == targetIndex)
				{
					LogStream.WriteLine(String.Format ("{0:HH:mm:ss.ffff}\tShared Target Gaze Start:\t{1}", DateTime.Now, currentUserGaze[0]));
				}
				else if (ambiguousIndices.Contains(currentUserGaze[0]))
				{
					LogStream.WriteLine(String.Format ("{0:HH:mm:ss.ffff}\tShared Ambiguous Gaze Start:\t{1}", DateTime.Now, currentUserGaze[0]));
				}
				else
				{
					LogStream.WriteLine(String.Format ("{0:HH:mm:ss.ffff}\tShared Other Gaze Start:\t{1}", DateTime.Now, currentUserGaze[0]));
				}
			}
			gazeShared = true;
		}

		if (gazeShared)
		{
			gazeSharedLength += (Time.deltaTime * 1000f);
			//Shared gaze has ended
			if ((currentUserGaze[0] != currentAgentGaze) || (currentUserGaze[0] == 0) || (currentUserGaze[0] == 19))
			{
				if (LogStream != null)
				{
					LogStream.WriteLine(String.Format ("{0:HH:mm:ss.ffff}\tShared Gaze End:\t{1}ms", DateTime.Now, gazeSharedLength));
				}
				gazeShared = false;
				gazeSharedLength = 0;
			}
		}

		if (gazeMutual)
		{
			gazeMutualLength += (Time.deltaTime * 1000f);
			//Mutual gaze has ended
			if ((currentUserGaze[0] != 19) || (currentAgentGaze != 19))
			{
				if (LogStream != null)
				{
					LogStream.WriteLine(String.Format ("{0:HH:mm:ss.ffff}\tMutual Gaze End:\t{1}ms", DateTime.Now, gazeMutualLength));
				}
				gazeMutual = false;
				gazeMutualLength = 0;
			}
		}

		if (condition == InteractiveGazeCondition.None) 
		{
			return;
		}

		//timeElapsed does not increase when a gaze shift is in progress
		if (gazeCtrl.LEye.trgReached || gazeCtrl.REye.trgReached)
		{
			timeElapsed += Time.deltaTime;
		}

		//the user has looked to the agent, and we wait to see if they quickly make the correct action
		//if not, we offer a refinement
		if (waitingForActionFlag)
		{
			waitingForActionTime += Time.deltaTime;
			if (waitingForActionTime >= waitingForActionLength)
			{
				waitingForActionFlag = false;
				waitingForActionTime = 0.0;
				offerRefinementFlag = true;
				if (LogStream != null)
				{
					LogStream.WriteLine(String.Format ("{0:HH:mm:ss.ffff}\tRefinement Due to Gaze at Agent", DateTime.Now));
				}
			}
		}

		//the user has looked at an ambiguous object, and we wait to see if they will quickly find the reference object
		//if not, we look to the user and offer a refinement
		if (waitingForReferenceGazeFlag)
		{
			if (!currentUserGaze.Contains (referenceIndex)) 
			{
				waitingForReferenceGazeTime += Time.deltaTime;
				if (waitingForReferenceGazeTime >= waitingForReferenceGazeLength)
				{
					//look at the user
					if (gazeState == InteractiveGazeState.PersonGaze)
					{
						resetTime ();
					}
					else
					{
						GoToState((int)InteractiveGazeState.PersonGaze);
					}
					//and offer a refinement
					offerRefinementFlag = true;
					waitingForReferenceGazeFlag = false;
					waitingForReferenceGazeTime = 0.0;
					if (LogStream != null)
					{
						LogStream.WriteLine(String.Format ("{0:HH:mm:ss.ffff}\tRefinement Due to Gaze at Ambiguous Object", DateTime.Now));
					}
				}
			}
			else
			{
				waitingForReferenceGazeFlag = false;
				waitingForReferenceGazeTime = 0.0;
			}
		}

		//Update the message we are receiving from the gaze tracker
		if (userGazeHasShifted && (currentUserGaze[0] != 0) && CurrentConditionHasGazeInput())
		{
			//Check for triggers in Monitor state
			if (phase == ReferencePhase.Monitor)
			{
				//User gazed to the reference object
				if (currentUserGaze.Contains(referenceIndex))
				{
					if (LogStream != null)
					{
						LogStream.WriteLine(String.Format ("{0:HH:mm:ss.ffff}\tHeuristic:\tMonitor - Reference After Reference", DateTime.Now));
					}
					//If the agent is already looking at the reference, just reset the timer so they don't immediately look away
					if (gazeState == InteractiveGazeState.ReferenceGaze)
					{
						resetTime ();
					}
					//Otherwise, look at the reference object
					else
					{
						GoToState((int)InteractiveGazeState.ReferenceGaze);
					}
					return;
				}
				//User gazed to the agent
				else if (currentUserGaze[0] == 19)
				{
					if (LogStream != null)
					{
						LogStream.WriteLine(String.Format ("{0:HH:mm:ss.ffff}\tHeuristic:\tMonitor - User After Agent", DateTime.Now));
					}
					//If the agent is already looking at the user, just reset the timer so they don't immediately look away
					if (gazeState == InteractiveGazeState.PersonGaze)
					{
						resetTime ();
					}
					//Otherwise, look at the user
					else
					{
						GoToState((int)InteractiveGazeState.PersonGaze);
					}
					waitingForActionTime = 0.0;
					waitingForActionFlag = true;
					return;
				}
				else
				{
					foreach (int aI in ambiguousIndices)
					{
						//User gazed to an ambiguous object
						if (currentUserGaze.Contains(aI))
						{
							if (LogStream != null)
							{
								LogStream.WriteLine(String.Format ("{0:HH:mm:ss.ffff}\tHeuristic:\tMonitor - Reference After Ambiguous", DateTime.Now));
							}
							//If the agent is already looking at the reference, just reset the timer so they don't immediately look away
							if (gazeState == InteractiveGazeState.ReferenceGaze)
							{
								resetTime ();
							}
							//Otherwise, look at the reference object
							else
							{
								GoToState((int)InteractiveGazeState.ReferenceGaze);
							}
							waitingForReferenceGazeTime = 0.0;
							waitingForReferenceGazeFlag = true;
							return;
						}
					}
				}
			}
			//Check for triggers in Action phase
			else if (phase == ReferencePhase.Action)
			{
				//User gazed to the target
				if (currentUserGaze.Contains(targetIndex))
				{
					if (LogStream != null)
					{
						LogStream.WriteLine(String.Format ("{0:HH:mm:ss.ffff}\tHeuristic:\tAction - Target After Target", DateTime.Now));
					}
					//If the agent is already looking at the target, just reset the timer so they don't immediately look away
					if (gazeState == InteractiveGazeState.TargetGaze)
					{
						resetTime ();
					}
					//Otherwise, look at the target
					else
					{
						GoToState((int)InteractiveGazeState.TargetGaze);
					}
					return;
				}
				//User gazed to the agent
				else if (currentUserGaze[0] == 19)
				{
					if (LogStream != null)
					{
						LogStream.WriteLine(String.Format ("{0:HH:mm:ss.ffff}\tHeuristic:\tAction - User and Target After Agent", DateTime.Now));
					}
					//If the agent is already looking at the user, just reset the timer so they don't immediately look away
					if (gazeState == InteractiveGazeState.PersonGaze)
					{
						//Ensure that the following gaze shift is to the target
						nextGazeToTarget = true;
						resetTime ();
						setTransitionProbabilities();
					}
					//Otherwise, look at the user
					else
					{
						//Ensure that the following gaze shift is to the target
						nextGazeToTarget = true;
						GoToState((int)InteractiveGazeState.PersonGaze);
					}
					return;
				}
				//User gazed to an object
				else if (ambiguousIndices.Contains(currentUserGaze[0]) || otherIndices.Contains(currentUserGaze[0])) 
				{
					if (LogStream != null)
					{
						LogStream.WriteLine(String.Format ("{0:HH:mm:ss.ffff}\tHeuristic:\tAction - Object after Object", DateTime.Now));
					}
					resetTime ();
					if (LogStream != null)
					{
						LogStream.WriteLine(String.Format ("{0:HH:mm:ss.ffff}\tAgent Gaze to Other:\t{1}", DateTime.Now, currentUserGaze[0]));
					}
					if (CurrentConditionHasGazeOutput())
					{
						currentAgentGaze = currentUserGaze[0];
						gazeCtrl.GazeAt(gridTargets[currentUserGaze[0]]);
						gazeLength = otherGazeLength.NextDouble();
					}
					return;
				}
			}
		}

		if (timeElapsed >= gazeLength)
		{
			pickNextState();
		}
	}

	//Shift gaze to a new target
	private void pickNextState() {
		double d = randNumGen.NextDouble ();

		double oTP = otherTransitionProbability;
		double aTP = ambiguousTransitionProbability;
		double rTP = referenceTransitionProbability;
		double pTP = personTransitionProbability;
		double tTP = targetTransitionProbability;

		//Zero out the probability of transitioning into the state we are already in (except for "Other")
		if (gazeState == InteractiveGazeState.AmbiguousGaze) {
			aTP = 0;
		}
		else if (gazeState == InteractiveGazeState.ReferenceGaze) {
			rTP = 0;
		}
		else if (gazeState == InteractiveGazeState.PersonGaze) {
			pTP = 0;
		}
		else if (gazeState == InteractiveGazeState.TargetGaze) {
			tTP = 0;
		}
		//renormalize
		double newTotal = oTP + aTP + rTP + pTP + tTP;
		oTP = oTP / newTotal;
		aTP = aTP / newTotal;
		rTP = rTP / newTotal;
		pTP = pTP / newTotal;
		tTP = tTP / newTotal;

		//pick the next state
		if (d < oTP) {
			GoToState((int)InteractiveGazeState.OtherGaze);
		}
		else if (d < (oTP + aTP)) {
			GoToState((int)InteractiveGazeState.AmbiguousGaze);
		}
		else if (d < (oTP + aTP + rTP)) {
			GoToState((int)InteractiveGazeState.ReferenceGaze);
		}
		else if (d < (oTP + aTP + rTP + pTP)) {
			//UnityEngine.Debug.Log ("pTP: " + pTP);
			//UnityEngine.Debug.Log ("gazeState: " + gazeState.ToString());
			GoToState((int)InteractiveGazeState.PersonGaze);
		}
		else {
			GoToState((int)InteractiveGazeState.TargetGaze);
		}
	}
	
	protected virtual void Update_AmbiguousGaze()
	{
		gazeState = InteractiveGazeState.AmbiguousGaze;
	}

	protected virtual void Update_OtherGaze()
	{
		gazeState = InteractiveGazeState.OtherGaze;
	}

	protected virtual void Update_PersonGaze()
	{
		gazeState = InteractiveGazeState.PersonGaze;
	}

	protected virtual void Update_TargetGaze()
	{
		gazeState = InteractiveGazeState.TargetGaze;
	}

	protected virtual void Update_ReferenceGaze()
	{
		gazeState = InteractiveGazeState.ReferenceGaze;
	}

	//Shift gaze to the reference object
	protected virtual void Transition_ReferenceGaze() {
		gazeState = InteractiveGazeState.ReferenceGaze;
		//Gaze at reference with full head alignment
		gazeCtrl.Head.align = 1f;

		if (CurrentConditionHasGazeOutput())
		{
			if (LogStream != null)
			{
				LogStream.WriteLine(String.Format ("{0:HH:mm:ss.ffff}\tAgent Gaze to Reference:\t{1}", DateTime.Now, referenceIndex));
			}
			currentAgentGaze = referenceIndex;
			gazeCtrl.GazeAt(gridTargets[referenceIndex]);
		}
		gazeLength = referenceGazeLength.NextDouble();
		resetTime ();
		setTransitionProbabilities();
	}

	//Shift gaze to the target
	protected virtual void Transition_TargetGaze() {
		gazeState = InteractiveGazeState.TargetGaze;
		//Gaze at target with full head alignment
		gazeCtrl.Head.align = 1f;

        if (CurrentConditionHasGazeOutput())
		{
			if (LogStream != null)
			{
				LogStream.WriteLine(String.Format ("{0:HH:mm:ss.ffff}\tAgent Gaze to Target:\t{1}", DateTime.Now, targetIndex));
			}
			currentAgentGaze = targetIndex;
			gazeCtrl.GazeAt(gridTargets[targetIndex]);
		}
		gazeLength = targetGazeLength.NextDouble();
		resetTime ();
		setTransitionProbabilities();
	}

	//Shift gaze to an ambiguous object
	protected virtual void Transition_AmbiguousGaze() {
		gazeState = InteractiveGazeState.AmbiguousGaze;
		//Minimal use of head for gazes to ambiguous objects
		gazeCtrl.Head.align = 0.4f;

		if (ambiguousIndices.Length > 0) {
			int randomInt = randNumGen.Next(0,ambiguousIndices.Length);
            if (CurrentConditionHasGazeOutput())
			{
				if (LogStream != null)
				{
					LogStream.WriteLine(String.Format ("{0:HH:mm:ss.ffff}\tAgent Gaze to Ambiguous:\t{1}", DateTime.Now, ambiguousIndices[randomInt]));
				}
				currentAgentGaze = ambiguousIndices[randomInt];
				gazeCtrl.GazeAt(gridTargets[ambiguousIndices[randomInt]]);
			}
		}
		gazeLength = ambiguousGazeLength.NextDouble();
		resetTime ();
		setTransitionProbabilities();
	}

	//Shift gaze to an 'other' object
	protected virtual void Transition_OtherGaze() {
		gazeState = InteractiveGazeState.OtherGaze;
		//Minimal use of head for gazes to random objects
		gazeCtrl.Head.align = 0.4f;

		int randomInt = randNumGen.Next(0,otherIndices.Length);
        if (CurrentConditionHasGazeOutput())
		{
			if (LogStream != null)
			{
				LogStream.WriteLine(String.Format ("{0:HH:mm:ss.ffff}\tAgent Gaze to Other:\t{1}", DateTime.Now, otherIndices[randomInt]));
			}
			currentAgentGaze = otherIndices[randomInt];
			gazeCtrl.GazeAt(gridTargets[otherIndices[randomInt]]);
		}
		gazeLength = otherGazeLength.NextDouble();
		resetTime ();
		setTransitionProbabilities();
	}

	//Shift gaze to the user
	protected virtual void Transition_PersonGaze() {
		gazeState = InteractiveGazeState.PersonGaze;
		//Look at the interlocutor with full head alignment
		gazeCtrl.Head.align = 1f;

        if (CurrentConditionHasGazeOutput())
		{
			if (LogStream != null)
			{
				LogStream.WriteLine(String.Format ("{0:HH:mm:ss.ffff}\tAgent Gaze to User", DateTime.Now));
			}
			currentAgentGaze = 19;
			gazeCtrl.GazeAt(mutualGazeObject);
		}
		gazeLength = personGazeLength.NextDouble();
		resetTime ();
		setTransitionProbabilities();
	}
	
	public override void UEdCreateStates()
	{
		// Initialize states
		_InitStateDefs<InteractiveGazeState>();
		_InitStateTransDefs( (int)InteractiveGazeState.AmbiguousGaze, 4 );
		_InitStateTransDefs( (int)InteractiveGazeState.OtherGaze, 5 );
		_InitStateTransDefs( (int)InteractiveGazeState.PersonGaze, 4 );
		_InitStateTransDefs( (int)InteractiveGazeState.ReferenceGaze, 4 );
		_InitStateTransDefs( (int)InteractiveGazeState.TargetGaze, 4 );
		//init update functions (same for every state)
		states[(int)InteractiveGazeState.AmbiguousGaze].updateHandler = "Update_AmbiguousGaze";
		states[(int)InteractiveGazeState.OtherGaze].updateHandler = "Update_OtherGaze";
		states[(int)InteractiveGazeState.PersonGaze].updateHandler = "Update_PersonGaze";
		states[(int)InteractiveGazeState.ReferenceGaze].updateHandler = "Update_ReferenceGaze";
		states[(int)InteractiveGazeState.TargetGaze].updateHandler = "Update_TargetGaze";
		//init transitions (fully connected graph)
		states[(int)InteractiveGazeState.AmbiguousGaze].nextStates[0].nextState = "OtherGaze";
		states[(int)InteractiveGazeState.AmbiguousGaze].nextStates[0].transitionHandler = "Transition_OtherGaze";
		states[(int)InteractiveGazeState.AmbiguousGaze].nextStates[1].nextState = "PersonGaze";
		states[(int)InteractiveGazeState.AmbiguousGaze].nextStates[1].transitionHandler = "Transition_PersonGaze";
		states[(int)InteractiveGazeState.AmbiguousGaze].nextStates[2].nextState = "ReferenceGaze";
		states[(int)InteractiveGazeState.AmbiguousGaze].nextStates[2].transitionHandler = "Transition_ReferenceGaze";
		states[(int)InteractiveGazeState.AmbiguousGaze].nextStates[3].nextState = "TargetGaze";
		states[(int)InteractiveGazeState.AmbiguousGaze].nextStates[3].transitionHandler = "Transition_TargetGaze";

		states[(int)InteractiveGazeState.PersonGaze].nextStates[0].nextState = "OtherGaze";
		states[(int)InteractiveGazeState.PersonGaze].nextStates[0].transitionHandler = "Transition_OtherGaze";
		states[(int)InteractiveGazeState.PersonGaze].nextStates[1].nextState = "AmbiguousGaze";
		states[(int)InteractiveGazeState.PersonGaze].nextStates[1].transitionHandler = "Transition_AmbiguousGaze";
		states[(int)InteractiveGazeState.PersonGaze].nextStates[2].nextState = "ReferenceGaze";
		states[(int)InteractiveGazeState.PersonGaze].nextStates[2].transitionHandler = "Transition_ReferenceGaze";
		states[(int)InteractiveGazeState.PersonGaze].nextStates[3].nextState = "TargetGaze";
		states[(int)InteractiveGazeState.PersonGaze].nextStates[3].transitionHandler = "Transition_TargetGaze";

		states[(int)InteractiveGazeState.ReferenceGaze].nextStates[0].nextState = "OtherGaze";
		states[(int)InteractiveGazeState.ReferenceGaze].nextStates[0].transitionHandler = "Transition_OtherGaze";
		states[(int)InteractiveGazeState.ReferenceGaze].nextStates[1].nextState = "PersonGaze";
		states[(int)InteractiveGazeState.ReferenceGaze].nextStates[1].transitionHandler = "Transition_PersonGaze";
		states[(int)InteractiveGazeState.ReferenceGaze].nextStates[2].nextState = "AmbiguousGaze";
		states[(int)InteractiveGazeState.ReferenceGaze].nextStates[2].transitionHandler = "Transition_AmbiguousGaze";
		states[(int)InteractiveGazeState.ReferenceGaze].nextStates[3].nextState = "TargetGaze";
		states[(int)InteractiveGazeState.ReferenceGaze].nextStates[3].transitionHandler = "Transition_TargetGaze";

		states[(int)InteractiveGazeState.TargetGaze].nextStates[0].nextState = "OtherGaze";
		states[(int)InteractiveGazeState.TargetGaze].nextStates[0].transitionHandler = "Transition_OtherGaze";
		states[(int)InteractiveGazeState.TargetGaze].nextStates[1].nextState = "PersonGaze";
		states[(int)InteractiveGazeState.TargetGaze].nextStates[1].transitionHandler = "Transition_PersonGaze";
		states[(int)InteractiveGazeState.TargetGaze].nextStates[2].nextState = "ReferenceGaze";
		states[(int)InteractiveGazeState.TargetGaze].nextStates[2].transitionHandler = "Transition_ReferenceGaze";
		states[(int)InteractiveGazeState.TargetGaze].nextStates[3].nextState = "AmbiguousGaze";
		states[(int)InteractiveGazeState.TargetGaze].nextStates[3].transitionHandler = "Transition_AmbiguousGaze";

		states[(int)InteractiveGazeState.OtherGaze].nextStates[0].nextState = "AmbiguousGaze";
		states[(int)InteractiveGazeState.OtherGaze].nextStates[0].transitionHandler = "Transition_AmbiguousGaze";
		states[(int)InteractiveGazeState.OtherGaze].nextStates[1].nextState = "PersonGaze";
		states[(int)InteractiveGazeState.OtherGaze].nextStates[1].transitionHandler = "Transition_PersonGaze";
		states[(int)InteractiveGazeState.OtherGaze].nextStates[2].nextState = "ReferenceGaze";
		states[(int)InteractiveGazeState.OtherGaze].nextStates[2].transitionHandler = "Transition_ReferenceGaze";
		states[(int)InteractiveGazeState.OtherGaze].nextStates[3].nextState = "TargetGaze";
		states[(int)InteractiveGazeState.OtherGaze].nextStates[3].transitionHandler = "Transition_TargetGaze";
		states[(int)InteractiveGazeState.OtherGaze].nextStates[4].nextState = "OtherGaze";
		states[(int)InteractiveGazeState.OtherGaze].nextStates[4].transitionHandler = "Transition_OtherGaze";
	}
};