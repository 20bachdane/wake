﻿using System.Collections;
using System.Collections.Generic;
using BeauRoutine;
using UnityEngine;

public class MaterialGlitchSwap : MonoBehaviour
{

    public MeshRenderer meshForMatSwap;
    public Material glitchMaterial;
    public float glitchMin = 0.3f;
    public float glitchMax = 0.8f;
    private Material startingMaterial;
    public bool glitching = false;
    public GameObject glitchingObject;
    public GameObject finalObject;
    private bool matSwapped = false;

    private Routine glitchRoutine;


    // Start is called before the first frame update
    void Start()
    {
        if (meshForMatSwap)
        {
            startingMaterial = meshForMatSwap.material;
        }
    }

    private void Update()
    {
        if (glitching)
        {
            if (startingMaterial && glitchMaterial && !glitchRoutine)
            {
                glitchRoutine.Replace(this, MaterialGlitch());
            }
        }
    }

    public void StartGlitching()
    {
        glitching = true;
    }

    public void StopGlitching()
    {
        glitching = false;
        glitchRoutine.Stop();
        if (glitchingObject && finalObject)
        {
            glitchingObject.SetActive(false);
            finalObject.SetActive(true);
        }
    }


    IEnumerator MaterialGlitch()
    {
        while(true)
        {
            float delay = Random.Range(glitchMin, glitchMax);
            yield return delay;
            meshForMatSwap.material = glitchMaterial;
            yield return delay;
            meshForMatSwap.material = startingMaterial;
        }
    }
}
