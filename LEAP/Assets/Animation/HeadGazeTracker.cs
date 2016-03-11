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

public enum HeadGazeTrackerState
{
	Calibration,
	Tracking
};

public class CalibratedPlane
{
    //upper left corner
    protected Vector3 point0;
    //upper right corner
    protected Vector3 point1;
    //lower left corner
    protected Vector3 point2;
    //lower right corner
    protected Vector3 point3;
    
    //The planar surface
    protected Plane plane;
    //A ray with origin at the upper left and pointing right across the plane
    public Ray widthRay;
    //A ray with origin at the upper left and pointing down the plane
    public Ray heightRay;
    
    protected float width;
    protected float height;
    //A buffer to allow for gaze just off the plane in any direction
    protected float errorBuffer = 0.1f;

    protected Matrix4x4 worldToPlaneTransformation;
    protected Matrix4x4 homographyMatrix;

    public void setupPlane(Vector3 p0, Vector3 p1, Vector3 p2)
    {
        point0 = new Vector3(p0.x, p0.y, p0.z);
        point1 = new Vector3(p1.x, p1.y, p1.z);
        point2 = new Vector3(p2.x, p2.y, p2.z);
        point3 = point2 + (point1 - point0);

        //Construct the plane and rays from the given points
        plane = new Plane(point0, point1, point2);
        widthRay = new Ray(point0, point1 - point0);
        heightRay = new Ray(point0, point2 - point0);
        
        //Figure out the width and height in camera space
        width = Vector3.Cross(heightRay.direction, point1 - point0).magnitude;
        height = Vector3.Cross(widthRay.direction, point2 - point0).magnitude;

        widthRay.direction.Normalize();
        heightRay.direction.Normalize();
        Vector4 planeX = new Vector4(widthRay.direction.x, widthRay.direction.y, widthRay.direction.z, 0);
        Vector4 planeY = new Vector4(plane.normal.x, plane.normal.y, plane.normal.z, 0);
        Vector4 planeZ = new Vector4(heightRay.direction.x, heightRay.direction.y, heightRay.direction.z, 0);
        Vector4 T = new Vector4(point0.x, point0.y, point0.z, 1);
        Matrix4x4 planeToWorld = new Matrix4x4();
        planeToWorld.SetColumn(0, planeX);
        planeToWorld.SetColumn(1, planeY);
        planeToWorld.SetColumn(2, planeZ);
        planeToWorld.SetColumn(3, T);
        worldToPlaneTransformation = planeToWorld.inverse;

        //UnityEngine.Debug.Log("Width: " + width + " ; Height: " + height);
        //UnityEngine.Debug.Log("Point 0 in plane coordinates: " + ConvertWorldToPlaneCoordinates(point0));
        //UnityEngine.Debug.Log("Point 1 in plane coordinates: " + ConvertWorldToPlaneCoordinates(point1));
        //UnityEngine.Debug.Log("Point 2 in plane coordinates: " + ConvertWorldToPlaneCoordinates(point2));
    }

    public void CalibratePlaneHomography(Ray[] headRays)
    {
        if (headRays == null || headRays.Length < 4)
        {
            return;
        }

        Vector3[] intersectionPoints = new Vector3[4];
        for (int i = 0; i < 4; i++)
        {
            float rayDistance = Raycast(headRays[i]);
            intersectionPoints[i] = headRays[i].GetPoint(rayDistance);
        }

        //We want to create a homography of the form Hx = x' to turn "raw" planar points (from calibration step)
        //into "canonical" planar points defined by point0, point1, point2, point3

        //H = [a, b, c; d, e, f; g, h, 1]

        //Solve an Ax = b problem where x is the flattened H, A is an accumulation of calibration points, and
        //b is a vector of the canonical planar points

        Vector2[] calibrationPlanarPoints = new Vector2[4];
        for (int i = 0; i < 4; i++)
        {
            calibrationPlanarPoints[i] = ConvertWorldToPlaneCoordinates(intersectionPoints[i]);
        }

        double[,] A = new double[12, 8];
        for (int i = 0; i < 4; i++)
        {
            A[i*3,0] = calibrationPlanarPoints[i].x;
            A[i*3,1] = calibrationPlanarPoints[i].y;
            A[i*3,2] = 1;

            A[i*3 + 1,3] = calibrationPlanarPoints[i].x;
            A[i*3 + 1,4] = calibrationPlanarPoints[i].y;
            A[i*3 + 1,5] = 1;

            A[i*3 + 2,6] = calibrationPlanarPoints[i].x;
            A[i*3 + 2,7] = calibrationPlanarPoints[i].y;
        }

        Vector2[] canonicalPlanarPoints = new Vector2[4]{ ConvertWorldToPlaneCoordinates(point0),
            ConvertWorldToPlaneCoordinates(point1), ConvertWorldToPlaneCoordinates(point2),
            ConvertWorldToPlaneCoordinates(point3) };

        double[] b = new double[12];
        for (int i = 0; i < 4; i++)
        {
            b[i*3] = canonicalPlanarPoints[i].x;
            b[i*3+1] = canonicalPlanarPoints[i].y;
        }

        int info;
        alglib.densesolverlsreport rep;
        double[] homographyArray = new double[8];
        alglib.rmatrixsolvels (A, 12, 8, b, 0, out info, out rep, out homographyArray);

        homographyMatrix = new Matrix4x4();
        homographyMatrix [0, 0] = (float)homographyArray [0];
        homographyMatrix [0, 1] = (float)homographyArray [1];
        homographyMatrix [0, 2] = (float)homographyArray [2];
        homographyMatrix [1, 0] = (float)homographyArray [3];
        homographyMatrix [1, 1] = (float)homographyArray [4];
        homographyMatrix [1, 2] = (float)homographyArray [5];
        homographyMatrix [2, 0] = (float)homographyArray [6];
        homographyMatrix [2, 1] = (float)homographyArray [7];
        homographyMatrix [2, 2] = 1f;
        homographyMatrix [3, 3] = 1f;
    }

    protected Vector2 HomographyConversion(Vector2 p)
    {
        Vector4 p_full = new Vector4(p.x, p.y, 1, 1);
        Vector4 converted_full = homographyMatrix * p_full;
        return new Vector2(converted_full.x, converted_full.y);
    }

    public bool RayIntersectsBoundedPlane(Ray r)
    {
        float rayDistance = Raycast(r);
        Vector3 p = r.GetPoint(rayDistance);
        Vector2 planarCoordinates = HomographyConversion(ConvertWorldToPlaneCoordinates(p));

        if ((planarCoordinates.x > (width + errorBuffer)) || (planarCoordinates.x < (-errorBuffer)) ||
            (planarCoordinates.y > (height + errorBuffer)) || (planarCoordinates.y < (-errorBuffer)))
        {
            return false;
        } 
        else
        {
            return true;
        }
    }

    protected Vector2 ConvertWorldToPlaneCoordinates(Vector3 p)
    {
        Vector4 p_homogenous = new Vector4(p.x, p.y, p.z, 1);
        Vector4 p_prime_homogenous = worldToPlaneTransformation * p_homogenous;
        //on the plane, 'y' is guaranteed to be 0, so we only care about 'x' and 'z'
        return new Vector2(p_prime_homogenous.x, p_prime_homogenous.z);
    }

    public float Raycast(Ray r)
    {
        float rayDistance;
        plane.Raycast(r, out rayDistance);
        return rayDistance;
    }
}

//A class for calibrating the table position in the camera's coordinate system.
public class CalibratedTable : CalibratedPlane
{

	public CalibratedTable(double d, double h, double a)
	{
		Vector3 p0 = new Vector3(-0.6f, (float)(d*Math.Sin(-a) - h*Math.Cos(-a)), 
                                 (float)(d*Math.Cos (-a) + h*Math.Sin (-a)));
		Vector3 p1 = new Vector3(0.6f, (float)(d*Math.Sin(-a) - h*Math.Cos(-a)), 
                                 (float)(d*Math.Cos (-a) + h*Math.Sin (-a)));
		Vector3 p2 = new Vector3(-0.6f, (float)((d+0.6)*Math.Sin(-a) - h*Math.Cos(-a)), 
                                 (float)((d+0.6)*Math.Cos (-a) + h*Math.Sin (-a)));

		//UnityEngine.Debug.Log (point0.x + ", " + point0.y + ", " + point0.z);
		//UnityEngine.Debug.Log (point1.x + ", " + point1.y + ", " + point1.z);
		//UnityEngine.Debug.Log (point2.x + ", " + point2.y + ", " + point2.z);

        setupPlane(p0, p1, p2);
	}

	//Given an (x,y) position in the plane of the table, find which grid location it corresponds with (if any)
	public int GetGridLocation(Ray r)
	{
		//The point is off the table
        if (!RayIntersectsBoundedPlane(r))
        {
            return 0;
        }

        float rayDistance = Raycast(r);
        Vector3 p = r.GetPoint(rayDistance);
        Vector3 planarCoordinates = HomographyConversion(ConvertWorldToPlaneCoordinates(p));

		//There are 6 columns and 3 rows
		float gridWidth = width / 6f;
		float gridHeight = height / 3f;

		//(x,y) system has its origin at the upper-left, but the grid cells (1-18)
		//count from left-to-right, bottom-to-top

		//Figure out which row we're in (bottom to top)
		int row = 0;
		for (int i = 1; i < 3; i++)
		{
			if (planarCoordinates.y < ((float)i * gridHeight))
			{
				row = 3 - i;
				break;
			}
		}

		//Figure out which column we're in (left to right)
		int col = 5;
		for (int i = 0; i < 5; i++)
		{
			if (planarCoordinates.x < ((float)(i+1) * gridWidth))
			{
				col = i;
				break;
			}
		}

		return row*6 + col + 1;
	}
}

public class CalibratedMonitor : CalibratedPlane
{
   
	public CalibratedMonitor(double d, double h, double a, double width, double height)
	{
		float halfwidth = (float)width / 2f;
		Vector3 p0 = new Vector3(-halfwidth, (float)(-d * Math.Sin(-a) + (h + height)*Math.Cos(-a)), 
                                 (float)(-d * Math.Cos (-a) - (h + height)*Math.Sin (-a)));
		Vector3 p1 = new Vector3(halfwidth, (float)(-d * Math.Sin(-a) + (h + height)*Math.Cos(-a)), 
                                 (float)(-d * Math.Cos (-a) - (h + height)*Math.Sin (-a)));
		Vector3 p2 = new Vector3(-halfwidth, (float)(-d * Math.Sin(-a) + h*Math.Cos(-a)), 
                                 (float)(-d * Math.Cos (-a) - h*Math.Sin (-a)));
		
		//UnityEngine.Debug.Log (point0.x + ", " + point0.y + ", " + point0.z);
		//UnityEngine.Debug.Log (point1.x + ", " + point1.y + ", " + point1.z);
		//UnityEngine.Debug.Log (point2.x + ", " + point2.y + ", " + point2.z);

        setupPlane(p0, p1, p2);
	}

}

public class HeadGazeTracker : AnimController
{
	//A socket for communicating with the realsense camera
	protected AsynchronousSocketListener socketListener = null;

	//Variables for calibration
	private CalibratedTable calibratedTable = null;
	private CalibratedMonitor calibratedMonitor = null;
	public bool doCalibration = false;
    private bool startCalibration = false;
	private string calibrationFileName = "HeadTrackerCalibration";
	private TextAsset calibrationFile = null;
	private StreamWriter calibrationWriter = null;
	private int calibrationStep = 0;
    private Ray[] tableCalibrationRays = new Ray[4];
    private Ray[] monitorCalibrationRays = new Ray[4];
	private bool calibrationComplete = false;
    private string[] calibrationButtonText = new string[8]{"Calibration Point: Upper-Left Table Corner", 
        "Calibration Point: Upper-Right Table Corner", "Calibration Point: Lower-Left Table Corner",
        "Calibration Point: Lower-Right Table Corner", "Calibration Point: Upper-Left Monitor Corner",
        "Calibration Point: Upper-Right Monitor Corner", "Calibration Point: Lower-Left Monitor Corner",
        "Calibration Point: Lower-Right Monitor Corner"};

    //Variables for the approximate location of the kinect
	public double distanceToTableEdge = 0;
	public double heightOffTable = 0;
	public double angle = 0;
	public double monitorWidth = 0;
	public double monitorHeight = 0;
	public double horizontalDistanceToMonitor = 0;
	public double verticalDistanceToMonitor = 0;
	
	private HeadGazeTrackerState currentState = HeadGazeTrackerState.Calibration;
	
	protected override void _Init()
	{
		//Get the socket listener connected to the head tracker
		AsynchronousSocketListener[] allSocketListeners = gameObject.GetComponents<AsynchronousSocketListener>();
		foreach (AsynchronousSocketListener asl in allSocketListeners)
		{
			if (asl.ID == SocketSource.HeadTracker)
			{
				socketListener = asl;
			}
		}

        //Create the table and monitor objects
        calibratedTable = new CalibratedTable(distanceToTableEdge, heightOffTable, angle * Math.PI / 180f);
        calibratedMonitor = new CalibratedMonitor(horizontalDistanceToMonitor, verticalDistanceToMonitor, 
                                                  angle * Math.PI / 180f, monitorWidth, monitorHeight);

		if (!doCalibration)
		{
			//Load an existing calibration file
			calibrationFile = Resources.Load(calibrationFileName) as TextAsset;
			bool calibrationFileLoaded = false;
			if (calibrationFile != null && calibrationFile.text != null)
			{
				calibrationFileLoaded = loadExistingCalibrationData(calibrationFile.text, calibratedTable, calibratedMonitor);
			}
			//Check if the file load was successful
			if (calibrationFileLoaded)
			{
                UnityEngine.Debug.LogWarning("Successfully found and loaded calibration file.");
			}
			else
			{
                UnityEngine.Debug.LogWarning("No calibration file was successfully loaded.");
			}
            calibrationComplete = true;
		}
		else
		{
			//Start a streamwriter for writing a new calibration file
			calibrationWriter = new StreamWriter("Assets\\Resources\\" + calibrationFileName + ".txt", false);
		}

	}

    public Ray createHeadPoseRay(double dir_x, double dir_y, double dir_z, 
                                 double center_x, double center_y, double center_z)
    {
        Vector3 headCenter = new Vector3 ((float)center_x, (float)center_y, (float)center_z);
        Vector3 pointingDirection = new Vector3 ((float)dir_x, (float)dir_y, (float)dir_z);
        pointingDirection.Normalize ();
        return new Ray(headCenter, pointingDirection);
    }

	//Extract calibration data from the file contents
	private bool loadExistingCalibrationData(string fileText, CalibratedTable ct, CalibratedMonitor cm)
	{
		string[] lines = fileText.Split('\n');
		if (lines.Length < 8)
		{
			return false;
		}

		for (int i = 0; i < 4; i++)
		{
            try
            {
                tableCalibrationRays[i] = parseHeadRayString(lines[i]);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning(e.Message);
                return false;
            }
		}
        for (int i = 4; i < 8; i++)
        {
            try
            {
                monitorCalibrationRays[i-4] = parseHeadRayString(lines[i]);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning(e.Message);
                return false;
            }
        }

        ct.CalibratePlaneHomography(tableCalibrationRays);
        cm.CalibratePlaneHomography(monitorCalibrationRays);

		return true;
	}

    private void WriteCalibrationFile()
    {
        if (tableCalibrationRays.Length != 4 || monitorCalibrationRays.Length != 4 || calibrationWriter == null)
        {
            UnityEngine.Debug.LogWarning("Failed in attempt to write a calibration file for head tracker");
            return;
        }

        for (int i = 0; i < 4; i++)
        {
            calibrationWriter.WriteLine(tableCalibrationRays [i].direction.x + "," + 
                                        tableCalibrationRays [i].direction.y + "," +
                                        tableCalibrationRays [i].direction.z + ";" +
                                        tableCalibrationRays [i].origin.x + "," + 
                                        tableCalibrationRays [i].origin.y + "," +
                                        tableCalibrationRays [i].origin.z);
        }
        for (int i = 0; i < 4; i++)
        {
            calibrationWriter.WriteLine(monitorCalibrationRays [i].direction.x + "," + 
                                        monitorCalibrationRays [i].direction.y + "," +
                                        monitorCalibrationRays [i].direction.z + ";" +
                                        monitorCalibrationRays [i].origin.x + "," + 
                                        monitorCalibrationRays [i].origin.y + "," +
                                        monitorCalibrationRays [i].origin.z);
        }
    }

	//Parse a single line of head tracker data (nose and head position) into a HeadRay
	private Ray parseHeadRayString(string headRayString)
	{
		headRayString.Trim ();
		string[] dirAndCenter = headRayString.Split (';');
        if (dirAndCenter.Length < 2)
		{
            throw (new Exception("Error in parsing head ray from string"));
		}
        string[] dir = dirAndCenter[0].Split (',');
		if (dir.Length < 3) 
		{
            throw (new Exception("Error in parsing head ray from string"));
		}
        string[] center = dirAndCenter[1].Split (',');
		if (center.Length < 3)
		{
            throw (new Exception("Error in parsing head ray from string"));
		}

		return createHeadPoseRay(Convert.ToDouble(dir[0].Trim ()), Convert.ToDouble(dir[1].Trim ()), 
                                 Convert.ToDouble(dir[2].Trim ()),
                                 Convert.ToDouble(center[0].Trim ()), Convert.ToDouble(center[1].Trim ()), 
                                 Convert.ToDouble(center[2].Trim ()));
	}

    public void StartCalibration()
    {
        startCalibration = true;
    }

	public void OnGUI()
	{
		//A button will appear during calibration for capturing 8 head vectors
        //(1 to each corner of the table and 1 to each corner of the monitor)
		if (doCalibration && startCalibration && (currentState == HeadGazeTrackerState.Calibration))
        {
            if (calibrationStep < 4)
            {
                if (GUI.Button(new Rect(10, 500, 300, 50), calibrationButtonText [calibrationStep]))
                {
                    try
                    {
                        tableCalibrationRays [calibrationStep] = parseHeadRayString(socketListener.SocketContent);
                    }
                    catch (Exception e)
                    {
                        UnityEngine.Debug.LogError(e.Message);
                        return;
                    }
                    calibrationStep++;
                }
            } else if (calibrationStep < 8)
            {
                if (GUI.Button(new Rect(10, 500, 300, 50), calibrationButtonText [calibrationStep]))
                {
                    try
                    {
                        monitorCalibrationRays [calibrationStep % 4] = parseHeadRayString(socketListener.SocketContent);
                    }
                    catch (Exception e)
                    {
                        UnityEngine.Debug.LogError(e.Message);
                        return;
                    }
                    calibrationStep++;
                    if (calibrationStep == 8)
                    {
                        calibrationComplete = true;
                        calibratedTable.CalibratePlaneHomography(tableCalibrationRays);
                        calibratedMonitor.CalibratePlaneHomography(monitorCalibrationRays);
                        WriteCalibrationFile();
                    }
                }
            }
        }
	}

	//A function for querying the current grid location that the user's head is pointed at
	public int getGridLocation()
	{
		if ((currentState == HeadGazeTrackerState.Tracking) && (socketListener.connectionMade) && 
		    (socketListener.SocketContent != null) && (calibratedTable != null) && (calibratedMonitor != null))
		{
			//Get the current head ray
            Ray h;
            try
            {
                h = parseHeadRayString(socketListener.SocketContent);
            }
			catch (Exception e)
            {
                UnityEngine.Debug.LogError(e.Message);
                return 0;
            }

			//First try to intersect the head ray with the monitor
			if (calibratedMonitor.RayIntersectsBoundedPlane(h))
			{
				return 19;
			}

            return calibratedTable.GetGridLocation(h);
		}
		else
		{
			return 0;
		}
	}

	protected override void _Update()
	{
	}
	
	protected virtual void Update_Calibration()
	{
		if (calibrationComplete)
		{
			GoToState((int)HeadGazeTrackerState.Tracking);
		}
	}

	protected virtual void Update_Tracking()
	{
		/*if ((socketListener.connectionMade) & (socketListener.SocketContent != null))
		{
			UnityEngine.Debug.Log ("Grid: " + getGridLocation());
		}*/
	}
	
	protected virtual void Transition_CalibrationTracking()
	{
		//Create the calibrated table object
		currentState = HeadGazeTrackerState.Tracking;
	}
	
	protected virtual void Transition_TrackingCalibration()
	{
		currentState = HeadGazeTrackerState.Calibration;
	}

	public void OnApplicationQuit() {
		if (calibrationWriter != null)
		{
			calibrationWriter.Close();
		}
	}

	public override void UEdCreateStates()
	{
		// Initialize states
		_InitStateDefs<HeadGazeTrackerState>();
		_InitStateTransDefs( (int)HeadGazeTrackerState.Tracking, 1 );
		_InitStateTransDefs( (int)HeadGazeTrackerState.Calibration, 1 );

		//init update functions
		states[(int)HeadGazeTrackerState.Tracking].updateHandler = "Update_Tracking";
		states[(int)HeadGazeTrackerState.Calibration].updateHandler = "Update_Calibration";

		//init transitions
		states[(int)HeadGazeTrackerState.Tracking].nextStates[0].nextState = "Calibration";
		states[(int)HeadGazeTrackerState.Tracking].nextStates[0].transitionHandler = "Transition_TrackingCalibration";

		states[(int)HeadGazeTrackerState.Calibration].nextStates[0].nextState = "Tracking";
		states[(int)HeadGazeTrackerState.Calibration].nextStates[0].transitionHandler = "Transition_CalibrationTracking";
	}
};