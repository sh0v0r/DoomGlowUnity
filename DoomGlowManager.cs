using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DoomGlowManager : MonoBehaviour
{
    public bool alwaysUpdate = false;
    DoomGlow[] glows;

    private void Awake()
    {
        glows = GetComponentsInChildren<DoomGlow>();
    }

    private void LateUpdate()
    {        
        foreach (DoomGlow dg in glows)
        {
            if (alwaysUpdate || dg.meshRenderer.isVisible)
            {
                dg.UpdateMeshVR();        
            }
        }        
    }
}
