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

public class OculusGazeTracker : MonoBehaviour
{
    private GameObject userHead = null;
    private Ray userHeadRay;
    private GazeController gazeCtrl;
    private Vector3 agentHeadPosition;
    private GameObject[] gridTargets = null;

    public void Start()
    {
        userHead = GameObject.Find("CenterEyeAnchor");
        gazeCtrl = gameObject.GetComponent<GazeController>();

        //Get and sort the grid gaze targets
        gridTargets = GameObject.FindGameObjectsWithTag("GazeTarget");
        Array.Sort(gridTargets, delegate(GameObject g1, GameObject g2)
        {
            return g1.name.CompareTo(g2.name);
        });
    }

	//A function for querying the current grid location that the user's head is pointed at
	public int getGridLocation()
	{
        userHeadRay = new Ray(userHead.transform.position, userHead.transform.forward);
        float distanceThreshold = 0.2f;

        if (GeomUtil.DistanceToLine(userHeadRay, agentHeadPosition) < distanceThreshold)
        {
            return 19;
        }

        int closestTarget = 0;
        float minDistance = distanceThreshold;

        for (int i = 0; i < gridTargets.Length; ++i )
        {
            float d = GeomUtil.DistanceToLine(userHeadRay, gridTargets[i].transform.position);
            if (d < minDistance)
            {
                minDistance = d;
                closestTarget = i + 1;
            }
        }

        return closestTarget;
	}

    public void LateUpdate()
    {
        //We need to get the agent's head position in LateUpdate, after the anim controllers have applied all animations to the head joint.
        agentHeadPosition = gazeCtrl.Head.bone.position;

        //test
        /*int gt = getGridLocation();
        if (gt == 19)
        {
            UnityEngine.Debug.Log("Gazing at AGENT");
        }
        else if (gt > 0)
        {
            UnityEngine.Debug.Log("Gazing at " + gridTargets[gt-1].name);
        }
        else
        {
            UnityEngine.Debug.Log("Gazing at NOTHING");
        }*/
    }

    public Vector3 GetUserHeadPosition()
    {
        if (userHead != null)
        {
            return userHead.transform.position;
        }
        else
        {
            return Vector3.zero;
        }
    }

    public Vector3 GetUserHeadDirection()
    {
        if (userHead != null)
        {
            return userHead.transform.forward;
        }
        else
        {
            return Vector3.zero;
        }
    }

};