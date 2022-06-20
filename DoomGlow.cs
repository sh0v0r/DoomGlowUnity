// This file is in the public domain. Where
// a public domain declaration is not recognized, you are granted
// a license to freely use, modify, and redistribute this file in
// any way you choose.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Unity remake of a fake volumetric light glow effect
// DoomGlow code by tim.tili.sabo@gmail.com -- http://yzergame.com/doomGlare.html
// Ideas by Simon https://simonschreibt.de/gat/doom-3-volumetric-glow/
//
// Unity Port: Sean'sh0v0r' Edwards
// This is a port of the Unreal Version by: @hollowdilnik https://twitter.com/hollowdilnik/status/1538098146588430336
// it doesn't support all of the same features
// It has been optimised and uses a parent Manager class to call the Update Mesh method if it is visible
// The original Unity version relied on Screen Space conversions which didn't work well with VR

public class DoomGlow : MonoBehaviour
{
    public float quadSize = 1.0f;
    public Color quadColor = new Color(0.0f, 1.0f, 1.0f, 0.95f);
    public Color edgeColor = new Color(0.0f, 0.0f, 0.0f, 0.02f);
    public float pushDistance = 0.3f;
    public bool showBack = true;
    public bool showQuad = true;
    public bool showBounds = false;

    public MeshRenderer meshRenderer;       
    
    MeshFilter meshFilter;
    Mesh mesh;
    
    public Camera mainCamera;
    public Transform mainCameraXForm;
    Transform xForm;

    Color colorFilled;
    Color colorEdge;
    Vector3 quadNormal;
    
    int[] indexBuffer;
    int[] indexBufferFull;
    Vector3[] quadPoints = new Vector3[4];
    Vector3[] vertexBuffer = new Vector3[16];
    Color[] colorBuffer = new Color[16];
    Vector3[] eyeToPoint_WS = new Vector3[4];

    private void Awake()
    {
        xForm = transform;
        if (mainCameraXForm == null)
        {
            mainCameraXForm = GameManager.instance.playerEyeXForm;
        }
        mainCamera = mainCameraXForm.GetComponent<Camera>();
        meshRenderer = GetComponent<MeshRenderer>();
        
        colorFilled = quadColor;
        colorEdge = edgeColor;
        colorEdge.a = 0;
        
        indexBufferFull =  new int[] {
        0,1,2, 0,2,3, // Quad
		0,5,7, 0,7,1, 1,8,10, 1,10,2, 2,11,13, 2,13,3, 3,14,4, 3,4,0, // Flaps
		0,4,6, 0,6,5, 1,7,9, 1,9,8, 2,10,12, 2,12,11, 3,13,15, 3,15,14}; // Connections
            
        indexBuffer = new int [] {
        0,5,7, 0,7,1, 1,8,10, 1,10,2, 2,11,13, 2,13,3, 3,14,4, 3,4,0, // Flaps
		0,4,6, 0,6,5, 1,7,9, 1,9,8, 2,10,12, 2,12,11, 3,13,15, 3,15,14}; // Connections
    }
        

    void Start()
    {
        if (mainCamera == null)
        {
            gameObject.SetActive(false);
            return;
        }

        meshFilter = GetComponent<MeshFilter>();       

        UpdateQuadPoints();

        mesh = new Mesh();
        meshFilter.mesh = mesh;
                               
        // get normal of quad        
        quadNormal = -xForm.forward;                
    }

    void UpdateQuadPoints()
    {
        quadPoints[0] = new Vector3(-quadSize / 2, -quadSize / 2, 0);
        quadPoints[1] = new Vector3(quadSize / 2, -quadSize / 2, 0);
        quadPoints[2] = new Vector3(quadSize / 2, quadSize / 2, 0);
        quadPoints[3] = new Vector3(-quadSize / 2, quadSize / 2, 0);

        for (int i = 0; i < 4; i++)
        {
            vertexBuffer[i] = xForm.TransformPoint(quadPoints[i]);
        }
    }

    float LinearMap(float inVal, float inFrom, float inTo, float outFrom, float outTo)
    {
        float inScale = (inFrom != inTo) ? ((inVal - inFrom) / (inTo - inFrom)) : 0.0f;
        inScale = Mathf.Clamp(inScale, 0.0f, 1.0f);
        return Mathf.Lerp(outFrom, outTo, inScale);
    }

    Vector3[] pushDirWS = new Vector3[3];

    void Swap(ref Vector3 A, ref Vector3 B)
    {
        Vector3 tempA = new Vector3(A.x, A.y, A.z);
        Vector3 tempB = new Vector3(B.x, B.y, B.z);
        A.x = tempB.x;
        A.y = tempB.y;
        A.z = tempB.z;
        B.x = tempA.x;
        B.y = tempA.y;
        B.z = tempA.z;
    }

    public float dot;
    public float sign;

    bool boundsRecalculated = false;
    public void UpdateMeshVR()
    {
#if UNITY_EDITOR
        quadNormal = -xForm.forward;
#endif
        Vector3 cameraLocalPosition = xForm.InverseTransformPoint(mainCameraXForm.position);
        Vector3 directionToCenter = (xForm.position - mainCameraXForm.position).normalized;
        
        dot = Vector3.Dot(directionToCenter, quadNormal);

        sign = Mathf.Sign(dot);

        float alpha = LinearMap(Mathf.Abs(dot), 0.001f, 0.1f, 0.0f, 1.0f);

        for (int i = 0; i < 4; ++i)
        {
            vertexBuffer[i] = quadPoints[i];
        }

        if (dot < 0)
        {
            // Just flip the quad
            //Swap(ref vertexBuffer[1], ref vertexBuffer[3]);
            sign = -1;            
        }
        
        for (int i = 0; i < 4; i++)
        {                    
            eyeToPoint_WS[i] = xForm.TransformVector((vertexBuffer[i] - cameraLocalPosition)).normalized;            
        }        

        // extrude quad vertices
        for (int i = 0; i < 4; i++)
        {            
            pushDirWS[0] = sign * Vector3.Cross(eyeToPoint_WS[i], eyeToPoint_WS[(i + 3) % 4]).normalized;
            pushDirWS[1] = sign * Vector3.Cross(eyeToPoint_WS[(i+1)%4], eyeToPoint_WS[i]).normalized;
            pushDirWS[2] = (pushDirWS[0] + pushDirWS[1]).normalized;

            for (int j = 0; j < 3; j++)
            {
                Vector3 offset = pushDistance * pushDirWS[j];
                offset = xForm.InverseTransformVector(-offset);
                vertexBuffer[4 + j + 3 * i] = vertexBuffer[i] + offset;
            }
        }

        // update colours
#if UNITY_EDITOR
        colorFilled = quadColor;
#endif        
        colorFilled.a = (alpha * quadColor.a);
#if UNITY_EDITOR
        colorEdge = edgeColor;
        colorEdge.a = 0;
#endif
        // set base colour
        for (int i = 0; i < 4; i++)
        {
            colorBuffer[i] = colorFilled;
        }

        // set edge colour
        for (int i = 4; i < 16; i++)
        {
            colorBuffer[i] = colorEdge;
        }

        // fill mesh
        mesh.vertices = vertexBuffer;
        mesh.colors = colorBuffer;
        mesh.triangles = (showQuad)?indexBufferFull:indexBuffer;
        meshFilter.mesh = mesh;

        if (!boundsRecalculated) // only should need to do this once
        {
            mesh.RecalculateBounds(UnityEngine.Rendering.MeshUpdateFlags.Default);
            boundsRecalculated = true;
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (mesh && showBounds)
        {
            Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.25f);
            Gizmos.DrawCube(mesh.bounds.center, mesh.bounds.size);            
        }
    }
#endif
}