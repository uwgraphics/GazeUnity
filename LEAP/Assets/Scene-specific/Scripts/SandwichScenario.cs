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

public enum SandwichIngredient
{
	Bacon1,
	Bacon2,
	Balogna,
	CheddarCheese,
	ProvoloneCheese,
	SwissCheese,
	Cucumber1,
	Cucumber2,
	Egg,
	Ham,
	Jelly,
	Ketchup,
	LightLettuce,
	DarkLettuce,
	Mayonnaise,
	Mustard,
	PeanutButter,
	Pickle1,
	Pickle2,
	RoastBeef,
	Salami,
	Tomato1,
	Tomato2,
	Turkey,
	Empty
};

public class Sandwich
{
	public SandwichIngredient[] layout = null;
	public SandwichIngredient[] ingredients = null;
	public string name = null;
	public string longName = null;
	private Dictionary<SandwichIngredient, SandwichIngredient[]> ambiguousIngredients;
	
	public Sandwich(string n, string ln)
	{
		name = n;
		longName = ln;
		
		//Set up the mapping from each ingredient to which ingredients are potentially ambiguous with it
		ambiguousIngredients = new Dictionary<SandwichIngredient, SandwichIngredient[]>();
		ambiguousIngredients.Add (SandwichIngredient.Bacon1, new SandwichIngredient[1]{SandwichIngredient.Bacon2});
		ambiguousIngredients.Add (SandwichIngredient.Bacon2, new SandwichIngredient[1]{SandwichIngredient.Bacon1});
		ambiguousIngredients.Add (SandwichIngredient.Balogna, new SandwichIngredient[3]{SandwichIngredient.Ham, SandwichIngredient.Salami, SandwichIngredient.RoastBeef});
		ambiguousIngredients.Add (SandwichIngredient.CheddarCheese, new SandwichIngredient[2]{SandwichIngredient.ProvoloneCheese, 
			SandwichIngredient.SwissCheese});
		ambiguousIngredients.Add (SandwichIngredient.ProvoloneCheese, new SandwichIngredient[3]{SandwichIngredient.CheddarCheese, SandwichIngredient.SwissCheese,
			SandwichIngredient.Turkey});
		ambiguousIngredients.Add (SandwichIngredient.SwissCheese, new SandwichIngredient[2]{SandwichIngredient.CheddarCheese, SandwichIngredient.ProvoloneCheese});
		ambiguousIngredients.Add (SandwichIngredient.Cucumber1, new SandwichIngredient[3]{SandwichIngredient.Cucumber2, SandwichIngredient.Pickle1,
			SandwichIngredient.Pickle2});
		ambiguousIngredients.Add (SandwichIngredient.Cucumber2, new SandwichIngredient[3]{SandwichIngredient.Cucumber1, SandwichIngredient.Pickle1,
			SandwichIngredient.Pickle2});
		ambiguousIngredients.Add (SandwichIngredient.Egg, new SandwichIngredient[0]{});
		ambiguousIngredients.Add (SandwichIngredient.Ham, new SandwichIngredient[3]{SandwichIngredient.Balogna, SandwichIngredient.Turkey, SandwichIngredient.RoastBeef});
		ambiguousIngredients.Add (SandwichIngredient.Jelly, new SandwichIngredient[0]{});
		ambiguousIngredients.Add (SandwichIngredient.Ketchup, new SandwichIngredient[0]{});
		ambiguousIngredients.Add (SandwichIngredient.LightLettuce, new SandwichIngredient[1]{SandwichIngredient.DarkLettuce});
		ambiguousIngredients.Add (SandwichIngredient.DarkLettuce, new SandwichIngredient[1]{SandwichIngredient.LightLettuce});
		ambiguousIngredients.Add (SandwichIngredient.Mayonnaise, new SandwichIngredient[2]{SandwichIngredient.ProvoloneCheese, SandwichIngredient.Turkey});
		ambiguousIngredients.Add (SandwichIngredient.Mustard, new SandwichIngredient[0]{});
		ambiguousIngredients.Add (SandwichIngredient.PeanutButter, new SandwichIngredient[2]{SandwichIngredient.RoastBeef, SandwichIngredient.Salami});
		ambiguousIngredients.Add (SandwichIngredient.Pickle1, new SandwichIngredient[3]{SandwichIngredient.Pickle2, SandwichIngredient.Cucumber1,
			SandwichIngredient.Cucumber2});
		ambiguousIngredients.Add (SandwichIngredient.Pickle2, new SandwichIngredient[3]{SandwichIngredient.Pickle1, SandwichIngredient.Cucumber1,
			SandwichIngredient.Cucumber2});
		ambiguousIngredients.Add (SandwichIngredient.RoastBeef, new SandwichIngredient[3]{SandwichIngredient.PeanutButter, SandwichIngredient.Salami, SandwichIngredient.Ham});
		ambiguousIngredients.Add (SandwichIngredient.Salami, new SandwichIngredient[3]{SandwichIngredient.RoastBeef, SandwichIngredient.Ham, SandwichIngredient.PeanutButter});
		ambiguousIngredients.Add (SandwichIngredient.Tomato1, new SandwichIngredient[1]{SandwichIngredient.Tomato2});
		ambiguousIngredients.Add (SandwichIngredient.Tomato2, new SandwichIngredient[1]{SandwichIngredient.Tomato1});
		ambiguousIngredients.Add (SandwichIngredient.Turkey, new SandwichIngredient[3]{SandwichIngredient.ProvoloneCheese, SandwichIngredient.RoastBeef, SandwichIngredient.Ham});
	}
	
	public int getReferenceObjectCell(int referenceIndex)
	{
		if ((ingredients != null) && (layout != null) && (ingredients.Length > referenceIndex))
		{
			SandwichIngredient ingredient = ingredients[referenceIndex];
			if (ingredient == SandwichIngredient.Empty)
			{
				return 0;
			}
			for (int i = 0; i < layout.Length; i++)
			{
				if (layout[i] == ingredient)
				{
					return i;
				}
			}
		}
		return 0;
	}

	public int[] getAmbiguousObjectCells(int referenceIndex)
	{
		if ((ingredients == null) || (layout == null) || (ingredients.Length <= referenceIndex))
		{
			return new int[0]{};
		}
		
		SandwichIngredient ingredient = ingredients[referenceIndex];
		List<int> ambiguousList = new List<int>();
		foreach (SandwichIngredient amb in ambiguousIngredients[ingredient])
		{
			for (int i = 0; i < layout.Length; i++)
			{
				if (layout[i] == amb)
				{
					ambiguousList.Add (i);
					break;
				}
			}
		}
		return ambiguousList.ToArray();
	}
	
	public int[] getOtherObjectCells(int referenceIndex)
	{
		if ((ingredients == null) || (layout == null) || (ingredients.Length <= referenceIndex))
		{
			return new int[0]{};
		}
		
		SandwichIngredient ingredient = ingredients[referenceIndex];
		List<int> otherList = new List<int>();
		for (int i = 0; i < layout.Length; ++i)
		{
			if ((layout[i] != SandwichIngredient.Empty) && (layout[i] != ingredient))
			{
				otherList.Add (i);
			}
		}
		return otherList.ToArray();
	}
	
	public void removeReferenceObjectFromLayout(int referenceIndex)
	{
		if ((ingredients == null) || (layout == null) || (ingredients.Length <= referenceIndex))
		{
			return;
		}
		
		SandwichIngredient ingredient = ingredients[referenceIndex];
		for (int i = 0; i < layout.Length; ++i)
		{
			if (layout[i] == ingredient)
			{
				layout[i] = SandwichIngredient.Empty;
				return;
			}
		}
	}
}

public class SandwichScenario : Scenario
{
	private string agentName = "Jason";
	
	//the relevant controllers
	private AsynchronousSocketListener listenSocket;
	private AsynchronousSocketListener actionSocket;
	private InteractiveGazeController intGazeCtrl;
	
	//Random distributions for smiling and nodding while listening
	private MersenneTwisterRandomSource randNumGen = null; //random number generator for seeding the distributions below
	private NormalDistribution nextSmileDistribution = null;
	private NormalDistribution smileLengthDistribution = null;

	//Random smiling
	private double nextSmileCounter = 0;
	private double nextSmileTime = 0;

	//public bool captureVideo = false;
	//VideoCapture vidcap;
	private StreamWriter LogStream = null;
	private DateTime ReferenceToActionStartTime = DateTime.MinValue;

	private bool actionPerformed = false;
	private int currentReferenceNumber = 0;
	private string sandwichBaseString = "";
	private string nextSandwichLongName = "";
	private List<Sandwich> sandwiches = null;
	private List<InteractiveGazeCondition> conditions = null;
	private int currentSandwichIndex = 0;

	private List<int> visibleGridCells = new List<int>();
	private int refinementBeginningIndex = 1;
	private int wrongBeginningIndex = 1;
	private bool startScenario = false;

	private List<int> completedObjects = new List<int>();
	private List<int> wrongActionCells = new List<int>();

	private bool sandwichInProgress = false;
	private float timeSinceLastRefinement = 0f;
	private float refinementRefreshPeriod = 3f;

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

		/*Sandwich salamiSpecial = new Sandwich("5_SS", "Salami Special");
		salamiSpecial.layout = new SandwichIngredient[19]{SandwichIngredient.Empty, SandwichIngredient.Bacon1, SandwichIngredient.Pickle1, SandwichIngredient.ProvoloneCheese,
			SandwichIngredient.Mustard, SandwichIngredient.PeanutButter, SandwichIngredient.Cucumber1, SandwichIngredient.Cucumber2, SandwichIngredient.Ketchup, SandwichIngredient.Salami,
			SandwichIngredient.Jelly, SandwichIngredient.Bacon2, SandwichIngredient.Empty, SandwichIngredient.SwissCheese, SandwichIngredient.Mayonnaise, SandwichIngredient.Turkey,
			SandwichIngredient.Balogna, SandwichIngredient.Pickle2, SandwichIngredient.RoastBeef};
		salamiSpecial.ingredients = new SandwichIngredient[10]{SandwichIngredient.SwissCheese, SandwichIngredient.Salami, SandwichIngredient.Pickle1, SandwichIngredient.Pickle2,
			SandwichIngredient.Mayonnaise, SandwichIngredient.Bacon1, SandwichIngredient.Ketchup, SandwichIngredient.Cucumber1, SandwichIngredient.Mustard, 
			SandwichIngredient.ProvoloneCheese};

		Sandwich peanutButterSpecial = new Sandwich("6_PBS", "Peanut Butter Special");
		peanutButterSpecial.layout = new SandwichIngredient[19]{SandwichIngredient.Empty, SandwichIngredient.Cucumber1, SandwichIngredient.CheddarCheese, SandwichIngredient.Pickle1,
			SandwichIngredient.Bacon1, SandwichIngredient.SwissCheese, SandwichIngredient.Tomato1, SandwichIngredient.Bacon2, SandwichIngredient.Salami, SandwichIngredient.Jelly,
			SandwichIngredient.PeanutButter, SandwichIngredient.Pickle2, SandwichIngredient.Empty, SandwichIngredient.RoastBeef, SandwichIngredient.ProvoloneCheese, 
			SandwichIngredient.Tomato2, SandwichIngredient.Cucumber2, SandwichIngredient.Ketchup, SandwichIngredient.Mayonnaise};
		peanutButterSpecial.ingredients = new SandwichIngredient[10]{SandwichIngredient.Pickle1, SandwichIngredient.PeanutButter, SandwichIngredient.Bacon1, SandwichIngredient.Pickle2,
			SandwichIngredient.Cucumber1, SandwichIngredient.Bacon2, SandwichIngredient.Jelly, SandwichIngredient.Tomato1, SandwichIngredient.Tomato2, SandwichIngredient.Cucumber2};

		Sandwich eggSpecial = new Sandwich("7_ES", "Egg Special");
		eggSpecial.layout = new SandwichIngredient[19]{SandwichIngredient.Empty, SandwichIngredient.DarkLettuce, SandwichIngredient.Pickle1, SandwichIngredient.Balogna, 
			SandwichIngredient.Mustard, SandwichIngredient.Jelly, SandwichIngredient.Pickle2, SandwichIngredient.ProvoloneCheese, SandwichIngredient.RoastBeef, SandwichIngredient.Ketchup,
			SandwichIngredient.Egg, SandwichIngredient.LightLettuce, SandwichIngredient.Empty, SandwichIngredient.Tomato1, SandwichIngredient.Turkey, SandwichIngredient.Cucumber1,
			SandwichIngredient.Ham, SandwichIngredient.Tomato2, SandwichIngredient.CheddarCheese};
		eggSpecial.ingredients = new SandwichIngredient[10]{SandwichIngredient.Egg, SandwichIngredient.LightLettuce, SandwichIngredient.Ketchup, SandwichIngredient.ProvoloneCheese,
			SandwichIngredient.Cucumber1, SandwichIngredient.Pickle1, SandwichIngredient.Mustard, SandwichIngredient.DarkLettuce, SandwichIngredient.CheddarCheese, 
			SandwichIngredient.Tomato1};

		Sandwich balognaSpecial = new Sandwich("8_BS", "Balogna Special");
		balognaSpecial.layout = new SandwichIngredient[19]{SandwichIngredient.Empty, SandwichIngredient.LightLettuce, SandwichIngredient.PeanutButter, SandwichIngredient.RoastBeef,
			SandwichIngredient.Salami, SandwichIngredient.CheddarCheese, SandwichIngredient.Tomato1, SandwichIngredient.Cucumber1, SandwichIngredient.Balogna, SandwichIngredient.Bacon1,
			SandwichIngredient.Turkey, SandwichIngredient.Cucumber2, SandwichIngredient.Empty, SandwichIngredient.SwissCheese, SandwichIngredient.Tomato2, SandwichIngredient.Mayonnaise,
			SandwichIngredient.Ham, SandwichIngredient.Jelly, SandwichIngredient.DarkLettuce};
		balognaSpecial.ingredients = new SandwichIngredient[10]{SandwichIngredient.Balogna, SandwichIngredient.SwissCheese, SandwichIngredient.Ham, SandwichIngredient.DarkLettuce,
			SandwichIngredient.Bacon1, SandwichIngredient.Cucumber1, SandwichIngredient.Turkey, SandwichIngredient.Tomato1, SandwichIngredient.Mayonnaise, SandwichIngredient.Salami};*/

		sandwiches = new List<Sandwich>();

		sandwiches.Add(hamSpecial);
		sandwiches.Add(roastBeefSpecial);
		sandwiches.Add(baconSpecial);
		sandwiches.Add(turkeySpecial);


		//sandwiches.Add(salamiSpecial);
		//sandwiches.Add(peanutButterSpecial);
		//sandwiches.Add(eggSpecial);
		//sandwiches.Add(balognaSpecial);

		//shuffle up the sandwiches into a random order
		System.Random rnd = new System.Random();
		for(int i=1; i < sandwiches.Count; i++)
		{
			int j = rnd.Next(i, sandwiches.Count);
			Sandwich temp = sandwiches[i];
			sandwiches[i] = sandwiches[j];
			sandwiches[j] = temp;
		}
		nextSandwichLongName = sandwiches[0].longName;

		//setup the conditions
		conditions = new List<InteractiveGazeCondition>();
		conditions.Add(InteractiveGazeCondition.FullModel);
		conditions.Add(InteractiveGazeCondition.None);
		conditions.Add(InteractiveGazeCondition.NoGazeDetection);
		conditions.Add(InteractiveGazeCondition.NoGazeProduction);

		for(int i=1; i < conditions.Count; i++)
		{
			int j = rnd.Next(i, conditions.Count);
			InteractiveGazeCondition temp = conditions[i];
			conditions[i] = conditions[j];
			conditions[j] = temp;
		}

		//speech and gaze behaviors
		//int curspeak = -1;
		int curgaze = -1;

		//Find the relevant controllers
		intGazeCtrl = agents[agentName].GetComponent<InteractiveGazeController>();
		AsynchronousSocketListener[] allSocketListeners = agents[agentName].GetComponents<AsynchronousSocketListener>();
		foreach (AsynchronousSocketListener asl in allSocketListeners) {
			if (asl.ID == SocketSource.SpeechRecognition) {
				listenSocket = asl;
			}
			if (asl.ID == SocketSource.ActionTracker) {
				actionSocket = asl;
			}
		}
		GazeController gazeCtrl = agents[agentName].GetComponent<GazeController>();

		//set the scene lights
		GameObject[] lights = GameObject.FindGameObjectsWithTag("Spotlight");
		float[] lightIntensities = new float[lights.Length];
		for (int i = 0; i < lights.Length; ++i) {
			lightIntensities[i] = lights[i].light.intensity;
			lights[i].light.intensity = 0f;	
		}
		
		// Initialize gaze
		gazeCtrl.GazeAt(intGazeCtrl.mutualGazeObject);
		yield return new WaitForSeconds(0.6f);

		while (!startScenario) {
			yield return 0;
		}

		yield return new WaitForSeconds (5f);
		for (int i = 0; i < lights.Length; ++i) {
			lights[i].light.intensity = lightIntensities[i];
		}
		
		//GameObject.Find ("Shield").SetActive(false);
		
		//SCREENSHOT CODE
		//if (captureVideo) {
		//	vidcap.enabled = true;
		//	vidcap.Start();
		//}

		if( !Directory.Exists("./Log") )
		{
			Directory.CreateDirectory("./Log");
		}
		string Logfile = String.Format("{0:yyyy-MM-dd_HH-mm-ss}.log", DateTime.Now);
		LogStream = new StreamWriter( "Log/" + Logfile, true );
		LogStream.AutoFlush = true;
		intGazeCtrl.LogStream = LogStream;

		//Some general introduction
		//yield return new WaitForSeconds(1f);
		//yield return StartCoroutine(SpeakAndWait("intro"));
		//yield return new WaitForSeconds(0.5f);

		//Iterate through each of the sandwiches
		currentSandwichIndex = 0;
		foreach(InteractiveGazeCondition c in conditions)
		{
			yield return new WaitForSeconds(1f);
			yield return StartCoroutine(SpeakAndWait("intro"));
			yield return new WaitForSeconds(0.5f);

            intGazeCtrl.setCondition(c);
			LogStream.WriteLine(String.Format ("{0:HH:mm:ss.ffff}\tCondition:\t{1}", DateTime.Now, intGazeCtrl.getCondition().ToString()));
			intGazeCtrl.changePhase(ReferencePhase.None);
			actionPerformed = false;
			for (int setIndex = 0; setIndex < 1; setIndex++)
			{
				LogStream.WriteLine();
				Sandwich s = sandwiches[currentSandwichIndex];
				sandwichBaseString = s.name;
				LogStream.WriteLine(String.Format ("{0:HH:mm:ss.ffff}\tSandwich Start:\t{1}", DateTime.Now, sandwichBaseString));
				//give the sandwich intro
				yield return StartCoroutine(SpeakAndWait(sandwichBaseString + "_intro"));
				completedObjects.Clear();
				wrongActionCells.Clear();
				//iterate through the 10 reference-action sequences for each sandwich
				sandwichInProgress = true;
				for (int i = 0; i < s.ingredients.Length; ++i)
				{
					currentReferenceNumber = i+1;
					LogStream.WriteLine(String.Format ("{0:HH:mm:ss.ffff}\tReference-Action Sequence Start:\t{1}", DateTime.Now, currentReferenceNumber));
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
					ReferenceToActionStartTime = DateTime.Now;
					timeSinceLastRefinement = 0f;
					yield return new WaitForSeconds(0.5f);

					//MONITOR
					intGazeCtrl.changePhase(ReferencePhase.Monitor);
					while (!actionPerformed) {
						yield return 0;
					}

					LogStream.WriteLine(String.Format ("{0:HH:mm:ss.ffff}\tCorrect Action Performed:\t{1}\t{2}ms", DateTime.Now, currentReferenceNumber, 
					                                   (DateTime.Now - ReferenceToActionStartTime).TotalMilliseconds));
					ReferenceToActionStartTime = DateTime.MinValue;

					//ACTION
					intGazeCtrl.changePhase(ReferencePhase.Action);
					yield return new WaitForSeconds(2f);
					completedObjects.Add (intGazeCtrl.getReferenceIndex());
					s.removeReferenceObjectFromLayout(i);
					LogStream.WriteLine(String.Format ("{0:HH:mm:ss.ffff}\tReference-Action Sequence End:\t{1}", DateTime.Now, currentReferenceNumber));
				}
				sandwichInProgress = false;
				
				yield return StartCoroutine(SpeakAndWait(sandwichBaseString + "_outro"));
				LogStream.WriteLine(String.Format ("{0:HH:mm:ss.ffff}\tSandwich End:\t{1}", DateTime.Now, sandwichBaseString));
				actionSocket.resetActionDEBUG();
				
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

				if (setIndex == 1)
				{
					GameObject.Find ("FinalInstructions").guiText.enabled = true;
				}
				if (currentSandwichIndex < (sandwiches.Count-1))
				{
					nextSandwichLongName = sandwiches[currentSandwichIndex+1].longName;
					startScenario = false;
					while (!startScenario) {
						yield return 0;
					}
				}
				GameObject.Find ("FinalInstructions").guiText.enabled = false;
				yield return new WaitForSeconds (5f);
				//bring the lights back up
				for (int i = 0; i < lights.Length; ++i) {
					lights[i].light.intensity = lightIntensities[i];
				}
				currentSandwichIndex++;
			}
			LogStream.WriteLine();
			LogStream.WriteLine();
		}

		//The outro
		//yield return StartCoroutine(SpeakAndWait("outro"));
		//yield return new WaitForSeconds(1f);
		foreach (GameObject g in lights) {
			g.light.intensity = 0f;	
		}
	}
	
	void Update() {
		//Periodic smiles and eyebrow raises to increase lifelikeness
		nextSmileCounter += Time.deltaTime;
		if (nextSmileCounter >= nextSmileTime) {
			nextSmileCounter = 0;
			nextSmileTime = nextSmileDistribution.NextDouble();
			StartCoroutine(agentSmileAndEyebrow(smileLengthDistribution.NextDouble()));
		}

		if (sandwichInProgress)
		{
			timeSinceLastRefinement += Time.deltaTime;
			//Parse the messages from the action socket
			parseActionSocket();
			//The reference object has been moved
			if (visibleGridCells.Contains (intGazeCtrl.getReferenceIndex()))
			{
				actionPerformed = true;
			}
			else
			{
				foreach(int visibleCell in visibleGridCells)
				{
					if (!completedObjects.Contains(visibleCell) && !wrongActionCells.Contains(visibleCell))
					{
						//the wrong object has been moved (disregard grid locations that have been completed)
						if (ReferenceToActionStartTime == DateTime.MinValue)
						{
							LogStream.WriteLine(String.Format ("{0:HH:mm:ss.ffff}\tWRONG ACTION PERFORMED OUT OF INTENDED SEQUENCE", DateTime.Now));
						}
						else
						{
							LogStream.WriteLine(String.Format ("{0:HH:mm:ss.ffff}\tWrong Action Performed:\t{1}\t{2}ms", DateTime.Now, visibleCell,
							                                   (DateTime.Now - ReferenceToActionStartTime).TotalMilliseconds));
						}
						wrongActionCells.Add (visibleCell);
						StartCoroutine(wrongActionSequence());
					}
				}
			}
			
			//The user has asked for clarification during the monitor phase. Offer a refinement.
			if (listenSocket.SocketContent.Contains("clarify"))
			{
				LogStream.WriteLine(String.Format ("{0:HH:mm:ss.ffff}\tUser Speech:\t{1}", DateTime.Now, listenSocket.SocketContent));
				listenSocket.SocketContent = "nothing";
				if (intGazeCtrl.phase == ReferencePhase.Monitor)
				{
					LogStream.WriteLine(String.Format ("{0:HH:mm:ss.ffff}\tSpeech-Triggered Refinement", DateTime.Now));
					StartCoroutine(refinementSequence());
					return;
				}
			}
			
			//The interactive gaze controller has flagged the need for a refinement during the monitor phase
			if (intGazeCtrl.isOfferRefinementFlagSet() && intGazeCtrl.phase == ReferencePhase.Monitor && timeSinceLastRefinement > refinementRefreshPeriod)
			{
				LogStream.WriteLine(String.Format ("{0:HH:mm:ss.ffff}\tGaze-Triggered Refinement", DateTime.Now));
				StartCoroutine(refinementSequence());
				return;
			}
		}
	}

	// Speak the utterance and wait for it to be finished. Also writes begin and end times to log file.
	IEnumerator SpeakAndWait(string SpeechFile)
	{
		LogStream.WriteLine(String.Format ("{0:HH:mm:ss.ffff}\tSpeech Start:\t{1}", DateTime.Now, SpeechFile));
		int curspeak = Speak (agentName, SpeechFile);
		yield return StartCoroutine(WaitUntilFinished(curspeak));
		LogStream.WriteLine(String.Format ("{0:HH:mm:ss.ffff}\tSpeech End:\t{1}", DateTime.Now, SpeechFile));
	}

	public void OnGUI() {
		if (!startScenario) {
			if (GUI.Button(new Rect(10, 10, 500, 100), "Start " + nextSandwichLongName)) {
				startScenario = true;
			}
		}
	}

	//Parsing function for the messages coming from the action socket.
	//Messages are in the form of "visible:0:1:2:4:5:6"
	//which would mean that grid cells 0, 1, 2, 4, 5, and 6 are currently visible
	private void parseActionSocket() {
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
	}

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
		LogStream.WriteLine(String.Format ("{0:HH:mm:ss.ffff}\tWrong Action-Triggered Refinement", DateTime.Now));
		intGazeCtrl.changePhase(ReferencePhase.Refinement);
		yield return StartCoroutine(SpeakAndWait("wrong" + wrongBeginningIndex));

		//alternate the way this correction is made
		if (wrongBeginningIndex == 1)
			wrongBeginningIndex = 2;
		else
			wrongBeginningIndex = 1;

		yield return StartCoroutine(SpeakAndWait(sandwichBaseString + "_Refinement_" + currentReferenceNumber));

		yield return new WaitForSeconds(0.5f);
		//offer a refinement after the correction
		//StartCoroutine(refinementSequence());
		intGazeCtrl.changePhase(ReferencePhase.Monitor);
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
		if (LogStream != null)
		{
			LogStream.WriteLine(String.Format ("{0:HH:mm:ss.ffff}\tEnd of Log", DateTime.Now));
			LogStream.Flush();
			LogStream.Close();
		}
	}
};