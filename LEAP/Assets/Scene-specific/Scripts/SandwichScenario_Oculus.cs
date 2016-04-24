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

public class SandwichScenario_Oculus : Scenario
{
	private string agentName = "Jason";
	
	//the relevant controllers
	private AsynchronousSocketListener listenSocket;
	private InteractiveGazeController intGazeCtrl;
	
	//Random distributions for smiling and nodding while listening
	private MersenneTwisterRandomSource randNumGen = null; //random number generator for seeding the distributions below
	private NormalDistribution nextSmileDistribution = null;
	private NormalDistribution smileLengthDistribution = null;

	//Random smiling
	private double nextSmileCounter = 0;
	private double nextSmileTime = 0;

	private bool actionPerformed = false;
	private int currentReferenceNumber = 0;
	private string sandwichBaseString = "";
	private List<Sandwich> sandwiches = null;
	private int currentSandwichIndex = 0;
    private bool startScenario = false;

	private List<int> visibleGridCells = new List<int>();
	private int refinementBeginningIndex = 1;
	private int wrongBeginningIndex = 1;

	private bool sandwichInProgress = false;
	private float timeSinceLastRefinement = 0f;
	private float refinementRefreshPeriod = 3f;

    private bool agentSpeechInProgress = false;

    private GameObject[] gridTargets = null;
    List<Vector3> ingredientStartingPositions;

    //animating the object action
    private GameObject movedObject;
    private float totalMotionTime = 1.5f;
    private float animationTimer = 0f;
    private Vector3 actionTargetPosition;
    private Vector3 actionSourcePosition;
    private bool doActionAnimation = false;
    private float arcParam_a = 0f;
    private float arcParam_b = 0f;

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
        gridTargets = GameObject.FindGameObjectsWithTag("GazeTarget");
        Array.Sort(gridTargets, delegate(GameObject g1, GameObject g2)
        {
            return g1.name.CompareTo(g2.name);
        });
	}
	
	/// <see cref="Scenario._Run()"/>
	protected override IEnumerator _Run()
	{
		//initialize sandwich ingredients
		//bacon special
		Sandwich baconSpecial = new Sandwich("1_BS", "Bacon Special");
		baconSpecial.layout = new SandwichIngredient[19]{SandwichIngredient.Empty, SandwichIngredient.CheddarCheese, SandwichIngredient.LightLettuce, SandwichIngredient.Mayonnaise,
			SandwichIngredient.Cucumber1, SandwichIngredient.Bacon1, SandwichIngredient.Jelly, SandwichIngredient.Bacon2, SandwichIngredient.Ketchup,
			SandwichIngredient.RoastBeef, SandwichIngredient.Mustard, SandwichIngredient.Egg, SandwichIngredient.Empty, SandwichIngredient.Cucumber2, SandwichIngredient.Turkey,
			SandwichIngredient.PeanutButter, SandwichIngredient.SwissCheese, SandwichIngredient.DarkLettuce, SandwichIngredient.Pickle1};
		baconSpecial.ingredients = new SandwichIngredient[12]{SandwichIngredient.Bacon1, SandwichIngredient.Mustard, SandwichIngredient.Egg,
			SandwichIngredient.LightLettuce, SandwichIngredient.Mayonnaise, SandwichIngredient.SwissCheese, SandwichIngredient.Pickle1, 
			SandwichIngredient.Ketchup, SandwichIngredient.Cucumber1, SandwichIngredient.Bacon2, SandwichIngredient.Cucumber2, SandwichIngredient.CheddarCheese};

		//turkey special
		Sandwich turkeySpecial = new Sandwich("2_TS", "Turkey Special");
		turkeySpecial.layout = new SandwichIngredient[19]{SandwichIngredient.Empty, SandwichIngredient.Tomato1, SandwichIngredient.Balogna, SandwichIngredient.Pickle1,
			SandwichIngredient.DarkLettuce, SandwichIngredient.CheddarCheese, SandwichIngredient.PeanutButter, SandwichIngredient.Mayonnaise,
			SandwichIngredient.SwissCheese, SandwichIngredient.Mustard, SandwichIngredient.Ketchup, SandwichIngredient.Tomato2, SandwichIngredient.Empty,
			SandwichIngredient.LightLettuce, SandwichIngredient.Turkey, SandwichIngredient.Ham, SandwichIngredient.Bacon1, SandwichIngredient.Jelly,
			SandwichIngredient.Pickle2};
		turkeySpecial.ingredients = new SandwichIngredient[12]{SandwichIngredient.LightLettuce, SandwichIngredient.Mustard, SandwichIngredient.Turkey,
			SandwichIngredient.Bacon1, SandwichIngredient.CheddarCheese, SandwichIngredient.Jelly, SandwichIngredient.Pickle1, SandwichIngredient.SwissCheese,
			SandwichIngredient.Tomato1, SandwichIngredient.Mayonnaise, SandwichIngredient.Pickle2, SandwichIngredient.Tomato2};

		//ham special
		Sandwich hamSpecial = new Sandwich("3_HS", "Ham Special");
		hamSpecial.layout = new SandwichIngredient[19]{SandwichIngredient.Empty, SandwichIngredient.RoastBeef, SandwichIngredient.DarkLettuce, SandwichIngredient.Mayonnaise,
			SandwichIngredient.PeanutButter, SandwichIngredient.CheddarCheese, SandwichIngredient.Cucumber1, SandwichIngredient.ProvoloneCheese, 
			SandwichIngredient.Bacon1, SandwichIngredient.Cucumber2, SandwichIngredient.Balogna, SandwichIngredient.Bacon2, SandwichIngredient.Empty,
			SandwichIngredient.Ham, SandwichIngredient.Ketchup, SandwichIngredient.Tomato1, SandwichIngredient.Mustard, SandwichIngredient.LightLettuce,
			SandwichIngredient.Salami};
		hamSpecial.ingredients = new SandwichIngredient[12]{SandwichIngredient.Ham, SandwichIngredient.Mustard, SandwichIngredient.Cucumber1,
			SandwichIngredient.Mayonnaise, SandwichIngredient.ProvoloneCheese, SandwichIngredient.Cucumber2, SandwichIngredient.Salami, 
			SandwichIngredient.Bacon1, SandwichIngredient.CheddarCheese, SandwichIngredient.DarkLettuce, SandwichIngredient.Bacon2, SandwichIngredient.LightLettuce};

		//roast beef special
		Sandwich roastBeefSpecial = new Sandwich("4_RBS", "Roast Beef Special");
		roastBeefSpecial.layout = new SandwichIngredient[19]{SandwichIngredient.Empty, SandwichIngredient.LightLettuce, SandwichIngredient.Tomato1, SandwichIngredient.CheddarCheese,
			SandwichIngredient.Cucumber2, SandwichIngredient.Pickle2, SandwichIngredient.RoastBeef, SandwichIngredient.PeanutButter, SandwichIngredient.Pickle1,
			SandwichIngredient.Egg, SandwichIngredient.SwissCheese, SandwichIngredient.Tomato2, SandwichIngredient.Empty, SandwichIngredient.Balogna, SandwichIngredient.Salami,
			SandwichIngredient.Ketchup, SandwichIngredient.DarkLettuce, SandwichIngredient.Mayonnaise, SandwichIngredient.Cucumber1};
		roastBeefSpecial.ingredients = new SandwichIngredient[12]{SandwichIngredient.Balogna, SandwichIngredient.Egg, SandwichIngredient.Tomato2,
			SandwichIngredient.Ketchup, SandwichIngredient.DarkLettuce, SandwichIngredient.Tomato1, SandwichIngredient.Pickle1, SandwichIngredient.Mayonnaise,
			SandwichIngredient.RoastBeef, SandwichIngredient.Cucumber1, SandwichIngredient.LightLettuce, SandwichIngredient.Pickle2};

		sandwiches = new List<Sandwich>();

        sandwiches.Add(baconSpecial);
		sandwiches.Add(turkeySpecial);
		sandwiches.Add(hamSpecial);
        sandwiches.Add(roastBeefSpecial);

        ingredientStartingPositions = new List<Vector3>();
        for (int i = 0; i < GameObject.Find("SandwichIngredients").transform.childCount; i++ )
        {
            ingredientStartingPositions.Add(GameObject.Find("SandwichIngredients").transform.GetChild(i).position);
        }

        //Find the relevant controllers
        intGazeCtrl = agents[agentName].GetComponent<InteractiveGazeController>();
		AsynchronousSocketListener[] allSocketListeners = agents[agentName].GetComponents<AsynchronousSocketListener>();
		foreach (AsynchronousSocketListener asl in allSocketListeners) {
			if (asl.ID == SocketSource.SpeechRecognition) {
				listenSocket = asl;
			}
		}

		//set the scene lights
		GameObject[] lights = GameObject.FindGameObjectsWithTag("Spotlight");
		float[] lightIntensities = new float[lights.Length];
		for (int i = 0; i < lights.Length; ++i) {
			lightIntensities[i] = lights[i].light.intensity;
			lights[i].light.intensity = 0f;	
		}
		
		// Initialize gaze
        int curgaze = -1;
        curgaze = GazeAt(agentName, intGazeCtrl.mutualGazeObject, 0.8f, 0f);
        yield return StartCoroutine( WaitUntilFinished(curgaze) );
        curgaze = GazeAt(agentName, intGazeCtrl.mutualGazeObject, 0.8f, 0f);
        yield return StartCoroutine( WaitUntilFinished(curgaze) );
        curgaze = GazeAt(agentName, intGazeCtrl.mutualGazeObject, 1.0f, 0f);
        yield return StartCoroutine( WaitUntilFinished(curgaze) );

		//Iterate through each of the conditions
		currentSandwichIndex = 0;
		foreach(Sandwich s in sandwiches)
		{
            for (int i = 0; i < GameObject.Find("SandwichIngredients").transform.childCount; i++)
            {
                GameObject.Find("SandwichIngredients").transform.GetChild(i).position = ingredientStartingPositions[i];
            }

            for (int i = 1; i < s.layout.Length; i++ )
            {
                string ingredientName = s.layout[i] == SandwichIngredient.Empty ? "Bread" : s.layout[i].ToString();
                GameObject.Find(ingredientName).transform.position = gridTargets[i - 1].transform.position;
            }

            startScenario = false;
            while (!startScenario)
            {
                yield return 0;
            }

            GameObject.Find("FinalInstructions").guiText.enabled = false;

            //bring the lights back up
            for (int i = 0; i < lights.Length; ++i) {
                lights[i].light.intensity = lightIntensities[i];
            }

			yield return new WaitForSeconds(1f);
			//yield return StartCoroutine(SpeakAndWait("intro"));
			//yield return new WaitForSeconds(0.5f);

			intGazeCtrl.changePhase(ReferencePhase.None);
			actionPerformed = false;

			sandwichBaseString = s.name;

			//give the sandwich intro
			yield return StartCoroutine(SpeakAndWait(sandwichBaseString + "_intro"));
			//iterate through the 10 reference-action sequences for each sandwich
			sandwichInProgress = true;
			for (int i = 0; i < s.ingredients.Length; ++i)
			{
				currentReferenceNumber = i+1;
				//set the grid indices for the reference, ambiguous, and other objects
				intGazeCtrl.setReferenceObject(s.getReferenceObjectCell(i), s.getAmbiguousObjectCells(i), s.getOtherObjectCells(i));
				actionPerformed = false;
				//PRE-REFERENCE
				intGazeCtrl.changePhase(ReferencePhase.PreReference);
				yield return new WaitForSeconds(1f);
				intGazeCtrl.triggerReferenceGaze(); //ensures a reference gaze shift leading up to the reference sequence
				yield return StartCoroutine(SpeakAndWait(sandwichBaseString + "_PreReference_" + currentReferenceNumber));

				//REFERENCE
				intGazeCtrl.changePhase(ReferencePhase.Reference);
				yield return StartCoroutine(SpeakAndWait(sandwichBaseString + "_Reference_" + currentReferenceNumber));
                timeSinceLastRefinement = 1f;
				yield return new WaitForSeconds(0.5f);

				//MONITOR
				intGazeCtrl.changePhase(ReferencePhase.Monitor);
				while (!actionPerformed) {
					yield return 0;
				}

				//ACTION
				intGazeCtrl.changePhase(ReferencePhase.Action);
				yield return new WaitForSeconds(2f);
				s.removeReferenceObjectFromLayout(i);
			}
			sandwichInProgress = false;
			
			yield return StartCoroutine(SpeakAndWait(sandwichBaseString + "_outro"));
			
			//turn off the lights, reinitialize gaze, and wait for starting the next sandwich
			intGazeCtrl.changePhase(ReferencePhase.None);
			curgaze = GazeAt(agentName, intGazeCtrl.mutualGazeObject, 0.8f, 0f);
			yield return StartCoroutine( WaitUntilFinished(curgaze) );
			curgaze = GazeAt(agentName, intGazeCtrl.mutualGazeObject, 0.8f, 0f);
			yield return StartCoroutine( WaitUntilFinished(curgaze) );
			curgaze = GazeAt(agentName, intGazeCtrl.mutualGazeObject, 1.0f, 0f);
			yield return StartCoroutine( WaitUntilFinished(curgaze) );
			for (int i = 0; i < lights.Length; ++i) {
				lights[i].light.intensity = 0f;	
			}

			GameObject.Find ("FinalInstructions").guiText.enabled = true;
			
			currentSandwichIndex++;
		}

		//The outro
		//yield return StartCoroutine(SpeakAndWait("outro"));
		//yield return new WaitForSeconds(1f);
		foreach (GameObject g in lights) {
			g.light.intensity = 0f;	
		}
	}
	
	void Update() {
		
        if (Input.GetButton("Fire1"))
        {
            OVRManager.display.RecenterPose();
            startScenario = true;
        }
        
        //Periodic smiles and eyebrow raises to increase lifelikeness
		nextSmileCounter += Time.deltaTime;
		if (nextSmileCounter >= nextSmileTime) {
			nextSmileCounter = 0;
			nextSmileTime = nextSmileDistribution.NextDouble();
			StartCoroutine(agentSmileAndEyebrow(smileLengthDistribution.NextDouble()));
		}

        string speechContent = listenSocket.SocketContent;
        listenSocket.SocketContent = "nothing";

		if (sandwichInProgress)
		{
			timeSinceLastRefinement += Time.deltaTime;

            if (Input.GetButtonDown("Jump"))
            {
                List<int> currentUserGaze = intGazeCtrl.GetCurrentUserGaze();
                if (currentUserGaze.Count > 0)
                {
                    List<int> possibleGazeTargets = intGazeCtrl.GetCurrentUserGaze();
                    if (possibleGazeTargets.Count > 0)
                    {
                        if (possibleGazeTargets.Contains(intGazeCtrl.getReferenceIndex()))
                        {
                            visibleGridCells.Add(intGazeCtrl.getReferenceIndex());
                            movedObject = GameObject.Find(sandwiches[currentSandwichIndex].layout[intGazeCtrl.getReferenceIndex()].ToString());
                            actionSourcePosition = movedObject.transform.position;
                            actionTargetPosition = gridTargets[11].transform.position;
                            actionTargetPosition.y += (currentReferenceNumber * 0.005f);
                            arcParam_a = (actionSourcePosition.y - actionTargetPosition.y - 1) / 2f;
                            arcParam_b = actionSourcePosition.y + (arcParam_a * arcParam_a);
                            doActionAnimation = true;
                            animationTimer = 0f;
                            actionPerformed = true;
                        }
                        else
                        {
                            visibleGridCells.Add(possibleGazeTargets[0]);
                            StartCoroutine(refinementSequence());
                        }
                    }
                }
            }
			
			//The user has asked for clarification during the monitor phase. Offer a refinement.
			if (speechContent.Contains("clarify"))
			{
                if (!agentSpeechInProgress)
                {
                    if ((intGazeCtrl.phase == ReferencePhase.Monitor) && (timeSinceLastRefinement > refinementRefreshPeriod))
                    {
                        StartCoroutine(refinementSequence());
                        return;
                    }
                }
			}
			
			//The interactive gaze controller has flagged the need for a refinement during the monitor phase
			if (intGazeCtrl.isOfferRefinementFlagSet() && intGazeCtrl.phase == ReferencePhase.Monitor && timeSinceLastRefinement > refinementRefreshPeriod)
			{
				StartCoroutine(refinementSequence());
				return;
			}

            if (doActionAnimation)
            {
                animationTimer += Time.deltaTime;
                
                if (animationTimer >= totalMotionTime)
                {
                    doActionAnimation = false;
                    animationTimer = totalMotionTime;
                }

                float new_x = actionSourcePosition.x + (animationTimer / totalMotionTime) * (actionTargetPosition.x - actionSourcePosition.x);
                float new_y = -(((animationTimer / totalMotionTime) + arcParam_a) * ((animationTimer / totalMotionTime) + arcParam_a)) + arcParam_b;
                float new_z = actionSourcePosition.z + (animationTimer / totalMotionTime) * (actionTargetPosition.z - actionSourcePosition.z);
                movedObject.transform.position = new Vector3(new_x, new_y, new_z);
            }
		}
	}

	// Speak the utterance and wait for it to be finished. Also writes begin and end times to log file.
	IEnumerator SpeakAndWait(string SpeechFile)
	{
        agentSpeechInProgress = true;
		int curspeak = Speak (agentName, SpeechFile);
		yield return StartCoroutine(WaitUntilFinished(curspeak));
        agentSpeechInProgress = false;
	}

	//Parsing function for the messages coming from the action socket.
	//Messages are in the form of "visible:0:1:2:4:5:6"
	//which would mean that grid cells 0, 1, 2, 4, 5, and 6 are currently visible
	/*private void parseActionSocket() {
		//UnityEngine.Debug.Log (actionSocket.SocketContent);
		visibleGridCells.Clear ();
		string[] newVisibleGridCellsArray = actionSocket.SocketContent.Split(':');
		foreach (string s in newVisibleGridCellsArray)
		{
			if ((s != null) && (s.Length > 0) && (s != "visible") && (s != "\n") && (s != "nothing"))
			{
				visibleGridCells.Add(Convert.ToInt32(s)+1);
			}
		}
	}*/

	//Offering a refinement
	IEnumerator refinementSequence()
	{
		timeSinceLastRefinement = 0f;
		intGazeCtrl.changePhase(ReferencePhase.Refinement);
		yield return StartCoroutine(SpeakAndWait("refine" + refinementBeginningIndex));
		//alternated the way in which refinement is offered
		if (refinementBeginningIndex == 1)
			refinementBeginningIndex = 2;
		else
			refinementBeginningIndex = 1;
		yield return StartCoroutine(SpeakAndWait(sandwichBaseString + "_Refinement_" + currentReferenceNumber));
		yield return new WaitForSeconds(0.5f);
		//go back to monitoring
		intGazeCtrl.changePhase(ReferencePhase.Monitor);
		timeSinceLastRefinement = 0f;
	}

	//Correcting the user when they make an incorrect action
	IEnumerator wrongActionSequence()
	{
        timeSinceLastRefinement = 0f;
		intGazeCtrl.changePhase(ReferencePhase.Refinement);
		yield return StartCoroutine(SpeakAndWait("sorry" + wrongBeginningIndex));

		//alternate the way this correction is made
		if (wrongBeginningIndex == 1)
			wrongBeginningIndex = 2;
		else
			wrongBeginningIndex = 1;

		yield return StartCoroutine(SpeakAndWait(sandwichBaseString + "_Refinement_" + currentReferenceNumber));

        timeSinceLastRefinement = 0f;
		yield return new WaitForSeconds(0.5f);
		intGazeCtrl.changePhase(ReferencePhase.Monitor);
        timeSinceLastRefinement = 0f;
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