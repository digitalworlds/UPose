using System;
using System.IO;
using System.Threading;
using UnityEngine;


[DefaultExecutionOrder(-1)]
public class MotionTracking : MonoBehaviour
{
    public string host = "127.0.0.1"; // This machines host.
    public int port = 52733; // Must match the Python side.
    private Transform bodyParent;
    private GameObject linePrefab;
    public float multiplier = 10f;
    public float landmarkScale = 1f;
    public bool MMPose;

    private ServerUDP server;

    private Body body;


    public Transform GetLandmark(Landmark mark)
    {
        return body.instances[(int)mark].transform ;
    }

    private void Start()
    {
        System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.InvariantCulture;

        GameObject child = new GameObject("Landmarks");
        child.transform.SetParent(transform);
        child.transform.localPosition=new Vector3(0,-5,20);
        child.SetActive(false);
        bodyParent=child.transform;

        // Create a new GameObject
        GameObject linePrefab = new GameObject("linePrefab");

        // Add a LineRenderer component
        LineRenderer lineRenderer = linePrefab.AddComponent<LineRenderer>();

        // Set the line width
        lineRenderer.startWidth = 0.1f;
        lineRenderer.endWidth = 0.1f;

        // Optional: Set material to default (otherwise, the line may be invisible)
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));

        // Optional: Set a color
        lineRenderer.startColor = Color.cyan;
        lineRenderer.endColor = Color.cyan;

        body = new Body(bodyParent,linePrefab,landmarkScale);


        Thread t = new Thread(new ThreadStart(Run));
        t.Start();
    }
    private void Update()
    {
        UpdateBody(body);
    }


    private void CalculatePelvisRotation(Body b)
    {
        Vector3 p1=b.instances[(int)Landmark.LEFT_HIP].transform.localPosition;
        Vector3 p2=b.instances[(int)Landmark.RIGHT_HIP].transform.localPosition;
        
        Vector3 direction = (p2 - p1).normalized;
        
        Vector3 directionXZ = new Vector3(direction.x, 0, direction.z).normalized;
        float signedAngle = Vector3.SignedAngle(directionXZ, Vector3.right, Vector3.up);

        b.instances[(int)Landmark.PELVIS].transform.localRotation=Quaternion.Euler(0,-signedAngle,0);
    }
    
    private void CalculateRotationShoulder(Body b, int bone1, int bone2, int base_bone)
    {
        Vector3 p1=b.instances[bone1].transform.localPosition;
        Vector3 p2=b.instances[bone2].transform.localPosition;
        Quaternion base_rotation=b.instances[base_bone].transform.localRotation;

        Vector3 direction = (p2 - p1).normalized;
        
        Quaternion newRotation = Quaternion.LookRotation(direction, base_rotation * Vector3.up);

        // Apply the modified rotation
        b.instances[bone1].transform.localRotation = newRotation;
    }

    private void CalculateRotationShoulderRight(Body b, int bone1, int bone2, int base_bone)
    {
        Vector3 p1=b.instances[bone1].transform.localPosition;
        Vector3 p2=b.instances[bone2].transform.localPosition;
        Quaternion base_rotation=b.instances[base_bone].transform.localRotation;

        Vector3 direction = (p2 - p1).normalized;
        
        // Project direction into base_rotation's coordinate system
        Vector3 localDirection = Quaternion.Inverse(base_rotation) * direction;

        

        Vector3 directionXY = new Vector3(localDirection.x,localDirection.y, 0).normalized;
        float rotZ = Vector3.SignedAngle(directionXY, Vector3.right, Vector3.back);

        localDirection=Quaternion.Euler(0,0,-rotZ)*localDirection;
        
        Vector3 directionXZ = new Vector3(localDirection.x,0,localDirection.z).normalized;
        float rotY = Vector3.SignedAngle(directionXZ, Vector3.right, Vector3.down);

        // Apply the modified rotation
        b.instances[bone1].transform.localRotation = base_rotation*Quaternion.Euler(0,rotY,rotZ);
    }

    private void CalculateRotationShoulderLeft(Body b, int bone1, int bone2, int base_bone)
    {
        Vector3 p1=b.instances[bone1].transform.localPosition;
        Vector3 p2=b.instances[bone2].transform.localPosition;
        Quaternion base_rotation=b.instances[base_bone].transform.localRotation;

        Vector3 direction = (p2 - p1).normalized;
        
        // Project direction into base_rotation's coordinate system
        Vector3 localDirection = Quaternion.Inverse(base_rotation) * direction;

        

        Vector3 directionXY = new Vector3(localDirection.x,localDirection.y, 0).normalized;
        float rotZ = Vector3.SignedAngle(directionXY, Vector3.left, Vector3.back);

        localDirection=Quaternion.Euler(0,0,-rotZ)*localDirection;
        

        Vector3 directionXZ = new Vector3(localDirection.x,0,localDirection.z).normalized;
        float rotY = Vector3.SignedAngle(directionXZ, Vector3.left, Vector3.down);

        // Apply the modified rotation
        b.instances[bone1].transform.localRotation = base_rotation*Quaternion.Euler(0,rotY,rotZ);
    }

    private void CalculateRotationThigh(Body b, int bone1, int bone2, int base_bone)
    {
        Vector3 p1=b.instances[bone1].transform.localPosition;
        Vector3 p2=b.instances[bone2].transform.localPosition;
        Quaternion base_rotation=b.instances[base_bone].transform.localRotation;

        Vector3 direction = (p2 - p1).normalized;
        
        // Project direction into base_rotation's coordinate system
        Vector3 localDirection = Quaternion.Inverse(base_rotation) * direction;

        

        Vector3 directionYZ = new Vector3(0,localDirection.y, localDirection.z).normalized;
        float rotX = Vector3.SignedAngle(directionYZ, Vector3.down, Vector3.left);

        localDirection=Quaternion.Euler(-rotX,0,0)*localDirection;

        Vector3 directionXY = new Vector3(localDirection.x,localDirection.y, 0).normalized;
        float rotZ = Vector3.SignedAngle(directionXY, Vector3.down, Vector3.back);

        // Apply the modified rotation
        b.instances[bone1].transform.localRotation = base_rotation*Quaternion.Euler(rotX,0,rotZ);
    }

     private void CalculateRotationKnee(Body b, int bone1, int bone2, int bone3)
    {
        Vector3 p1=b.instances[bone1].transform.localPosition;
        Vector3 p2=b.instances[bone2].transform.localPosition;
        Vector3 p3=b.instances[bone3].transform.localPosition;

        // Compute Y-axis (normalized)
        Vector3 yAxis = (p1 - p2).normalized;
        
        // Compute X-axis (perpendicular to plane)
        Vector3 p32 = (p2 - p3).normalized;
        Vector3 xAxis = Vector3.Cross(yAxis, p32);

        // Compute Z-axis (ensuring right-handed coordinate system)
        Vector3 zAxis = Vector3.Cross(xAxis, yAxis);

        // Construct rotation matrix
        Matrix4x4 rotationMatrix = new Matrix4x4();
        rotationMatrix.SetColumn(0, new Vector4(xAxis.x, xAxis.y, xAxis.z, 0));
        rotationMatrix.SetColumn(1, new Vector4(yAxis.x, yAxis.y, yAxis.z, 0));
        rotationMatrix.SetColumn(2, new Vector4(zAxis.x, zAxis.y, zAxis.z, 0));
        rotationMatrix.SetColumn(3, new Vector4(0, 0, 0, 1)); // Homogeneous coordinate
        b.instances[bone1].transform.localRotation=rotationMatrix.rotation;
    }


    private void CalculateRotationElbowRight(Body b, int bone1, int bone2, int bone3)
    {
        Vector3 p1=b.instances[bone1].transform.localPosition;
        Vector3 p2=b.instances[bone2].transform.localPosition;
        Vector3 p3=b.instances[bone3].transform.localPosition;

        // Compute X-axis (normalized)
        Vector3 xAxis = (p2 - p1).normalized;
        
        // Compute Y-axis (perpendicular to plane)
        Vector3 p32 = (p1 - p3).normalized;
        Vector3 yAxis = Vector3.Cross(xAxis, p32);

        // Compute Z-axis (ensuring right-handed coordinate system)
        Vector3 zAxis = Vector3.Cross(xAxis, yAxis);

        // Construct rotation matrix
        Matrix4x4 rotationMatrix = new Matrix4x4();
        rotationMatrix.SetColumn(0, new Vector4(xAxis.x, xAxis.y, xAxis.z, 0));
        rotationMatrix.SetColumn(1, new Vector4(yAxis.x, yAxis.y, yAxis.z, 0));
        rotationMatrix.SetColumn(2, new Vector4(zAxis.x, zAxis.y, zAxis.z, 0));
        rotationMatrix.SetColumn(3, new Vector4(0, 0, 0, 1)); // Homogeneous coordinate
        b.instances[bone1].transform.localRotation=rotationMatrix.rotation;
    }

    private void CalculateRotationElbowLeft(Body b, int bone1, int bone2, int bone3)
    {
        Vector3 p1=b.instances[bone1].transform.localPosition;
        Vector3 p2=b.instances[bone2].transform.localPosition;
        Vector3 p3=b.instances[bone3].transform.localPosition;

        // Compute X-axis (normalized)
        Vector3 xAxis = (p1 - p2).normalized;
        
        // Compute Y-axis (perpendicular to plane)
        Vector3 p32 = (p1 - p3).normalized;
        Vector3 yAxis = Vector3.Cross(xAxis, p32);

        // Compute Z-axis (ensuring right-handed coordinate system)
        Vector3 zAxis = Vector3.Cross(xAxis, yAxis);

        // Construct rotation matrix
        Matrix4x4 rotationMatrix = new Matrix4x4();
        rotationMatrix.SetColumn(0, new Vector4(xAxis.x, xAxis.y, xAxis.z, 0));
        rotationMatrix.SetColumn(1, new Vector4(yAxis.x, yAxis.y, yAxis.z, 0));
        rotationMatrix.SetColumn(2, new Vector4(zAxis.x, zAxis.y, zAxis.z, 0));
        rotationMatrix.SetColumn(3, new Vector4(0, 0, 0, 1)); // Homogeneous coordinate
        b.instances[bone1].transform.localRotation=rotationMatrix.rotation;
    }

    public float getLeftElbowAngle(){
        return CalculateElbowAngle(body,(int)Landmark.LEFT_ELBOW,(int)Landmark.LEFT_WRIST,(int)Landmark.LEFT_SHOULDER);
    }

    public float getRightElbowAngle(){
        return CalculateElbowAngle(body,(int)Landmark.RIGHT_ELBOW,(int)Landmark.RIGHT_WRIST,(int)Landmark.RIGHT_SHOULDER);
    }

    private float CalculateElbowAngle(Body b, int bone1, int bone2, int bone3)
    {
        Vector3 p1=b.instances[bone1].transform.localPosition;
        Vector3 p2=b.instances[bone2].transform.localPosition;
        Vector3 p3=b.instances[bone3].transform.localPosition;

        // Compute X-axis (normalized)
        Vector3 xAxis = (p2-p1).normalized;
        
        // Compute Y-axis (perpendicular to plane)
        Vector3 p32 = (p3-p1).normalized;
        
       return Vector3.Angle(xAxis, p32);
    }


    private void CalculateTorsoRotation(Body b)
    {
        Vector3 p1=b.instances[(int)Landmark.SHOULDER_CENTER].transform.localPosition;
        Vector3 p2=b.instances[(int)Landmark.LEFT_SHOULDER].transform.localPosition;
        Vector3 p3=b.instances[(int)Landmark.PELVIS].transform.localPosition;

        // Compute X-axis (normalized)
        Vector3 xAxis = (p1 - p2).normalized;
        
        // Compute Y-axis (perpendicular to plane)
        Vector3 p32 = (p2 - p3).normalized;
        Vector3 yAxis = Vector3.Cross(xAxis, p32);

        // Compute X-axis (ensuring right-handed coordinate system)
        Vector3 zAxis = Vector3.Cross(xAxis,yAxis);

        // Construct rotation matrix
        Matrix4x4 rotationMatrix = new Matrix4x4();
        rotationMatrix.SetColumn(0, new Vector4(xAxis.x, xAxis.y, xAxis.z, 0));
        rotationMatrix.SetColumn(1, new Vector4(yAxis.x, yAxis.y, yAxis.z, 0));
        rotationMatrix.SetColumn(2, new Vector4(zAxis.x, zAxis.y, zAxis.z, 0));
        rotationMatrix.SetColumn(3, new Vector4(0, 0, 0, 1)); // Homogeneous coordinate
        b.instances[(int)Landmark.SHOULDER_CENTER].transform.localRotation=rotationMatrix.rotation;
        b.instances[(int)Landmark.SHOULDER_CENTER].transform.Rotate(-90,0,0);
    }



    private void UpdateBody(Body b)
    {
        for (int i = 0; i < LANDMARK_COUNT; ++i)
        {
            b.positions[i] = b.positionsBuffer[i].value *multiplier;
            b.instances[i].transform.localPosition=b.positions[i];
            if(b.positionsBuffer[i].visible)
            b.instances[i].GetComponent<Renderer>().enabled = true;
            else b.instances[i].GetComponent<Renderer>().enabled = false;
        }

        CalculatePelvisRotation(b);
        CalculateTorsoRotation(b);        
        
        CalculateRotationShoulderRight(b,(int)Landmark.RIGHT_SHOULDER,(int)Landmark.RIGHT_ELBOW,(int)Landmark.SHOULDER_CENTER);
        CalculateRotationShoulderLeft(b,(int)Landmark.LEFT_SHOULDER,(int)Landmark.LEFT_ELBOW,(int)Landmark.SHOULDER_CENTER);
        
        //CalculateRotationShoulder(b,(int)Landmark.RIGHT_SHOULDER,(int)Landmark.RIGHT_ELBOW,(int)Landmark.SHOULDER_CENTER);        
        //CalculateRotationShoulder(b,(int)Landmark.LEFT_SHOULDER,(int)Landmark.LEFT_ELBOW,(int)Landmark.SHOULDER_CENTER);

        CalculateRotationElbowRight(b,(int)Landmark.RIGHT_ELBOW,(int)Landmark.RIGHT_WRIST,(int)Landmark.RIGHT_SHOULDER);        
        CalculateRotationElbowLeft(b,(int)Landmark.LEFT_ELBOW,(int)Landmark.LEFT_WRIST,(int)Landmark.LEFT_SHOULDER); 

        CalculateRotationThigh(b,(int)Landmark.LEFT_HIP,(int)Landmark.LEFT_KNEE,(int)Landmark.PELVIS);        
        CalculateRotationThigh(b,(int)Landmark.RIGHT_HIP,(int)Landmark.RIGHT_KNEE,(int)Landmark.PELVIS); 

        CalculateRotationKnee(b,(int)Landmark.RIGHT_KNEE,(int)Landmark.RIGHT_ANKLE,(int)Landmark.RIGHT_HIP);        
        CalculateRotationKnee(b,(int)Landmark.LEFT_KNEE,(int)Landmark.LEFT_ANKLE,(int)Landmark.LEFT_HIP); 

        float angle=CalculateElbowAngle(b,(int)Landmark.RIGHT_ELBOW,(int)Landmark.RIGHT_WRIST,(int)Landmark.RIGHT_SHOULDER);
        if(angle>140){
            Quaternion q1=b.instances[(int)Landmark.RIGHT_ELBOW].transform.localRotation;
            Quaternion q2=b.instances[(int)Landmark.RIGHT_SHOULDER].transform.localRotation;
            float w=1-Math.Max(0,(160f-angle)/20f);
            b.instances[(int)Landmark.RIGHT_ELBOW].transform.localRotation=Quaternion.Lerp(q1,q2,w);
        }
        else {
            Quaternion q1=b.instances[(int)Landmark.RIGHT_ELBOW].transform.localRotation;
            Quaternion q2=b.instances[(int)Landmark.RIGHT_SHOULDER].transform.localRotation;
            q1=q1*Quaternion.Euler(0,180-angle,0);
            float w=Math.Max(0,(angle-120)/20f);
            b.instances[(int)Landmark.RIGHT_SHOULDER].transform.localRotation=Quaternion.Lerp(q1,q2,w);
        }

        angle=CalculateElbowAngle(b,(int)Landmark.LEFT_ELBOW,(int)Landmark.LEFT_WRIST,(int)Landmark.LEFT_SHOULDER);
        if(angle>140){
            Quaternion q1=b.instances[(int)Landmark.LEFT_ELBOW].transform.localRotation;
            Quaternion q2=b.instances[(int)Landmark.LEFT_SHOULDER].transform.localRotation;
            float w=1-Math.Max(0,(160f-angle)/20f);
            b.instances[(int)Landmark.LEFT_ELBOW].transform.localRotation=Quaternion.Lerp(q1,q2,w);
        }
        else {
            Quaternion q1=b.instances[(int)Landmark.LEFT_ELBOW].transform.localRotation;
            Quaternion q2=b.instances[(int)Landmark.LEFT_SHOULDER].transform.localRotation;
            q1=q1*Quaternion.Euler(0,180+angle,0);
            float w=Math.Max(0,(angle-120)/20f);
            b.instances[(int)Landmark.LEFT_SHOULDER].transform.localRotation=Quaternion.Lerp(q1,q2,w);
        }   
        
        b.UpdateLines();


        b.rotations[(int)Landmark.PELVIS]=GetLandmark(Landmark.PELVIS).localRotation;
        b.rotations[(int)Landmark.SHOULDER_CENTER]=Quaternion.Inverse(GetLandmark(Landmark.PELVIS).localRotation)*GetLandmark(Landmark.SHOULDER_CENTER).localRotation;
        b.rotations[(int)Landmark.LEFT_SHOULDER]=Quaternion.Inverse(GetLandmark(Landmark.SHOULDER_CENTER).localRotation)*GetLandmark(Landmark.LEFT_SHOULDER).localRotation;
        b.rotations[(int)Landmark.RIGHT_SHOULDER]=Quaternion.Inverse(GetLandmark(Landmark.SHOULDER_CENTER).localRotation)*GetLandmark(Landmark.RIGHT_SHOULDER).localRotation;
        b.rotations[(int)Landmark.LEFT_ELBOW]=Quaternion.Inverse(GetLandmark(Landmark.LEFT_SHOULDER).localRotation)*GetLandmark(Landmark.LEFT_ELBOW).localRotation;
        b.rotations[(int)Landmark.RIGHT_ELBOW]=Quaternion.Inverse(GetLandmark(Landmark.RIGHT_SHOULDER).localRotation)*GetLandmark(Landmark.RIGHT_ELBOW).localRotation;
        b.rotations[(int)Landmark.LEFT_HIP]=Quaternion.Inverse(GetLandmark(Landmark.PELVIS).localRotation)*GetLandmark(Landmark.LEFT_HIP).localRotation;
        b.rotations[(int)Landmark.RIGHT_HIP]=Quaternion.Inverse(GetLandmark(Landmark.PELVIS).localRotation)*GetLandmark(Landmark.RIGHT_HIP).localRotation;
        b.rotations[(int)Landmark.LEFT_KNEE]=Quaternion.Inverse(GetLandmark(Landmark.LEFT_HIP).localRotation)*GetLandmark(Landmark.LEFT_KNEE).localRotation;
        b.rotations[(int)Landmark.RIGHT_KNEE]=Quaternion.Inverse(GetLandmark(Landmark.RIGHT_HIP).localRotation)*GetLandmark(Landmark.RIGHT_KNEE).localRotation;
        

    }

    public Quaternion GetRotation(Landmark i)
    {
        return body.rotations[(int)i];
    }
    public void SetVisible(bool visible)
    {
        bodyParent.gameObject.SetActive(visible);
    }

    private void Run()
    {
        Debug.Log("Started");
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;

        server = new ServerUDP(host, port);
        server.Connect();
        server.StartListeningAsync();
        print("Listening @"+host+":"+port);        

        Landmark[] m=new Landmark[17];
        m[0]=Landmark.PELVIS;
        m[1]=Landmark.LEFT_HIP;
        m[2]=Landmark.LEFT_KNEE;
        m[3]=Landmark.LEFT_ANKLE;
        m[4]=Landmark.RIGHT_HIP;
        m[5]=Landmark.RIGHT_KNEE;
        m[6]=Landmark.RIGHT_ANKLE;
        m[7]=Landmark.SPINE;
        m[8]=Landmark.SHOULDER_CENTER;
        m[9]=Landmark.NECK;
        m[10]=Landmark.HEAD;
        m[11]=Landmark.RIGHT_SHOULDER;
        m[12]=Landmark.RIGHT_ELBOW;
        m[13]=Landmark.RIGHT_WRIST;
        m[14]=Landmark.LEFT_SHOULDER;
        m[15]=Landmark.LEFT_ELBOW;
        m[16]=Landmark.LEFT_WRIST;

        while (true)
        {
            try
            {
                Body h = body;
                var len = 0;
                var str = "";

                
                if(server.HasMessage())
                    str = server.GetMessage();
                else continue;
                len = str.Length;
                

                string[] lines = str.Split('\n');
                foreach (string l in lines)
                {
                    if (string.IsNullOrWhiteSpace(l))
                        continue;
                    string[] s = l.Split('|');
                    if (s.Length < 4) continue;
                    int i;
                    if (!int.TryParse(s[0], out i)) continue;
                    
                    if(MMPose)
                    {
                        i=(int)m[i];
                        h.positionsBuffer[i].value = new Vector3(float.Parse(s[1]), -float.Parse(s[2]), float.Parse(s[3]));
                        if(s.Length==5 && float.Parse(s[4])>0.5) h.positionsBuffer[i].visible=true;
                        else h.positionsBuffer[i].visible=false;
                    }
                    else {
                        h.positionsBuffer[i].value = new Vector3(float.Parse(s[1]), -float.Parse(s[2]), -float.Parse(s[3]));
                        if(s.Length==5 && float.Parse(s[4])>0.5) h.positionsBuffer[i].visible=true;
                        else h.positionsBuffer[i].visible=false;
                    }
                    
                    h.positionsBuffer[i].accumulatedValuesCount = 1;
                    
                    h.active = true;
                }

                if(!MMPose){
                    if(h.positionsBuffer[(int)Landmark.LEFT_HIP].visible && h.positionsBuffer[(int)Landmark.RIGHT_HIP].visible)
                    {
                        h.positionsBuffer[(int)Landmark.PELVIS].value=(h.positionsBuffer[(int)Landmark.LEFT_HIP].value+h.positionsBuffer[(int)Landmark.RIGHT_HIP].value)/2;
                        h.positionsBuffer[(int)Landmark.PELVIS].visible=true;
                    }else h.positionsBuffer[(int)Landmark.PELVIS].visible=false;

                    if(h.positionsBuffer[(int)Landmark.LEFT_SHOULDER].visible && h.positionsBuffer[(int)Landmark.RIGHT_SHOULDER].visible)
                    {
                        h.positionsBuffer[(int)Landmark.SHOULDER_CENTER].value=(h.positionsBuffer[(int)Landmark.LEFT_SHOULDER].value+h.positionsBuffer[(int)Landmark.RIGHT_SHOULDER].value)/2;
                        h.positionsBuffer[(int)Landmark.SHOULDER_CENTER].visible=true;
                    }else h.positionsBuffer[(int)Landmark.SHOULDER_CENTER].visible=false;

                }
                Debug.Log("NEW DATA");
            }
            catch (EndOfStreamException)
            {
                print("server Disconnected");
                break;
            }
        }

    }

    private void OnDisable()
    {
        print("server disconnected.");    
        server.Disconnect();
    
    }

    const int LANDMARK_COUNT = 38;
    const int LINES_COUNT = 11;

    public struct AccumulatedBuffer
    {
        public Vector3 value;
        public int accumulatedValuesCount;
        public bool visible;
        public AccumulatedBuffer(Vector3 v, int ac, bool vis)
        {
            value = v;
            accumulatedValuesCount = ac;
            visible=vis;
        }
    }

    public class Body
    {
        public Transform parent;
        public AccumulatedBuffer[] positionsBuffer = new AccumulatedBuffer[LANDMARK_COUNT];
        public Vector3[] positions = new Vector3[LANDMARK_COUNT];
        public Quaternion[] rotations = new Quaternion[LANDMARK_COUNT];
        public GameObject[] instances = new GameObject[LANDMARK_COUNT];
        public LineRenderer[] lines = new LineRenderer[LINES_COUNT];

        public bool active;

        private void MakeXYZ(GameObject o)
        {
            GameObject zAxis=GameObject.CreatePrimitive(PrimitiveType.Cube);
                zAxis.GetComponent<Renderer>().material.color = Color.blue; // Change to red
                zAxis.transform.localScale=new Vector3(0.1f,0.1f,2f);
                zAxis.transform.localPosition=new Vector3(0,0,1);
                zAxis.transform.parent=o.transform;
                zAxis.name =o.name+" z";

                GameObject yAxis=GameObject.CreatePrimitive(PrimitiveType.Cube);
                yAxis.GetComponent<Renderer>().material.color = Color.green; // Change to red
                yAxis.transform.localScale=new Vector3(0.1f,2f,0.1f);
                yAxis.transform.localPosition=new Vector3(0,1,0);
                yAxis.transform.parent=o.transform;
                yAxis.name =o.name+" y";

                GameObject xAxis=GameObject.CreatePrimitive(PrimitiveType.Cube);
                xAxis.GetComponent<Renderer>().material.color = Color.red; // Change to red
                xAxis.transform.localScale=new Vector3(2f,0.1f,0.1f);
                xAxis.transform.localPosition=new Vector3(1,0,0);
                xAxis.transform.parent=o.transform;
                xAxis.name =o.name+" x";
        }

        public Body(Transform parent, GameObject linePrefab, float s)
        {
            this.parent = parent;
            for (int i = 0; i < instances.Length; ++i)
            {
                string name=((Landmark)i).ToString();
                if(true&&(name.Contains("SHOULDER")||name.Contains("ELBOW")||name.Contains("HIP")||name.Contains("KNEE")||name.Contains("PELVIS")))
                {
                     instances[i] = GameObject.CreatePrimitive(PrimitiveType.Cube);//Instantiate(landmarkPrefab);
                     instances[i].transform.localScale = Vector3.one * s;
                }
                else{
                    instances[i] = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    instances[i].transform.localScale = Vector3.one * s*1.0f;
                }
                instances[i].transform.parent = parent;
                instances[i].name = name;
                instances[i].GetComponent<Renderer>().enabled = false;
            }

            MakeXYZ(instances[(int)Landmark.RIGHT_SHOULDER]);
            MakeXYZ(instances[(int)Landmark.LEFT_SHOULDER]);

            MakeXYZ(instances[(int)Landmark.RIGHT_ELBOW]);
            MakeXYZ(instances[(int)Landmark.LEFT_ELBOW]);

            MakeXYZ(instances[(int)Landmark.RIGHT_HIP]);
            MakeXYZ(instances[(int)Landmark.LEFT_HIP]);

            MakeXYZ(instances[(int)Landmark.RIGHT_KNEE]);
            MakeXYZ(instances[(int)Landmark.LEFT_KNEE]);

            MakeXYZ(instances[(int)Landmark.PELVIS]);
            MakeXYZ(instances[(int)Landmark.SHOULDER_CENTER]);

            for (int i = 0; i < lines.Length; ++i)
            {
                lines[i] = Instantiate(linePrefab).GetComponent<LineRenderer>();
                lines[i].transform.parent = parent;
            }
        }
        public void UpdateLines()
        {
            

            //LEFT LEG
            lines[2].positionCount = 4;
            lines[2].SetPosition(0, Position(Landmark.LEFT_ANKLE));
            lines[2].SetPosition(1, Position(Landmark.LEFT_KNEE));
            lines[2].SetPosition(2, Position(Landmark.LEFT_HIP));
            lines[2].SetPosition(3, Position(Landmark.PELVIS));
            //RIGHT LEG
            lines[3].positionCount = 4;
            lines[3].SetPosition(0, Position(Landmark.RIGHT_ANKLE));
            lines[3].SetPosition(1, Position(Landmark.RIGHT_KNEE));
            lines[3].SetPosition(2, Position(Landmark.RIGHT_HIP));
            lines[3].SetPosition(3, Position(Landmark.PELVIS));

            //TORSO
            lines[4].positionCount = 5;
            lines[4].SetPosition(0, Position(Landmark.PELVIS));
            lines[4].SetPosition(1, Position(Landmark.SPINE));
            lines[4].SetPosition(2, Position(Landmark.SHOULDER_CENTER));
            lines[4].SetPosition(3, Position(Landmark.NECK));
            lines[4].SetPosition(4, Position(Landmark.HEAD));

            //LEFT ARM
            lines[5].positionCount = 4;
            lines[5].SetPosition(0, Position(Landmark.LEFT_WRIST));
            lines[5].SetPosition(1, Position(Landmark.LEFT_ELBOW));
            lines[5].SetPosition(2, Position(Landmark.LEFT_SHOULDER));
            lines[5].SetPosition(3, Position(Landmark.SHOULDER_CENTER));
           //RIGHT ARM
            lines[6].positionCount = 4;
            lines[6].SetPosition(0, Position(Landmark.RIGHT_WRIST));
            lines[6].SetPosition(1, Position(Landmark.RIGHT_ELBOW));
            lines[6].SetPosition(2, Position(Landmark.RIGHT_SHOULDER));
            lines[6].SetPosition(3, Position(Landmark.SHOULDER_CENTER));
        }

        public Vector3 Direction(Landmark from,Landmark to)
        {
            return (instances[(int)to].transform.position - instances[(int)from].transform.position).normalized;
        }
        public float Distance(Landmark from, Landmark to)
        {
            return (instances[(int)from].transform.position - instances[(int)to].transform.position).magnitude;
        }
        public Vector3 LocalPosition(Landmark Mark)
        {
            return instances[(int)Mark].transform.localPosition;
        }
        public Vector3 Position(Landmark Mark)
        {
            return instances[(int)Mark].transform.position;
        }

    }
}
