using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.IO;

public enum SpeechState
{
	NoSpeech,
	PrepareSpeech,
	Speaking
};

public enum SpeechType
{
	Question,
	Answer,
	Other
};

/// <summary>
/// Controller for execution of speech from audio or text.
/// </summary>
[RequireComponent (typeof(AudioSource))]
public class SpeechController : AnimController
{
	/// <summary>
	/// Collection of speech clips for the current agent.
	/// This is needed because Unity is a buggy pile of crap
	/// where documentation says one thing while the engine does the opposite.
	/// </summary>
	public AudioClip[] speechClips;
	public TextAsset[] visemeFiles;
	
	/// <summary>
	/// Audio clip containing next speech.
	/// </summary>
	public AudioClip speechClip = null;
	
	/// <summary>
	/// Text that should be rendered as speech. 
	/// </summary>
	public string speechText = "";
	
	/// <summary>
	/// If true, the agent will start a speech utterance on the next  frame.
	/// </summary>
	public bool doSpeech = false;
	
	/// <summary>
	/// If true, the agent will stop an ongoing speech utterance on
	/// the next frame.
	/// </summary>
	public bool stopSpeech = false;
	
	/// <summary>
	/// How much audio is delayed after lip-sync (in seconds).
	/// </summary>
	//public float audioDelayTime = 0.08f;
	public float audioDelayTime = 0f;

	/// <summary>
	/// If true, the speech controller will automatically have
	/// the face controller apply random head motion during speech.
	/// </summary>
	public bool useSpeechMotion = true;
	
	/// <summary>
	/// Scale factor to apply to lip-sync animations. 
	/// </summary>
	/// <remarks>Things like this will be removed soon,
	/// and specified at AnimController level.</remarks>
	public float lipSyncScale = 1f; // TODO: get rid of this later
	
	protected float curDelayTime = 0;
	[HideInInspector]
	public float curPlayTime = 0;
	
	protected BodyIdleController bodyIdleCtrl;
	protected FaceController faceCtrl;
	protected GazeController gazeCtrl;
	protected MorphController morphCtrl;
	
	public bool roboSpeech = false;
	public bool lipSyncFromVisemeFiles = false;
	public Transform mouth = null;
	
	[HideInInspector]
	public SpeechType speechType = SpeechType.Other;
	[HideInInspector]
	public bool prepareSpeech = false;
	
	//Get audio intensity for robo speech
	protected int qSamples = 2048;  // array size
	protected float rmsValue;   // sound level - RMS
	protected float maxRmsValue = 0f;
	protected float maxLightIntensity = 8f;

	private float visemeTimer = 0f;
	private List<float> visemeTriggerTimes = new List<float>();
	private List<int> visemesToTrigger = new List<int>();

	//private double[] visemeTriggerTimes = new double[53]{0.1, 0.235, 0.295, 0.355, 0.44, 0.49, 0.49, 0.595, 0.65, 0.695 ,0.755, 0.88, 0.975, 0.975, 1.08, 1.125, 1.205, 1.275, 1.275, 1.375, 1.45, 1.5, 1.575, 1.575, 
	//	1.67, 1.765, 1.79, 1.915, 2.025, 2.07, 2.13, 2.19, 2.265, 2.265, 2.35, 2.43, 2.555, 2.635, 2.68, 2.715, 2.765, 2.84, 2.84, 2.96, 3.025, 3.085, 3.14, 3.245, 3.31, 3.415, 3.515, 3.575, 3.715};
	//private int[] visemes = new int[53]{0, 18, 5, 13, 15, 19, 2, 6, 7, 6, 14, 19, 6, 19, 16, 6, 7, 12, 2, 4, 19, 4, 21, 4, 6, 20, 1, 15, 1, 19, 19, 7, 6, 19, 16, 20, 3, 14, 19, 17, 1, 21, 4, 6, 20, 1, 19, 15, 21, 4, 16, 1, 14};
	private Dictionary<int,string> visemeToMorphChannel = new Dictionary<int,string>();

	/// <summary>
	/// Length of the current speech utterance.
	/// </summary>
	public virtual float SpeechLength
	{
		get
		{
			if (speechClip != null)
				return (speechClip.length/audio.pitch);
			else
				return 0f;
		}
	}
	
	/// <summary>
	/// Perform a speech action.
	/// </summary>
	/// <param name="speechName">
	/// Speech audio clip name.<see cref="System.String"/>
	/// </param>
	public virtual void Speak( string speechName )
	{
		// Find audio clip by that name
		foreach( AudioClip clip in speechClips )
		{
			if( clip.name == speechName )
			{
				Speak(clip);
				
				return;
			}
		}
		
		Debug.LogWarning( "Unable to play speech clip " + speechName + ". Clip not found." );
	}
	
	/// <summary>
	/// Perform a speech action.
	/// </summary>
	/// <param name="speech">
	/// Speech audio clip.<see cref="AudioClip"/>
	/// </param>
	public virtual void Speak( AudioClip speech )
	{
		StopSpeech();
		
		if( speech != null )
		{
			if (lipSyncFromVisemeFiles)
			{
				visemeTriggerTimes.Clear ();
				visemesToTrigger.Clear ();
				foreach (TextAsset t in visemeFiles)
				{
					if (t.name.Substring(0, t.name.IndexOf('.')) == speech.name)
					{
						string[] lines = t.text.Split('\n');
						foreach (string line in lines)
						{
							if (line != null && line.Length > 0)
							{
								string[] elements = line.Split('\t');
								if (elements.Length == 2)
								{
									float newTime = (float)Convert.ToDouble(elements[0]);
									int newViseme = Convert.ToInt32(elements[1]);
									visemeTriggerTimes.Add (newTime);
									visemesToTrigger.Add (newViseme);
								}
							}
						}
						break;
					}
				}
			}
			speechClip = speech;
			doSpeech = true;
		}
	}
		
	/// <summary>
	/// Perform a speech action.
	/// </summary>
	/// <param name="text">
	/// Speech text string.<see cref="System.String"/>
	/// </param>
	public virtual void SpeakText( string text )
	{
		StopSpeech();
		
		/*speechText = text;
		doSpeech = true;*/
	}
	
	/// <summary>
	/// Stop current speech action. 
	/// </summary>
	public virtual void StopSpeech()
	{
		stopSpeech = true;
	}

	protected override void _Init()
	{
		bodyIdleCtrl = gameObject.GetComponent<BodyIdleController>();
		faceCtrl = gameObject.GetComponent<FaceController>();
		gazeCtrl = gameObject.GetComponent<GazeController>();
		morphCtrl = gameObject.GetComponent<MorphController>();
		prepareSpeech = false;

		visemeToMorphChannel.Add(1,"VisemeAh");
		visemeToMorphChannel.Add(2,"VisemeAah");
		visemeToMorphChannel.Add(3,"VisemeAh");
		visemeToMorphChannel.Add(4,"VisemeEh");
		visemeToMorphChannel.Add(5,"VisemeEr");
		visemeToMorphChannel.Add(6,"VisemeIh");
		visemeToMorphChannel.Add(7,"VisemeW");
		visemeToMorphChannel.Add(8,"VisemeOh");
		visemeToMorphChannel.Add(9,"VisemeOh");
		visemeToMorphChannel.Add(10,"VisemeOh");
		visemeToMorphChannel.Add(11,"VisemeEh");
		visemeToMorphChannel.Add(12,"VisemeR");
		visemeToMorphChannel.Add(13,"VisemeR");
		visemeToMorphChannel.Add(14,"VisemeDST");
		visemeToMorphChannel.Add(15,"VisemeDST");
		visemeToMorphChannel.Add(16,"VisemeChJSh");
		visemeToMorphChannel.Add(17,"VisemeTh");
		visemeToMorphChannel.Add(18,"VisemeFV");
		visemeToMorphChannel.Add(19,"VisemeN");
		visemeToMorphChannel.Add(20,"VisemeKG");
		visemeToMorphChannel.Add(21,"VisemeBMP");
	}
	
	protected virtual void Update_NoSpeech()
	{
		stopSpeech = false;
		
		if( doSpeech && 
		   ( speechClip != null || speechText != "" ) )
		{
			// Render speech
			GoToState((int)SpeechState.PrepareSpeech);
		}

        if (!roboSpeech)
        {
            int i = morphCtrl.GetMorphChannelIndex("VisemeAah");
            morphCtrl.morphChannels[i].weight = 0f;
        }
	}
	
	protected virtual void Update_Speaking()
	{
		if(stopSpeech)
		{
			// Interrupt speech action
			GoToState((int)SpeechState.NoSpeech);
			return;
		}

		if (lipSyncFromVisemeFiles)
		{
			visemeTimer += Time.deltaTime;
			for (int idx = 1; idx < visemeTriggerTimes.Count; ++idx)
			{
				if (visemeTimer < visemeTriggerTimes[idx])
				{
					float t = visemeTimer - visemeTriggerTimes[idx-1];
					float interval = visemeTriggerTimes[idx] - visemeTriggerTimes[idx-1];

					if (visemesToTrigger[idx-1] != 0)
					{
						int lastVisemeMorphChannel = morphCtrl.GetMorphChannelIndex(visemeToMorphChannel[visemesToTrigger[idx-1]]);
						morphCtrl.morphChannels[lastVisemeMorphChannel].weight = 0.9f * (1.0f - (t/interval));
					}

					if (visemesToTrigger[idx] != 0)
					{
						int nextVisemeMorphChannel = morphCtrl.GetMorphChannelIndex(visemeToMorphChannel[visemesToTrigger[idx]]);
						morphCtrl.morphChannels[nextVisemeMorphChannel].weight = 0.9f * (t/interval);
					}
					break;
				}
			}
		}
		
		if( !audio.isPlaying )
		{
			curDelayTime += Time.deltaTime;
			if( curDelayTime > audioDelayTime )
				// Delay done, start playing the audio clip
				audio.Play();
		}
		else
		{

			curPlayTime += Time.deltaTime;
			
			if (roboSpeech && mouth != null) {
				float[] samples = new float[qSamples];
				audio.GetOutputData(samples, 0); // fill array with samples
				float sum = 0f;
				for (int i=0; i < qSamples; i++){
					sum += samples[i]*samples[i]; // sum squared samples
				}
				rmsValue = Mathf.Sqrt(sum/qSamples); // rms = square root of average
				if (rmsValue > maxRmsValue)
					maxRmsValue = rmsValue;
				
				for (int i = 0; i < mouth.childCount; ++i) {
					Light l = mouth.GetChild(i).gameObject.GetComponent<Light>();
					l.intensity = (rmsValue / maxRmsValue) * maxLightIntensity;
				}
			}
			
			if( curPlayTime > speechClip.length/audio.pitch ) {
				// Speech action done
				GoToState((int)SpeechState.NoSpeech);
			}
		}
		
		if( useSpeechMotion &&
		   gazeCtrl != null && gazeCtrl.StateId == (int)GazeState.NoGaze )
		{
			if( faceCtrl != null && faceCtrl.StateId != (int)FaceState.Speech )
				// Enable random head motion
				faceCtrl.speechMotionEnabled = true;
			if( bodyIdleCtrl != null && bodyIdleCtrl.StateId != (int)BodyIdleState.Engaged )
				// Enable random body motion
				bodyIdleCtrl.speechMotionEnabled = true;
		}
	}
	
	protected virtual void Update_PrepareSpeech()
	{
		//if (!prepareSpeech)
		GoToState((int)SpeechState.Speaking);
	}
	
	protected virtual void Transition_NoSpeechPrepareSpeech()
	{
		doSpeech = false;
		visemeTimer = 0f;
	}
	
	protected virtual void Transition_PrepareSpeechSpeaking()
	{
		visemeTimer = 0f;
		if( speechClip != null )
		{	
			// Render speech from audio clip
		
			// Play audio
			audio.clip = speechClip;
			if( audioDelayTime <= 0 )
				audio.Play();
			curDelayTime = 0;
			curPlayTime = 0;


			
			// Play lip animation
			if (!lipSyncFromVisemeFiles)
			{
				AnimationState anim = animation[speechClip.name];
				if( anim != null )
				{
					// Enable playback
					anim.enabled = true;
					anim.time = 0;
					anim.speed = audio.pitch;
					
					// Make sure it affects only the face
					for( int child_i = 0; child_i < transform.childCount; ++child_i )
					{
						Transform mcbone = transform.GetChild(child_i);
						
						if( mcbone.name.StartsWith(LEAPCore.morphAnimationPrefix) )
						{
							anim.AddMixingTransform(mcbone);
						}
					}
					
					// Configure blending and wrapping
					anim.wrapMode = WrapMode.Once;
					anim.blendMode = AnimationBlendMode.Additive;
					anim.weight = lipSyncScale;
					anim.layer = 10;
				}
				else
				{
					Debug.LogWarning( string.Format( "Missing lip animation for {0}", speechClip.name ) );
					curDelayTime = audioDelayTime;
				}
			}
		}
		else if( speechText != "" )
		{
			// Render speech from text
			
			// TODO: implement VTTS
		}
	}
	
	protected virtual void Transition_SpeakingNoSpeech()
	{
		stopSpeech = false;
		visemeTimer = 0f;
		
		//If this is a robot with light-up mouth, turn off its mouth lights
		if (roboSpeech && mouth != null) {
			maxRmsValue /= 2f;
			for (int i = 0; i < mouth.childCount; ++i) {
				Light l = mouth.GetChild(i).gameObject.GetComponent<Light>();
				l.intensity = 0f;
			}
		}
		
		if( audio.clip != null )
		{
			// Stop animation and audio
			animation.Stop(audio.clip.name);
			audio.Stop();
		}
		
		if( faceCtrl != null  )
			// Enable random head motion
			faceCtrl.speechMotionEnabled = false;
		if( bodyIdleCtrl != null  )
			// Enable random body motion
			bodyIdleCtrl.speechMotionEnabled = false;
		
	}
	
	public override void UEdCreateStates()
	{
		// Initialize states
		_InitStateDefs<SpeechState>();
		_InitStateTransDefs( (int)SpeechState.NoSpeech, 1 );
		_InitStateTransDefs( (int)SpeechState.PrepareSpeech, 1 );
		_InitStateTransDefs( (int)SpeechState.Speaking, 1 );
		states[(int)SpeechState.NoSpeech].updateHandler = "Update_NoSpeech";
		states[(int)SpeechState.NoSpeech].nextStates[0].nextState = "PrepareSpeech";
		states[(int)SpeechState.NoSpeech].nextStates[0].transitionHandler = "Transition_NoSpeechPrepareSpeech";
		states[(int)SpeechState.PrepareSpeech].updateHandler = "Update_PrepareSpeech";
		//states[(int)SpeechState.PrepareSpeech].lateUpdateHandler = "LateUpdate_PrepareSpeech";
		states[(int)SpeechState.PrepareSpeech].nextStates[0].nextState = "Speaking";
		states[(int)SpeechState.PrepareSpeech].nextStates[0].transitionHandler = "Transition_PrepareSpeechSpeaking";
		states[(int)SpeechState.Speaking].updateHandler = "Update_Speaking";
		//states[(int)SpeechState.Speaking].lateUpdateHandler = "LateUpdate_Speaking";
		states[(int)SpeechState.Speaking].nextStates[0].nextState = "NoSpeech";
		states[(int)SpeechState.Speaking].nextStates[0].transitionHandler = "Transition_SpeakingNoSpeech";
	}
}
